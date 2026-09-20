using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace TM.Services;

public enum SignatureVerificationResult { Invalid, ValidUntrusted, ValidTrusted }

/// <summary>Compatible detached CMS signatures for document certificates.</summary>
public sealed class DocumentSignatureService(ProjectCryptoService crypto)
{
    public const long MaximumInputBytes = 64L * 1024 * 1024;

    public async Task CreateAsync(ProjectDocument document, string sourcePath, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        string source = Validate(sourcePath, destinationPath);
        byte[]? data = null;
        byte[]? signature = null;
        string staging = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!, $".tm-signature-{Guid.NewGuid():N}");
        string temporary = Path.Combine(staging, "payload");
        bool created = false;
        try
        {
            X509Certificate2 certificate = document.Security.CaCertificate ?? throw new InvalidOperationException("Generate a document certificate first.");
            Directory.CreateDirectory(staging); created = true;
            data = await File.ReadAllBytesAsync(source, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            signature = await Task.Run(() => crypto.SignFile(document, source), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllBytesAsync(temporary, signature, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, Path.GetFullPath(destinationPath), false);
        }
        finally
        {
            ProjectCryptoService.ClearArray(ref data); ProjectCryptoService.ClearArray(ref signature);
            if (created) { if (File.Exists(temporary)) File.Delete(temporary); Directory.Delete(staging); }
        }
    }

    public async Task<SignatureVerificationResult> VerifyAsync(string dataPath, string signaturePath, CancellationToken cancellationToken = default)
    {
        EnsureLength(dataPath); EnsureLength(signaturePath);
        byte[]? data = null; byte[]? signature = null;
        try
        {
            data = await File.ReadAllBytesAsync(dataPath, cancellationToken);
            signature = await File.ReadAllBytesAsync(signaturePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SignedCms cms = new(new ContentInfo(data), detached: true);
            try { cms.Decode(signature); cms.CheckSignature(verifySignatureOnly: true); }
            catch (CryptographicException) { return SignatureVerificationResult.Invalid; }
            foreach (X509Certificate2 certificate in cms.Certificates)
            {
                using X509Chain chain = new();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                if (chain.Build(certificate)) return SignatureVerificationResult.ValidTrusted;
            }
            return SignatureVerificationResult.ValidUntrusted;
        }
        catch (ArgumentException) { return SignatureVerificationResult.Invalid; }
        finally { ProjectCryptoService.ClearArray(ref data); ProjectCryptoService.ClearArray(ref signature); }
    }

    private static string Validate(string sourcePath, string destinationPath)
    {
        string source = Path.GetFullPath(sourcePath), destination = Path.GetFullPath(destinationPath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) || File.Exists(destination)) throw new IOException("Choose a new output file.");
        EnsureLength(source); return source;
    }
    private static void EnsureLength(string path)
    {
        long length = new FileInfo(path).Length;
        if (length is 0) throw new CryptographicException("Detached CMS signatures require a non-empty file.");
        if (length > MaximumInputBytes) throw new IOException("The selected file exceeds the supported signature limit.");
    }
}
