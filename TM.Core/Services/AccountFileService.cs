using System.Security.Cryptography;

namespace TM.Services;

public enum AccountFileOperation { Encrypt, Decrypt }

/// <summary>Creates a new EFS-protected or decrypted copy without modifying the source.</summary>
public sealed class AccountFileService(IAccountFileProtection protection)
{
    public async Task ExecuteAsync(AccountFileOperation operation, string sourcePath, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        cancellationToken.ThrowIfCancellationRequested();
        string source = Path.GetFullPath(sourcePath);
        string destination = Path.GetFullPath(destinationPath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) || File.Exists(destination))
            throw new IOException("Choose a new output file.");
        if (operation is AccountFileOperation.Decrypt && !protection.IsEncrypted(source))
            throw new CryptographicException("The source is not EFS encrypted.");
        string staging = Path.Combine(Path.GetDirectoryName(destination)!, $".tm-account-{Guid.NewGuid():N}");
        string temporary = Path.Combine(staging, "payload");
        bool created = false;
        try
        {
            if (Directory.Exists(staging)) throw new IOException("Staging directory already exists.");
            Directory.CreateDirectory(staging);
            created = true;
            await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true))
            await using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                await input.CopyToAsync(output, cancellationToken);

            bool encrypted = operation is AccountFileOperation.Encrypt;
            // Do not abandon synchronous EFS work or delete its payload before it completes.
            await Task.Run(() => protection.SetEncrypted(temporary, encrypted), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (protection.IsEncrypted(temporary) != encrypted)
                throw new CryptographicException("The requested EFS state was not applied.");
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (created)
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                Directory.Delete(staging); // Never recursively delete unexpected files.
            }
        }
    }
}
