using System.Security.Cryptography;

namespace TM.Services;

public enum PasswordFileOperation { Encrypt, Decrypt }

/// <summary>Non-destructive adapter for the existing jnUtil password file format.</summary>
public sealed class PasswordFileService
{
    public async Task ExecuteAsync(PasswordFileOperation operation, string sourcePath, string destinationPath,
        SecureString password, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (password.Length is 0 or > 4096) throw new ArgumentException("Enter a valid file password.", nameof(password));
        cancellationToken.ThrowIfCancellationRequested();
        string source = Path.GetFullPath(sourcePath);
        string destination = Path.GetFullPath(destinationPath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) || File.Exists(destination))
            throw new IOException("Choose a new output file.");
        string staging = Path.Combine(Path.GetDirectoryName(destination)!, $".tm-crypto-{Guid.NewGuid():N}");
        string temporary = Path.Combine(staging, "payload");
        bool created = false;
        try
        {
            // jnUtil requires a nonexistent output path, so isolate it in a staging directory.
            if (Directory.Exists(staging)) throw new IOException("Staging directory already exists.");
            Directory.CreateDirectory(staging);
            created = true;
            bool succeeded = await Task.Run(() => operation switch
            {
                PasswordFileOperation.Encrypt => SecurityHelper.AesEncryptFile(source, temporary, password),
                PasswordFileOperation.Decrypt => SecurityHelper.AesDecryptFile(source, temporary, password),
                _ => false
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!succeeded) throw new CryptographicException("The file operation failed.");
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
