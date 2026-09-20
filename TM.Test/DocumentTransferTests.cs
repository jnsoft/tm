using System.Security.Cryptography;
using TM.Models;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class DocumentTransferTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.TransferTests.{Guid.NewGuid():N}");
    private readonly ProjectCryptoService crypto = new();
    private readonly DocumentTransferService transfers;

    public DocumentTransferTests() => transfers = new(crypto);

    [TestInitialize]
    public void Initialize() => Directory.CreateDirectory(directory);

    [TestCleanup]
    public void Cleanup() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public async Task Transfer_InteroperatesWithLegacySavFormatAsync()
    {
        ProjectDocument source = TestDataBuilder.CreateLoadedEncryptedDocument(crypto, "source-password");
        string exported = Path.Combine(directory, "exported.sav");
        byte[] key = await transfers.ExportAsync(source, SecurePassword("source-password"), exported);
        try
        {
            byte[] payload = SecurityHelper.GCMDecrypt((await File.ReadAllTextAsync(exported)).FromBase64(), key);
            XmlDocument legacyXml = new();
            legacyXml.LoadXml(payload.ToStringFromByte());
            Assert.AreEqual("project_store", legacyXml.DocumentElement?.Name);
            Assert.IsTrue(legacyXml.SelectSingleNode("/project_store/projects/project") is not null);
            CryptographicOperations.ZeroMemory(payload);

            ProjectDocument imported = await transfers.ImportAsync(exported, SecurePassword("import-password"), key);
            Assert.IsFalse(imported.IsEmpty);
            Assert.IsFalse(imported.IsDiffieHellmanEnabled);
            Assert.IsFalse(imported.IsPkiEnabled);
            Assert.IsTrue(imported.Nodes.SelectMany(node => node.AllChildNodesFlat)
                .Where(node => node.IsProtected)
                .Select(node => crypto.DecryptSecret(imported, node.Password))
                .Contains("password1", StringComparer.Ordinal));

            byte[] legacyKey = SecurityHelper.GetRandomKey(DocumentTransferService.TransferKeyLength);
            string legacy = Path.Combine(directory, "legacy.sav");
            try
            {
                XmlDocument transferXml = crypto.GetUnencryptedXml(source, SecurePassword("source-password"));
                await File.WriteAllTextAsync(legacy, SecurityHelper.GCMEncrypt(transferXml.InnerXml.ToByte(), legacyKey).ToBase64());
                ProjectDocument legacyImported = await transfers.ImportAsync(legacy, SecurePassword("new-password"), legacyKey);
                Assert.AreEqual("Top node 1", legacyImported.Nodes.Single().Text);
            }
            finally { CryptographicOperations.ZeroMemory(legacyKey); }
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    [TestMethod]
    public async Task RejectedTransfers_PreserveOutputAndRejectUnsafePayloadsAsync()
    {
        ProjectDocument source = TestDataBuilder.CreateLoadedEncryptedDocument(crypto, "source-password");
        string existing = Path.Combine(directory, "existing.sav");
        await File.WriteAllTextAsync(existing, "retain");
        await Assert.ThrowsAsync<IOException>(() => transfers.ExportAsync(source, SecurePassword("source-password"), existing));
        Assert.AreEqual("retain", await File.ReadAllTextAsync(existing));
        await Assert.ThrowsAsync<CryptographicException>(() => transfers.ExportAsync(source, SecurePassword("wrong-password"), Path.Combine(directory, "wrong.sav")));

        string malformed = Path.Combine(directory, "malformed.sav");
        await File.WriteAllTextAsync(malformed, "not-base64");
        await Assert.ThrowsAsync<FormatException>(() => transfers.ImportAsync(malformed, SecurePassword("new-password"), new byte[32]));

        byte[] key = SecurityHelper.GetRandomKey(32);
        try
        {
            string dtd = Path.Combine(directory, "dtd.sav");
            await File.WriteAllTextAsync(dtd, SecurityHelper.GCMEncrypt("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///ignored'>]><project_store><projects><project>&e;</project></projects></project_store>".ToByte(), key).ToBase64());
            await Assert.ThrowsAsync<XmlException>(() => transfers.ImportAsync(dtd, SecurePassword("new-password"), key));
            await Assert.ThrowsAsync<CryptographicException>(() => transfers.ImportAsync(dtd, SecurePassword("new-password"), new byte[32]));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static SecureString SecurePassword(string value) => value.ToCharArray().ToSecureStringAndClear();
}
