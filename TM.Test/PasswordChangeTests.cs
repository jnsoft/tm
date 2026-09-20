using System.Security.Cryptography;
using TM.Services;
using Project = TM.Entities.Project;
using ProtectedItem = TM.Entities.ProtectedItem;

namespace TM.Test;

[TestClass]
public sealed class PasswordChangeTests
{
    private readonly ProjectCryptoService crypto = new();

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ChangePassword_WithoutProtectedItems_AuthenticatesAndRoundTrips(bool includeProject)
    {
        ProjectDocument document = new();
        ProjectDocument restored = new();
        ProjectDocument rejected = new();
        try
        {
            crypto.InitializeNew(document, Password("old-test-password"));
            if (includeProject) document.AddNode(new(new Project("Empty project")));
            byte[] oldSalt = document.Security.Salt!;
            byte[] oldKey = document.Security.ProtectedMasterKey!;
            var originalNodes = document.Nodes;
            Assert.Throws<CryptographicException>(() => crypto.ChangeMasterPassword(document,
                Password("wrong-password"), Password("new-test-password")));
            Assert.AreSame(oldSalt, document.Security.Salt);
            Assert.AreSame(oldKey, document.Security.ProtectedMasterKey);
            Assert.AreSame(originalNodes, document.Nodes);
            Assert.IsTrue(crypto.ChangeMasterPassword(document, Password("old-test-password"), Password("new-test-password")));
            Assert.IsTrue(oldKey.All(value => value == 0));
            CollectionAssert.AreNotEqual(oldSalt, document.Security.Salt!);
            XmlDocument saved = crypto.GetAsEncryptedXml(document);
            crypto.LoadEncryptedDocument(restored, (XmlDocument)saved.CloneNode(true), Password("new-test-password"));
            Assert.AreEqual(includeProject ? 1 : 0, restored.Nodes.Count);
            try
            {
                crypto.LoadEncryptedDocument(rejected, saved, Password("old-test-password"));
                Assert.Fail("Old password was accepted after rekey.");
            }
            catch (Exception error) when (error is CryptographicException or XmlException) { }
        }
        finally
        {
            crypto.ClearAll(document);
            crypto.ClearAll(restored);
            crypto.ClearAll(rejected);
        }
    }

    [TestMethod]
    public void ChangePassword_DamagedLaterEntry_DoesNotPartiallyRekey()
    {
        ProjectDocument document = new();
        try
        {
            crypto.InitializeNew(document, Password("old-test-password"));
            Project project = new("Root");
            project.ProtectedItems.Add(new ProtectedItem("A First") { Password = crypto.EncryptSecret(document, "first-secret") });
            byte[] damaged = Convert.FromBase64String(crypto.EncryptSecret(document, "second-secret"));
            damaged[^1] ^= 1;
            project.ProtectedItems.Add(new ProtectedItem("Z Damaged") { Password = Convert.ToBase64String(damaged) });
            document.LoadProjects([project]);
            var nodes = document.Nodes;
            byte[] salt = document.Security.Salt!;
            byte[] key = document.Security.ProtectedMasterKey!;
            byte[] keyCopy = [.. key];
            string[] encrypted = [.. nodes[0].Nodes.Select(node => node.Password)];
            Assert.Throws<CryptographicException>(() => crypto.ChangeMasterPassword(document,
                Password("old-test-password"), Password("new-test-password")));
            Assert.AreSame(nodes, document.Nodes);
            Assert.AreSame(salt, document.Security.Salt);
            Assert.AreSame(key, document.Security.ProtectedMasterKey);
            CollectionAssert.AreEqual(keyCopy, key);
            CollectionAssert.AreEqual(encrypted, nodes[0].Nodes.Select(node => node.Password).ToArray());
            Assert.AreEqual("first-secret", crypto.DecryptSecret(document, nodes[0].Nodes[0].Password));
        }
        finally { crypto.ClearAll(document); }
    }

    [TestMethod]
    public void LoadEncryptedDocument_MissingPayload_IsNotAnEmptyCollection()
    {
        ProjectDocument source = new();
        ProjectDocument target = new();
        try
        {
            crypto.InitializeNew(source, Password("test-password"));
            XmlDocument xml = crypto.GetAsEncryptedXml(source);
            XmlNode encrypted = xml.GetElementsByTagName("EncryptedData", "http://www.w3.org/2001/04/xmlenc#")[0]!;
            encrypted.ParentNode!.RemoveChild(encrypted);
            Assert.Throws<CryptographicException>(() => crypto.LoadEncryptedDocument(target, xml, Password("test-password")));
            Assert.IsFalse(target.IsFileLoaded);
        }
        finally
        {
            crypto.ClearAll(source);
            crypto.ClearAll(target);
        }
    }

    private static SecureString Password(string value) => value.ToCharArray().ToSecureStringAndClear();
}
