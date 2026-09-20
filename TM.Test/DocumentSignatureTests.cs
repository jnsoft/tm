using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TM.Models;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class DocumentSignatureTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.Signatures.{Guid.NewGuid():N}");
    private readonly ProjectCryptoService crypto = new();
    [TestInitialize] public void Initialize() => Directory.CreateDirectory(directory);
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private string PathFor(string name) => Path.Combine(directory, name);

    [TestMethod]
    [DataRow(200001)]
    public async Task Signatures_InteroperateWithLegacyCmsAsync(int size)
    {
        byte[] data = RandomNumberGenerator.GetBytes(size);
        await File.WriteAllBytesAsync(PathFor("data"), data);
        ProjectDocument document = CreateDocumentWithCertificate();
        DocumentSignatureService service = new(crypto);
        try
        {
            await service.CreateAsync(document, PathFor("data"), PathFor("signature"));
            Assert.AreNotEqual(SignatureVerificationResult.Invalid, await service.VerifyAsync(PathFor("data"), PathFor("signature")));
            Assert.IsTrue(CMSHelper.Verify(data, await File.ReadAllBytesAsync(PathFor("signature")), true));
            byte[] legacy = crypto.SignFile(document, PathFor("data"));
            try { await File.WriteAllBytesAsync(PathFor("legacy.p7c"), legacy); }
            finally { CryptographicOperations.ZeroMemory(legacy); }
            Assert.AreNotEqual(SignatureVerificationResult.Invalid, await service.VerifyAsync(PathFor("data"), PathFor("legacy.p7c")));
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("data")));
            Assert.HasCount(0, Directory.GetDirectories(directory));
        }
        finally { document.Security.CaCertificate?.Dispose(); crypto.ClearAll(document); }
    }

    [TestMethod]
    public async Task InvalidSignatures_DoNotPublishOrModifyFilesAsync()
    {
        await File.WriteAllTextAsync(PathFor("data"), "synthetic signed content");
        await File.WriteAllTextAsync(PathFor("existing"), "keep output");
        ProjectDocument document = CreateDocumentWithCertificate();
        DocumentSignatureService service = new(crypto);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(document, PathFor("data"), PathFor("data")));
            await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(document, PathFor("data"), PathFor("existing")));
            await Assert.ThrowsAsync<FileNotFoundException>(() => service.CreateAsync(document, PathFor("missing"), PathFor("signature")));
            using CancellationTokenSource canceled = new(); canceled.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => service.CreateAsync(document, PathFor("data"), PathFor("canceled"), canceled.Token));
            await File.WriteAllBytesAsync(PathFor("malformed"), [1, 2, 3]);
            Assert.AreEqual(SignatureVerificationResult.Invalid, await service.VerifyAsync(PathFor("data"), PathFor("malformed")));
            await service.CreateAsync(document, PathFor("data"), PathFor("valid"));
            await File.WriteAllTextAsync(PathFor("data"), "changed content");
            Assert.AreEqual(SignatureVerificationResult.Invalid, await service.VerifyAsync(PathFor("data"), PathFor("valid")));
            Assert.AreEqual("keep output", await File.ReadAllTextAsync(PathFor("existing")));
            Assert.HasCount(4, Directory.GetFiles(directory));
            Assert.HasCount(0, Directory.GetDirectories(directory));
        }
        finally { document.Security.CaCertificate?.Dispose(); crypto.ClearAll(document); }
    }

    [TestMethod]
    public async Task CertificateAndInputLimits_AreEnforcedAsync()
    {
        await File.WriteAllTextAsync(PathFor("data"), "synthetic signed content");
        ProjectDocument withoutCertificate = new();
        crypto.InitializeNew(withoutCertificate, "synthetic-password".ToCharArray().ToSecureStringAndClear());
        DocumentSignatureService service = new(crypto);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(withoutCertificate, PathFor("data"), PathFor("signature")));
            await File.WriteAllBytesAsync(PathFor("empty"), []);
            ProjectDocument signingDocument = CreateDocumentWithCertificate();
            try { await Assert.ThrowsAsync<CryptographicException>(() => service.CreateAsync(signingDocument, PathFor("empty"), PathFor("empty-signature"))); }
            finally { signingDocument.Security.CaCertificate?.Dispose(); crypto.ClearAll(signingDocument); }
            await using (FileStream large = new(PathFor("large"), FileMode.CreateNew, FileAccess.Write)) large.SetLength(DocumentSignatureService.MaximumInputBytes + 1);
            ProjectDocument document = CreateDocumentWithCertificate();
            try
            {
                await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(document, PathFor("large"), PathFor("large-signature")));
                await Assert.ThrowsAsync<IOException>(() => service.VerifyAsync(PathFor("large"), PathFor("data")));
            }
            finally { document.Security.CaCertificate?.Dispose(); crypto.ClearAll(document); }
        }
        finally { crypto.ClearAll(withoutCertificate); }
    }

    private ProjectDocument CreateDocumentWithCertificate()
    {
        ProjectDocument document = new();
        crypto.InitializeNew(document, "synthetic-password".ToCharArray().ToSecureStringAndClear());
        crypto.EnsureCaCertificate(document);
        return document;
    }
}
