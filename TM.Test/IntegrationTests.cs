using TM.Services;

namespace TM.Test;

[TestClass]
public class IntegrationTests
{
    private static readonly ProjectCryptoService Crypto = new();

    [TestMethod]
    public void SaveAndOpenEncryptedXml_RoundTripsDocument()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xml");

        try
        {
            ProjectDocument model = TestDataBuilder.CreateLoadedEncryptedDocument(Crypto);
            Crypto.GenerateKeys(model);
            Crypto.EnsureCaCertificate(model);

            XMLhelper.XmlToFile(Crypto.GetAsEncryptedXml(model).DocumentElement, filePath);
            Crypto.ClearAll(model);

            XmlDocument doc = XMLhelper.XmlFromFile(filePath);
            ProjectDocument loaded = new();
            Crypto.LoadEncryptedDocument(loaded, doc, "secret".ToCharArray().ToSecureStringAndClear());

            Assert.IsFalse(loaded.IsEmpty);
            Assert.IsTrue(loaded.IsDiffieHellmanEnabled);
            Assert.IsTrue(loaded.Security.IsPkiEnabled);
            Assert.HasCount(1, loaded.Nodes);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [TestMethod]
    public void SaveAndOpenUnencryptedPayload_RoundTripsDocument()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sav");

        try
        {
            ProjectDocument model = TestDataBuilder.CreateLoadedEncryptedDocument(Crypto);

            XmlDocument unencrypted = Crypto.GetUnencryptedXml(model, "secret".ToCharArray().ToSecureStringAndClear());
            byte[] key = SecurityHelper.GetRandomKey(32);
            string fileContent = SecurityHelper.GCMEncrypt(unencrypted.InnerXml.ToByte(), key).ToBase64();
            File.WriteAllText(filePath, fileContent);

            byte[] decryptedBytes = SecurityHelper.GCMDecrypt(
                File.ReadAllText(filePath).FromBase64(),
                key);

            XmlDocument reloadedXml = new();
            reloadedXml.LoadXml(decryptedBytes.ToStringFromByte());

            ProjectDocument loaded = new();
            Crypto.InitializeNew(loaded, "secret".ToCharArray().ToSecureStringAndClear());
            loaded.LoadUnencrypted(reloadedXml);
            Crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(loaded);

            Assert.IsFalse(loaded.IsEmpty);
            Assert.HasCount(model.Nodes.Count, loaded.Nodes);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
