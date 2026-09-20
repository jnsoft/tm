using System.Security.Cryptography;
using System.Xml.Linq;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class EncryptedDocumentCompatibilityTests
{
    private const string Password = "baseline-test-only";
    private readonly ProjectCryptoService crypto = new();
    private readonly List<ProjectDocument> documents = [];

    [TestCleanup]
    public void ClearDocuments()
    {
        foreach (ProjectDocument document in documents)
        {
            crypto.ClearAll(document);
            document.Security.CaCertificate?.Dispose();
        }
    }

    [TestMethod]
    public void FrozenWpfDocument_LoadsAllFieldsSecretsKeysAndCertificate()
    {
        ProjectDocument document = LoadFixture();
        AssertExpectedProjects(document, Password);
        Assert.IsTrue(document.IsFileLoaded);
        Assert.IsFalse(document.IsLocked);
        AssertKeyAndCertificate(document);
    }

    [TestMethod]
    public void FrozenWpfDocument_SaveReload_PreservesFieldsAndKeyStores()
    {
        ProjectDocument document = LoadFixture();
        XmlDocument saved = crypto.GetAsEncryptedXml(document);
        ProjectDocument reloaded = NewDocument();
        crypto.LoadEncryptedDocument(reloaded, saved, SecurePassword(Password));
        AssertExpectedProjects(reloaded, Password);
        AssertKeyAndCertificate(reloaded);
    }

    [TestMethod]
    public void FrozenWpfDocument_WrongPassword_IsRejected()
    {
        ProjectDocument document = NewDocument();
        AssertPasswordRejected(() =>
            crypto.LoadEncryptedDocument(document, ReadXml("baseline-encrypted.xml"), SecurePassword("incorrect")));
        Assert.IsFalse(document.IsFileLoaded);
        Assert.IsTrue(document.IsEmpty);
    }

    [TestMethod]
    public void FrozenWpfDocument_ChangePassword_PreservesDataAndRejectsOldPassword()
    {
        const string newPassword = "replacement-test-only";
        ProjectDocument document = LoadFixture();
        Assert.IsTrue(crypto.ChangeMasterPassword(document, SecurePassword(Password), SecurePassword(newPassword)));
        XmlDocument saved = crypto.GetAsEncryptedXml(document);
        ProjectDocument reloaded = NewDocument();
        crypto.LoadEncryptedDocument(reloaded, (XmlDocument)saved.CloneNode(true), SecurePassword(newPassword));
        AssertExpectedProjects(reloaded, newPassword);
        AssertKeyAndCertificate(reloaded);
        AssertPasswordRejected(() =>
            crypto.LoadEncryptedDocument(NewDocument(), saved, SecurePassword(Password)));
    }

    [TestMethod]
    public void FrozenWpfDocument_TamperedPrivateKey_IsRejected()
    {
        XmlDocument xml = ReadXml("baseline-encrypted.xml");
        XmlNode? key = xml.SelectSingleNode("/project_store/key_store/private_key");
        Assert.IsNotNull(key);
        byte[] ciphertext = Convert.FromBase64String(key.InnerText);
        ciphertext[^1] ^= 1;
        key.InnerText = Convert.ToBase64String(ciphertext);
        Assert.Throws<CryptographicException>(() =>
            crypto.LoadEncryptedDocument(NewDocument(), xml, SecurePassword(Password)));
    }

    [TestMethod]
    public void FrozenWpfDocument_TamperedProtectedValue_IsRejected()
    {
        ProjectDocument document = LoadFixture();
        NodeModel node = TestDataBuilder.GetFirstProtectedNode(document);
        byte[] ciphertext = Convert.FromBase64String(node.Password);
        ciphertext[^1] ^= 1;
        Assert.Throws<CryptographicException>(() => crypto.DecryptSecret(document, Convert.ToBase64String(ciphertext)));
    }

    [TestMethod]
    public void FrozenWpfDocument_Save_UsesFreshEncryptionAndHidesProjectData()
    {
        ProjectDocument document = LoadFixture();
        string first = crypto.GetAsEncryptedXml(document).OuterXml;
        string second = crypto.GetAsEncryptedXml(document).OuterXml;
        Assert.AreNotEqual(first, second);
        foreach (string plain in new[] { "Top node 1", "user1", "password1", "password6" })
            Assert.IsFalse(first.Contains(plain, StringComparison.Ordinal), "Saved document exposed synthetic plaintext.");
    }

    [TestMethod]
    public void FrozenWpfDocument_Lock_ClearsMasterAndEcdhKeyBuffers()
    {
        ProjectDocument document = LoadFixture();
        byte[]? master = document.Security.ProtectedMasterKey;
        byte[]? privateKey = document.Security.ProtectedPrivateKey;
        Assert.IsNotNull(master);
        Assert.IsNotNull(privateKey);
        crypto.Lock(document);
        Assert.IsTrue(document.IsLocked);
        Assert.IsNull(document.Security.ProtectedMasterKey);
        Assert.IsNull(document.Security.ProtectedPrivateKey);
        Assert.IsNull(document.Security.PublicKey);
        Assert.IsTrue(master.All(value => value == 0));
        Assert.IsTrue(privateKey.All(value => value == 0));
        Assert.Throws<InvalidOperationException>(() =>
            crypto.DecryptSecret(document, TestDataBuilder.GetFirstProtectedNode(document).Password));
        // Certificate disposal is a separately documented legacy security concern.
    }

    [TestMethod]
    public void UnicodeAndXmlCharacters_SaveReload_PreservesTextAndProtectedValue()
    {
        ProjectDocument document = LoadFixture();
        NodeModel node = TestDataBuilder.GetFirstProtectedNode(document);
        const string text = "Åäö 日本語 <node> & \"quoted\"";
        node.Text = text;
        node.Description = "First line\nSecond line: " + text;
        node.Password = crypto.EncryptSecret(document, text);
        ProjectDocument reloaded = NewDocument();
        crypto.LoadEncryptedDocument(reloaded, crypto.GetAsEncryptedXml(document), SecurePassword(Password));
        NodeModel? restored = reloaded.GetNodeById(node.Id);
        Assert.IsNotNull(restored);
        Assert.AreEqual(text, restored.Text);
        Assert.AreEqual(node.Description, restored.Description);
        Assert.AreEqual(text, crypto.DecryptSecret(reloaded, restored.Password));
    }

    private ProjectDocument NewDocument()
    {
        ProjectDocument document = new();
        documents.Add(document);
        return document;
    }

    private ProjectDocument LoadFixture()
    {
        ProjectDocument document = NewDocument();
        crypto.LoadEncryptedDocument(document, ReadXml("baseline-encrypted.xml"), SecurePassword(Password));
        return document;
    }

    private void AssertExpectedProjects(ProjectDocument document, string password)
    {
        XmlDocument expected = ReadXml("baseline-expected.xml");
        XmlDocument actual = crypto.GetUnencryptedXml(document, SecurePassword(password));
        XmlNode? expectedProjects = expected.SelectSingleNode("/project_store/projects");
        XmlNode? actualProjects = actual.SelectSingleNode("/project_store/projects");
        Assert.IsNotNull(expectedProjects);
        Assert.IsNotNull(actualProjects);
        Assert.IsTrue(XNode.DeepEquals(XElement.Parse(expectedProjects.OuterXml), XElement.Parse(actualProjects.OuterXml)),
            "Project fields, hierarchy, IDs, dates or protected plaintext changed from the frozen WPF baseline.");
    }

    private void AssertKeyAndCertificate(ProjectDocument document)
    {
        XmlDocument fixture = ReadXml("baseline-encrypted.xml");
        Assert.AreEqual(fixture.SelectSingleNode("/project_store/key_store/public_key")?.InnerText, document.Security.PublicKey);
        Assert.IsNotNull(document.Security.CaCertificate);
        Assert.IsTrue(document.Security.CaCertificate.HasPrivateKey);
        Assert.AreEqual(fixture.SelectSingleNode("/project_store/cert_store/cert_id")?.InnerText,
            document.Security.CaCertificate.GetSerialNumberString());
        byte[]? privateKey = crypto.GetUnprotectedPrivateKey(document);
        Assert.IsNotNull(privateKey);
        try
        {
            Assert.AreEqual(ReadResource("baseline-private-key.sha256").Trim(), Convert.ToHexString(SHA256.HashData(privateKey)));
        }
        finally { CryptographicOperations.ZeroMemory(privateKey); }
    }

    private static void AssertPasswordRejected(Action load)
    {
        // Legacy XML encryption can fail during padding validation or XML parsing.
        // Do not accept arbitrary exceptions as evidence of a correct rejection.
        try
        {
            load();
        }
        catch (Exception exception) when (exception is CryptographicException or XmlException)
        {
            return;
        }

        Assert.Fail("An incorrect password was accepted.");
    }

    private static SecureString SecurePassword(string value) => value.ToCharArray().ToSecureStringAndClear();

    private static XmlDocument ReadXml(string name)
    {
        XmlDocument document = new() { XmlResolver = null };
        document.LoadXml(ReadResource(name));
        return document;
    }

    private static string ReadResource(string name)
    {
        using Stream stream = typeof(EncryptedDocumentCompatibilityTests).Assembly
            .GetManifestResourceStream($"TM.Test.Fixtures.{name}")
            ?? throw new InvalidOperationException($"Missing fixture {name}.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
