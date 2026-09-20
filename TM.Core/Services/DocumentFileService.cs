using System.Security.Cryptography;

namespace TM.Services;

public enum DocumentFileOperation { Encrypt, Decrypt }

/// <summary>Caller must serialize document changes until this operation completes.</summary>
public sealed class DocumentFileService(ProjectCryptoService crypto)
{
    public async Task ExecuteAsync(ProjectDocument document, DocumentFileOperation operation, string sourcePath,
        string destinationPath, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (!document.IsFileLoaded || document.IsLocked || document.Security.ProtectedMasterKey is null)
            throw new InvalidOperationException("Open or unlock a document first.");
        cancellationToken.ThrowIfCancellationRequested();
        string source = Path.GetFullPath(sourcePath);
        string destination = Path.GetFullPath(destinationPath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) || File.Exists(destination))
            throw new IOException("Choose a new output file.");
        string staging = Path.Combine(Path.GetDirectoryName(destination)!, $".tm-document-crypto-{Guid.NewGuid():N}");
        string temporary = Path.Combine(staging, "payload");
        bool created = false;
        byte[]? key = null;
        byte[]? originalKey = null;
        try
        {
            byte[] salt = operation is DocumentFileOperation.Encrypt
                ? SecurityHelper.GetRandomKey(ProjectCryptoService.SaltLength)
                : SecurityHelper.AesGetSaltToDecryptFile(source, ProjectCryptoService.SaltLength);
            if (salt.Length != ProjectCryptoService.SaltLength) throw new CryptographicException("Invalid file salt.");
            key = crypto.DeriveKey(document, "file encryption", salt);
            originalKey = key;
            if (Directory.Exists(staging)) throw new IOException("Staging directory already exists.");
            Directory.CreateDirectory(staging);
            created = true;
            bool succeeded = await Task.Run(() => operation switch
            {
                DocumentFileOperation.Encrypt => SecurityHelper.AesEncryptFile(source, temporary, ref key, salt),
                DocumentFileOperation.Decrypt => SecurityHelper.AesDecryptFile(source, temporary, ref key, ProjectCryptoService.SaltLength),
                _ => false
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!succeeded) throw new CryptographicException("The file operation failed.");
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            ProjectCryptoService.ClearArray(ref key);
            ProjectCryptoService.ClearArray(ref originalKey);
            if (created)
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                Directory.Delete(staging);
            }
        }
    }
}
