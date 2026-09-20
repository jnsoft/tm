using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TM.Models;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class DocumentCertificateTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.Certificates.{Guid.NewGuid():N}");
    [TestInitialize] public void Initialize() => Directory.CreateDirectory(directory);
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private string PathFor(string name) => Path.Combine(directory, name);

    [TestMethod]
    public async Task PfxImportAndCerExport_InteroperateWithLegacyAsync()
    {
        using X509Certificate2 source = X509Helper.CreateCACert("TM synthetic certificate", null);
        using SecureString password = "synthetic-pfx-password".ToCharArray().ToSecureStringAndClear();
        byte[] pfx = X509Helper.X509ToPfx(source, password);
        await File.WriteAllBytesAsync(PathFor("source.pfx"), pfx);
        CryptographicOperations.ZeroMemory(pfx);
        DocumentCertificateService service = new();
        using X509Certificate2 imported = await service.ImportAsync(PathFor("source.pfx"), password);
        Assert.IsTrue(imported.HasPrivateKey);
        await service.ExportPublicAsync(imported, PathFor("exported.cer"));
        byte[] legacy = File.ReadAllBytes(PathFor("exported.cer"));
        try
        {
            using X509Certificate2 publicCertificate = new(legacy);
            Assert.IsFalse(publicCertificate.HasPrivateKey);
            Assert.AreEqual(imported.Thumbprint, publicCertificate.Thumbprint);
        }
        finally { CryptographicOperations.ZeroMemory(legacy); }
        Assert.HasCount(0, Directory.GetDirectories(directory));
    }

    [TestMethod]
    public async Task RejectedCertificateOperations_PreserveOutputsAndCleanupAsync()
    {
        DocumentCertificateService service = new();
        await File.WriteAllTextAsync(PathFor("malformed.pfx"), "not a pfx");
        await File.WriteAllTextAsync(PathFor("existing.cer"), "keep output");
        using SecureString password = "synthetic-pfx-password".ToCharArray().ToSecureStringAndClear();
        await Assert.ThrowsAsync<CryptographicException>(() => service.ImportAsync(PathFor("malformed.pfx"), password));
        using X509Certificate2 certificate = X509Helper.CreateCACert("TM synthetic certificate", null);
        await Assert.ThrowsAsync<IOException>(() => service.ExportPublicAsync(certificate, PathFor("existing.cer")));
        await using (FileStream large = new(PathFor("large.pfx"), FileMode.CreateNew, FileAccess.Write)) large.SetLength(DocumentCertificateService.MaximumPfxBytes + 1);
        await Assert.ThrowsAsync<IOException>(() => service.ImportAsync(PathFor("large.pfx"), password));
        using CancellationTokenSource canceled = new(); canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExportPublicAsync(certificate, PathFor("canceled.cer"), canceled.Token));
        Assert.AreEqual("keep output", await File.ReadAllTextAsync(PathFor("existing.cer")));
        Assert.HasCount(3, Directory.GetFiles(directory));
        Assert.HasCount(0, Directory.GetDirectories(directory));
    }
}
