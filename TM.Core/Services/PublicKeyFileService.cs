using System.Security.Cryptography;

namespace TM.Services;

public enum PublicKeyFileOperation { Encrypt, Decrypt }

/// <summary>Compatible ECDH-derived AES-GCM file copies for a document private key and peer SPKI key.</summary>
public sealed class PublicKeyFileService(ProjectCryptoService crypto)
{
    public const long MaximumInputBytes = 64L * 1024 * 1024;

    public async Task ExecuteAsync(ProjectDocument document, PublicKeyFileOperation operation, ReadOnlyMemory<byte> peerPublicKey,
        string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (peerPublicKey.Length is 0 or > 16384) throw new ArgumentException("Enter a valid peer public key.", nameof(peerPublicKey));
        cancellationToken.ThrowIfCancellationRequested();
        string source = Path.GetFullPath(sourcePath);
        string destination = Path.GetFullPath(destinationPath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) || File.Exists(destination))
            throw new IOException("Choose a new output file.");
        FileInfo info = new(source);
        if (info.Length > MaximumInputBytes) throw new IOException("The selected file exceeds the supported public-key operation limit.");
        string staging = Path.Combine(Path.GetDirectoryName(destination)!, $".tm-public-key-{Guid.NewGuid():N}");
        string temporary = Path.Combine(staging, "payload");
        byte[]? privateKey = null;
        byte[]? sharedKey = null;
        byte[]? peerKey = null;
        byte[]? input = null;
        byte[]? output = null;
        bool created = false;
        try
        {
            if (Directory.Exists(staging)) throw new IOException("Staging directory already exists.");
            Directory.CreateDirectory(staging);
            created = true;
            input = await File.ReadAllBytesAsync(source, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            privateKey = crypto.GetUnprotectedPrivateKey(document) ?? throw new InvalidOperationException("Generate document ECDH keys first.");
            peerKey = peerPublicKey.ToArray();
            sharedKey = SecurityHelper.DeriveSymmetricKey(privateKey, peerKey);
            if (sharedKey.Length != 32) throw new CryptographicException("The derived ECDH key is invalid.");
            try
            {
                output = await Task.Run(() => operation switch
                {
                    PublicKeyFileOperation.Encrypt => SecurityHelper.GCMEncrypt(input, sharedKey),
                    PublicKeyFileOperation.Decrypt => SecurityHelper.GCMDecrypt(input, sharedKey),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation))
                }, cancellationToken);
            }
            catch (ArgumentException error) when (operation is PublicKeyFileOperation.Decrypt)
            {
                throw new CryptographicException("The public-key payload is invalid.", error);
            }
            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllBytesAsync(temporary, output, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            ProjectCryptoService.ClearArray(ref privateKey);
            ProjectCryptoService.ClearArray(ref sharedKey);
            ProjectCryptoService.ClearArray(ref peerKey);
            ProjectCryptoService.ClearArray(ref input);
            ProjectCryptoService.ClearArray(ref output);
            if (created)
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                Directory.Delete(staging); // Never recursively delete unexpected files.
            }
        }
    }
}
