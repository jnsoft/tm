using System.Security;
using System.Security.Cryptography.X509Certificates;

namespace TM.Services;

/// <summary>Compatible PFX import and public DER certificate export without OS-store mutation.</summary>
public sealed class DocumentCertificateService
{
    public const long MaximumPfxBytes = 16L * 1024 * 1024;

    public async Task<X509Certificate2> ImportAsync(string pfxPath, SecureString password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);
        cancellationToken.ThrowIfCancellationRequested();
        string path = Path.GetFullPath(pfxPath);
        if (new FileInfo(path).Length > MaximumPfxBytes) throw new IOException("The selected PFX exceeds the supported import limit.");
        byte[]? pfx = null;
        try
        {
            pfx = await File.ReadAllBytesAsync(path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            X509Certificate2 certificate = X509Helper.X509FromPfx(pfx, password);
            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                throw new ArgumentException("The PFX does not contain a private key.");
            }
            return certificate;
        }
        finally { ProjectCryptoService.ClearArray(ref pfx); }
    }

    public async Task ExportPublicAsync(X509Certificate2 certificate, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        cancellationToken.ThrowIfCancellationRequested();
        string destination = Path.GetFullPath(destinationPath);
        if (File.Exists(destination)) throw new IOException("Choose a new output file.");
        string staging = Path.Combine(Path.GetDirectoryName(destination)!, $".tm-certificate-{Guid.NewGuid():N}");
        string temporary = Path.Combine(staging, "payload");
        bool created = false;
        try
        {
            Directory.CreateDirectory(staging); created = true;
            await Task.Run(() => X509Helper.SaveX509ToCerFile(certificate, temporary), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (created) { if (File.Exists(temporary)) File.Delete(temporary); Directory.Delete(staging); }
        }
    }
}
