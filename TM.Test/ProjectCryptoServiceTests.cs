using System;
using System.Collections.Generic;
using System.Text;
using TM.Entities;
using TM.Services;

namespace TM.Test;

[TestClass]
public class ProjectCryptoServiceTests
{
    private static readonly ProjectCryptoService Crypto = new();

    [TestMethod]
    public void TestDeriveKey()
    {
        string password = "secret";
        string password2 = new("secret".ToCharArray());
        ProjectDocument model = new();
        Crypto.InitializeNew(model, password.ToSecureString());

        byte[] masterKey = SecurityHelper.GetKeyFromPassword(
            password2.ToSecureString(),
            model.Security.Salt,
            32,
            ProjectCryptoService.Pbkdf2Iterations);

        byte[] derivedKeySalt = SecurityHelper.GetRandomKey(ProjectCryptoService.SaltLength);

        byte[] key1 = Crypto.DeriveKey(model, "test", derivedKeySalt);
        byte[] key2 = Crypto.DeriveKey(masterKey, "test", derivedKeySalt);

        CollectionAssert.AreEqual(key1, key2);
    }

    [TestMethod]
    public void TestEncryptAndDecryptSecret()
    {
        SecureString pass = "secret".ToSecureString();
        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass);
        string secret = "secret";

        string encrypted = Crypto.EncryptSecret(model, secret);
        string decrypted = Crypto.DecryptSecret(model, encrypted);

        Assert.AreEqual(secret, decrypted);
        Assert.AreNotEqual(secret, encrypted);
    }

    [TestMethod]
    public void TestReEncryptSecret()
    {
        string secret = "secret";
        string secret2 = new("secret".ToCharArray());
        string secret3 = new("secret".ToCharArray());
        string secret4 = new("secret".ToCharArray());

        SecureString oldPass = secret.ToSecureString();
        SecureString oldPass2 = secret2.ToSecureString();
        ProjectDocument model = new();
        Crypto.InitializeNew(model, oldPass);

        byte[] oldKey = SecurityHelper.GetKeyFromPassword(
            oldPass2,
            model.Security.Salt,
            32,
            ProjectCryptoService.Pbkdf2Iterations);

        SecureString newPass = secret3.ToSecureString();
        byte[] newSalt = SecurityHelper.GetRandomKey(ProjectCryptoService.SaltLength);
        byte[] newKey = SecurityHelper.GetKeyFromPassword(
            newPass,
            newSalt,
            32,
            ProjectCryptoService.Pbkdf2Iterations);

        string encryptedSecret = Crypto.EncryptSecret(model, secret4);
        string reencrypted = Crypto.ReencryptSecret(encryptedSecret, oldKey, newKey);
        string rereencrypted = Crypto.ReencryptSecret(reencrypted, newKey, oldKey);
        string decrypted = Crypto.DecryptSecret(model, rereencrypted);

        Assert.AreEqual(secret4, decrypted);
    }

    [TestMethod]
    public void TestChangePassword()
    {
        string pass = new("secret".ToCharArray());
        string pass2 = new("secret".ToCharArray());
        string pass4 = new("secret2".ToCharArray());
        string pass5 = new("secret".ToCharArray());
        string pass6 = new("secret2".ToCharArray());

        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());
        List<Project> projects = TestDataBuilder.CreateSampleProjects();
        model.LoadProjects(projects);
        Crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(model);
        projects = model.GetProjects();

        byte[] originalSalt = model.Security.Salt ?? [];
        Crypto.ChangeMasterPassword(model, pass2.ToSecureString(), pass4.ToSecureString());
        List<Project> projects2 = model.GetProjects();
        byte[] newSalt = model.Security.Salt ?? [];

        Crypto.ChangeMasterPassword(model, pass6.ToSecureString(), pass5.ToSecureString());
        List<Project> projects3 = model.GetProjects();
        byte[] newerSalt = model.Security.Salt ?? [];

        Assert.AreNotEqual(projects[0].GetHashCode(), projects2[0].GetHashCode());
        Assert.AreNotEqual(projects[0].GetHashCode(), projects3[0].GetHashCode());

        Assert.AreNotEqual(projects[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password, projects2[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password);
        Assert.AreNotEqual(projects[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password, projects3[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password);
        Assert.AreNotEqual(projects2[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password, projects3[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password);

        Assert.AreEqual(projects[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID, projects2[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID);
        Assert.AreEqual(projects[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID, projects3[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID);
        Assert.AreEqual(projects2[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID, projects3[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID);

        CollectionAssert.AreNotEqual(projects, projects2);
        CollectionAssert.AreNotEqual(projects, projects3);
        CollectionAssert.AreNotEqual(projects2, projects3);

        CollectionAssert.AreNotEqual(originalSalt, newSalt);
        CollectionAssert.AreNotEqual(newSalt, newerSalt);
        CollectionAssert.AreNotEqual(originalSalt, newerSalt);
    }

    [TestMethod]
    public void TestSaveLoadProjects()
    {
        string pass = new("secret".ToCharArray());
        string pass2 = new("secret".ToCharArray());

        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());
        model.LoadProjects(TestDataBuilder.CreateSampleProjects());
        Crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(model);

        XmlDocument doc = Crypto.GetAsEncryptedXml(model);
        ProjectDocument model2 = new();
        Crypto.LoadEncryptedDocument(model2, doc, pass2.ToSecureString());

        List<Project> projects = model.GetProjects();
        List<Project> projects2 = model2.GetProjects();

        Assert.AreEqual(projects[0].GetHashCode(), projects2[0].GetHashCode());
        CollectionAssert.AreEqual(projects, projects2);
    }

    [TestMethod]
    public void TestSaveLoad()
    {
        string pass = new("secret".ToCharArray());
        string pass2 = new("secret".ToCharArray());

        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());
        model.LoadProjects(TestDataBuilder.CreateSampleProjects());
        Crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(model);
        Crypto.GenerateKeys(model);

        XmlDocument doc = Crypto.GetAsEncryptedXml(model);
        ProjectDocument model2 = new();
        Crypto.LoadEncryptedDocument(model2, doc, pass2.ToSecureString());

        List<Project> projects = model.GetProjects();
        List<Project> projects2 = model2.GetProjects();
        byte[] key1 = Crypto.GetUnprotectedPrivateKey(model) ?? [];
        byte[] key2 = Crypto.GetUnprotectedPrivateKey(model2) ?? [];

        Assert.AreEqual(projects[0].GetHashCode(), projects2[0].GetHashCode());
        CollectionAssert.AreEqual(projects, projects2);
        CollectionAssert.AreEqual(key1, key2);
    }

    [TestMethod]
    public void TestSaveUnenencrypted()
    {
        string pass = new("secret".ToCharArray());
        string pass2 = new("secret".ToCharArray());
        string pass3 = new("secret2".ToCharArray());

        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());
        List<Project> projects = TestDataBuilder.CreateSampleProjects();
        model.LoadProjects(projects);
        Crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(model);

        XmlDocument doc = Crypto.GetUnencryptedXml(model, pass2.ToSecureString());
        bool saveWithWrongPassword = true;

        try
        {
            _ = Crypto.GetUnencryptedXml(model, pass3.ToSecureString());
        }
        catch
        {
            saveWithWrongPassword = false;
        }

        Assert.IsNotNull(doc.DocumentElement);
        Assert.IsGreaterThan(0, doc.DocumentElement.OuterXml.Length);
        Assert.IsFalse(saveWithWrongPassword);
    }

    [TestMethod]
    public void TestGenerateNewPKIpair()
    {
        string pass = "secret";
        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());

        Crypto.GenerateKeys(model);
        string pubkey1 = model.Security.PublicKey;
        byte[] privkey1 = Crypto.GetUnprotectedPrivateKey(model);
        Crypto.GenerateKeys(model);
        string pubkey2 = model.Security.PublicKey;
        byte[] privkey2 = Crypto.GetUnprotectedPrivateKey(model);

        Assert.IsFalse(string.IsNullOrWhiteSpace(pubkey1));
        Assert.IsNotNull(privkey1);
        Assert.IsFalse(string.IsNullOrWhiteSpace(pubkey2));
        Assert.IsNotNull(privkey2);
        Assert.AreNotEqual(pubkey1, pubkey2);
        CollectionAssert.AreNotEqual(privkey1, privkey2);
    }

    [TestMethod]
    public void TestGetKeyStoreXML()
    {
        string pass = "secret";
        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());
        Crypto.GenerateKeys(model);

        XmlDocument keyStore = Crypto.GetKeyStoreXml(model);
        string publicKey = XMLhelper.GetInnerTextFromChild(keyStore, "public_key");
        byte[] salt = XMLhelper.GetInnerTextFromChild(keyStore, "keystore_salt").FromBase64();
        byte[] privateKey = XMLhelper.GetInnerTextFromChild(keyStore, "private_key").FromBase64();

        Assert.IsFalse(string.IsNullOrWhiteSpace(publicKey));
        Assert.IsNotNull(salt);
        Assert.AreEqual(ProjectCryptoService.SaltLength, salt.Length);
        Assert.IsNotNull(privateKey);
        Assert.IsTrue(privateKey.Length > 0);
    }

    [TestMethod]
    public void TestLoadKeyStore()
    {
        string pass = new("secret".ToCharArray());
        string pass2 = new("secret".ToCharArray());

        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());
        Crypto.GenerateKeys(model);

        string pubkey1 = model.Security.PublicKey;
        byte[] privkey1 = Crypto.GetUnprotectedPrivateKey(model);
        byte[] salt = model.Security.Salt;
        XmlDocument keyStore = Crypto.GetKeyStoreXml(model);

        ProjectDocument model2 = new();
        Crypto.SetMasterKey(model2, pass2.ToSecureString(), salt);

        Crypto.LoadKeyStore(model2, keyStore.DocumentElement);

        string pubkey2 = model2.Security.PublicKey;
        byte[] privkey2 = Crypto.GetUnprotectedPrivateKey(model2);

        Assert.IsFalse(string.IsNullOrWhiteSpace(pubkey1));
        Assert.IsNotNull(privkey1);
        Assert.IsFalse(string.IsNullOrWhiteSpace(pubkey2));
        Assert.IsNotNull(privkey2);
        Assert.AreEqual(pubkey1, pubkey2);
        CollectionAssert.AreEqual(privkey1, privkey2);
    }

    [TestMethod]
    public void TestGenerateCert()
    {
        string pass = new("secret".ToCharArray());
        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());

        Crypto.EnsureCaCertificate(model);

        Assert.IsNotNull(model.Security.CaCertificate);
        Assert.IsTrue(model.Security.CaCertificate.HasPrivateKey);
    }

    [TestMethod]
    public void TestGetCertStoreXML()
    {
        string pass = "secret";
        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());
        Crypto.EnsureCaCertificate(model);

        XmlDocument certStore = Crypto.GetCertStoreXml(model);
        string id = XMLhelper.GetInnerTextFromChild(certStore, "cert_id");
        byte[] salt = XMLhelper.GetInnerTextFromChild(certStore, "certstore_salt").FromBase64();
        byte[] pfxEnc = XMLhelper.GetInnerTextFromChild(certStore, "CDATA").FromBase64();

        Assert.IsFalse(string.IsNullOrWhiteSpace(id));
        Assert.IsNotNull(salt);
        Assert.AreEqual(ProjectCryptoService.SaltLength, salt.Length);
        Assert.IsNotNull(pfxEnc);
        Assert.IsTrue(pfxEnc.Length > 0);
    }

    [TestMethod]
    public void TestLoadCertStore()
    {
        string pass = new("secret".ToCharArray());
        string pass2 = new("secret".ToCharArray());

        ProjectDocument model = new();
        Crypto.InitializeNew(model, pass.ToSecureString());
        Crypto.EnsureCaCertificate(model);

        string id1 = model.Security.CaCertificate.GetSerialNumberString();
        bool hasPrivateKey1 = model.Security.CaCertificate.HasPrivateKey;
        XmlDocument certStore = Crypto.GetCertStoreXml(model);

        ProjectDocument model2 = new();
        Crypto.SetMasterKey(model2, pass2.ToSecureString(), model.Security.Salt);

        Crypto.LoadCertStore(model2, certStore.DocumentElement);

        string id2 = model2.Security.CaCertificate.GetSerialNumberString();
        bool hasPrivateKey2 = model2.Security.CaCertificate.HasPrivateKey;

        Assert.IsFalse(string.IsNullOrWhiteSpace(id1));
        Assert.IsFalse(string.IsNullOrWhiteSpace(id2));
        Assert.IsTrue(hasPrivateKey1);
        Assert.IsTrue(hasPrivateKey2);
        Assert.AreEqual(id1, id2);
    }
}
