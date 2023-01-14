using TM.Entities;
using Task = TM.Entities.Task;

namespace TM.Test;

[TestClass]
public class MainWindowModelTests
{
    [TestMethod]
    public void TestDeriveKey()
    {
        // Arrange
        string password = "secret";
        string password2 = new string("secret"); // all variables reference the same string object because literal strings are added to the string pool, new forces a new instance
        MainWindowModel model = new MainWindowModel(password.ToSecureString());
        byte[] masterkey = SecurityHelper.GetKeyFromPassword(password2.ToSecureString(), model.Salt, 32, MainWindowModel.PBKDF2_ITERATIONS);
        byte[] derivedKeySalt = SecurityHelper.GetRandomKey(MainWindowModel.SALT_LEN);

        // Act
        byte[] key1 = model.DeriveKey("test", derivedKeySalt);
        byte[] key2 = model.DeriveKey(masterkey, "test", derivedKeySalt);

        // Assert
        CollectionAssert.AreEqual(key1, key2);
    }

    [TestMethod]
    public void TestEncryptAndDecryptSecret()
    {
        // Arrange
        SecureString pass = "secret".ToSecureString();
        MainWindowModel model = new MainWindowModel(pass);
        string secret = "secret";

        // Act
        string encrypted = model.EncryptSecret(secret);
        string decrypted = model.DecryptSecret(encrypted);

        // Assert
        Assert.AreEqual(secret, decrypted);
        Assert.AreNotEqual(secret, encrypted);
    }

    [TestMethod]
    public void TestReEncryptSecret()
    {
        // Arrange
        string secret = "secret";
        string secret2 = new string("secret"); // force new instance
        string secret3 = new string("secret"); // force new instance
        string secret4 = new string("secret"); // force new instance

        SecureString oldPass = secret.ToSecureString();
        SecureString oldPass2 = secret2.ToSecureString();
        MainWindowModel model = new MainWindowModel(oldPass); // new string to force a new instance
        byte[] oldKey = SecurityHelper.GetKeyFromPassword(oldPass2, model.Salt, 32, MainWindowModel.PBKDF2_ITERATIONS);

        SecureString newPass = secret3.ToSecureString();
        byte[] newSalt = SecurityHelper.GetRandomKey(MainWindowModel.SALT_LEN);
        byte[] newKey = SecurityHelper.GetKeyFromPassword(newPass, newSalt, 32, MainWindowModel.PBKDF2_ITERATIONS);

        // Act
        string encryptedSecret = model.EncryptSecret(secret4);
        string reencrypted = model.ReencryptSecret(encryptedSecret, oldKey, newKey);
        string rereencrypted = model.ReencryptSecret(reencrypted, newKey, oldKey);
        string decrypted = model.DecryptSecret(rereencrypted);

        // Assert
        Assert.AreEqual(secret4, decrypted);
    }

    [TestMethod]
    public void TestSetGetProjects()
    {
        // Arrange
        string pass1 = "secret";
        MainWindowModel model = new MainWindowModel(pass1.ToSecureString());
        List<Project> ps = getSampleProjects();

        // Act
        model.LoadProjects(ps);
        List<Project> ps2 = model.GetProjects();

        // Assert
        Assert.AreEqual(ps[0].GetHashCode(), ps2[0].GetHashCode());
        CollectionAssert.AreEqual(ps, ps2);
    }

    [TestMethod]
    public void TestChangePassword()
    {
        // Arrange
        string pass = new string("secret"); // force new instance
        string pass2 = new string("secret"); // force new instance
        string pass4 = new string("secret2"); // force new instance
        string pass5 = new string("secret"); // force new instance
        string pass6 = new string("secret2"); // force new instance

        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        List<Project> ps = getSampleProjects();
        model.LoadProjects(ps);
        model.EncryptProtectedItemsAfterLoadingUnencryptedProjects();
        ps = model.GetProjects();

        // Act
        byte[] orgSalt = model.Salt;
        model.ChangeMasterPassword(pass2.ToSecureString(), pass4.ToSecureString());
        List<Project> ps2 = model.GetProjects();
        byte[] newSalt = model.Salt;

        model.ChangeMasterPassword(pass6.ToSecureString(), pass5.ToSecureString());
        List<Project> ps3 = model.GetProjects();
        byte[] newnewSalt = model.Salt;


        // Assert
        Assert.AreNotEqual(ps[0].GetHashCode(), ps2[0].GetHashCode());
        Assert.AreNotEqual(ps[0].GetHashCode(), ps3[0].GetHashCode());

        Assert.AreNotEqual(ps[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password, ps2[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password);
        Assert.AreNotEqual(ps[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password, ps3[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password);
        Assert.AreNotEqual(ps2[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password, ps3[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].Password);

        Assert.AreEqual(ps[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID, ps2[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID);
        Assert.AreEqual(ps[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID, ps3[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID);
        Assert.AreEqual(ps2[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID, ps3[0].Milestones[0].Tasks[0].SubTasks[0].ProtectedItems[0].UUID);

        CollectionAssert.AreNotEqual(ps, ps2);
        CollectionAssert.AreNotEqual(ps, ps3);
        CollectionAssert.AreNotEqual(ps2, ps3);

        CollectionAssert.AreNotEqual(orgSalt, newSalt);
        CollectionAssert.AreNotEqual(newSalt, newnewSalt);
        CollectionAssert.AreNotEqual(orgSalt, newnewSalt);
    }

    [TestMethod]
    public void TestSaveLoadProjects()
    {
        // Arrange
        string pass = new string("secret"); // force new instance
        string pass2 = new string("secret"); // force new instance

        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        model.LoadProjects(getSampleProjects());
        model.EncryptProtectedItemsAfterLoadingUnencryptedProjects();

        // Act
        XmlDocument doc = model.GetAsEncryptedXML();
        MainWindowModel model2 = new MainWindowModel(doc, pass2.ToSecureString());

        List<Project> ps = model.GetProjects();
        List<Project> ps2 = model2.GetProjects();
        int hash1 = ps[0].GetHashCode();
        int hash2 = ps2[0].GetHashCode();

        // Assert
        Assert.AreEqual(hash1, hash2);
        CollectionAssert.AreEqual(model.GetProjects(), model2.GetProjects());
    }

    [TestMethod]
    public void TestSaveLoad()
    {
        // Arrange
        string pass = new string("secret"); // force new instance
        string pass2 = new string("secret"); // force new instance

        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        model.LoadProjects(getSampleProjects());
        model.EncryptProtectedItemsAfterLoadingUnencryptedProjects();
        model.GenerateNewPKIpair();

        // Act
        XmlDocument doc = model.GetAsEncryptedXML();
        MainWindowModel model2 = new MainWindowModel(doc, pass2.ToSecureString());

        List<Project> ps = model.GetProjects();
        List<Project> ps2 = model2.GetProjects();
        int hash1 = ps[0].GetHashCode();
        int hash2 = ps2[0].GetHashCode();

        byte[] key1 = model.GetUnprotectedPrivateKey();
        byte[] key2 = model2.GetUnprotectedPrivateKey();


        // Assert
        Assert.AreEqual(hash1, hash2);
        CollectionAssert.AreEqual(model.GetProjects(), model2.GetProjects());
        CollectionAssert.AreEqual(key1, key2);
    }

    [TestMethod]
    public void TestSaveUnenencrypted()
    {
        // Arrange
        string pass = new string("secret"); // force new instance
        string pass2 = new string("secret"); // force new instance
        string pass3 = new string("secret2"); // force new instance
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        List<Project> ps = getSampleProjects();
        model.LoadProjects(ps);
        model.EncryptProtectedItemsAfterLoadingUnencryptedProjects();

        // Act
        XmlDocument doc = model.GetUnencryptedXML(pass2.ToSecureString());
        bool saveWithWrongPassword = true;
        try
        {
            XmlDocument doc2 = model.GetUnencryptedXML(pass3.ToSecureString());
        }
        catch (Exception)
        {
            saveWithWrongPassword = false;
        }

        // Assert
        Assert.IsTrue(doc.DocumentElement.ToString().Length >= 0);
        Assert.IsFalse(saveWithWrongPassword);
    }

    [TestMethod]
    public void TestLoadUnencrypted()
    {
        // Arrange
        string pass = new string("secret"); // force new instance
        string pass2 = new string("secret"); // force new instance
        string pass3 = new string("secret2"); // force new instance
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        List<Project> ps = getSampleProjects();
        model.LoadProjects(ps);
        model.EncryptProtectedItemsAfterLoadingUnencryptedProjects();
        XmlDocument unencrypted = model.GetUnencryptedXML(pass2.ToSecureString());

        // Act
        MainWindowModel model2 = new MainWindowModel(pass3.ToSecureString());
        model2.LoadUnencrypted(unencrypted);
        model2.EncryptProtectedItemsAfterLoadingUnencryptedProjects();


        // Assert
        Assert.AreEqual(model.Nodes.Count, model2.Nodes.Count);
    }


    #region DH tests

    [TestMethod]
    public void TestGenerateNewPKIpair()
    {
        // Arrange
        string pass = "secret";
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());

        // Act
        model.GenerateNewPKIpair();
        string pubkey1 = model.PublicKey;
        byte[] privkey1 = model.GetUnprotectedPrivateKey();
        model.GenerateNewPKIpair();
        string pubkey2 = model.PublicKey;
        byte[] privkey2 = model.GetUnprotectedPrivateKey();

        // Assert
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
        // Arrange
        string pass = "secret";
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        model.GenerateNewPKIpair();

        // Act
        XmlDocument key_store = model.GetKeyStoreXML();
        //XmlNode node = XMLhelper.FindNodeByName(key_store.DocumentElement.ChildNodes, "key_store", false);
        string public_key = XMLhelper.GetInnerTextFromChild(key_store, "public_key");
        byte[] salt = XMLhelper.GetInnerTextFromChild(key_store, "keystore_salt").FromBase64();
        byte[] private_key = XMLhelper.GetInnerTextFromChild(key_store, "private_key").FromBase64();

        // Assert
        Assert.IsFalse(string.IsNullOrWhiteSpace(public_key));
        Assert.IsNotNull(salt);
        Assert.IsTrue(salt.Length == MainWindowModel.SALT_LEN);
        Assert.IsNotNull(private_key);
        Assert.IsTrue(private_key.Length > 0);
    }

    [TestMethod]
    public void TestLoadKeyStore()
    {
        // Arrange
        string pass = new string("secret");
        string pass2 = new string("secret");
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        model.GenerateNewPKIpair();
        string pubkey1 = model.PublicKey;
        byte[] privkey1 = model.GetUnprotectedPrivateKey();
        byte[] salt = model.Salt;
        XmlDocument key_store = model.GetKeyStoreXML();

        MainWindowModel model2 = new MainWindowModel();
        model2.SetMasterKey(pass2.ToSecureString(), salt);

        // Act
        model2.LoadKeyStore(key_store.DocumentElement);
        string pubkey2 = model2.PublicKey;
        byte[] privkey2 = model2.GetUnprotectedPrivateKey();

        // Assert
        Assert.IsFalse(string.IsNullOrWhiteSpace(pubkey1));
        Assert.IsNotNull(privkey1);
        Assert.IsFalse(string.IsNullOrWhiteSpace(pubkey2));
        Assert.IsNotNull(privkey2);
        Assert.AreEqual(pubkey1, pubkey2);
        CollectionAssert.AreEqual(privkey1, privkey2);
    }


    #endregion

    #region PKI tests

    [TestMethod]
    public void TestGenerateCert()
    {
        // Arrange
        string pass = new string("secret");
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());

        // Act
        model.EnsureCAcert();

        // Assert
        Assert.IsTrue(model.CA_Certificate != null);
        Assert.IsTrue(model.CA_Certificate.HasPrivateKey);
    }

    [TestMethod]
    public void TestGetCertStoreXML()
    {
        // Arrange
        string pass = "secret";
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        model.EnsureCAcert();

        // Act
        XmlDocument cert_store = model.GetCertStoreXML();
        string id = XMLhelper.GetInnerTextFromChild(cert_store, "cert_id");
        byte[] salt = XMLhelper.GetInnerTextFromChild(cert_store, "certstore_salt").FromBase64();
        byte[] pfx_enc = XMLhelper.GetInnerTextFromChild(cert_store, "CDATA").FromBase64();

        // Assert
        Assert.IsFalse(string.IsNullOrWhiteSpace(id));
        Assert.IsNotNull(salt);
        Assert.IsTrue(salt.Length == MainWindowModel.SALT_LEN);
        Assert.IsNotNull(pfx_enc);
        Assert.IsTrue(pfx_enc.Length > 0);
    }

    [TestMethod]
    public void TestLoadCertStore()
    {
        // Arrange
        string pass = new string("secret");
        string pass2 = new string("secret");
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        model.EnsureCAcert();

        string id1 = model.CA_Certificate.GetSerialNumberString();
        bool pKey = model.CA_Certificate.HasPrivateKey;
        XmlDocument cert_store = model.GetCertStoreXML();

        MainWindowModel model2 = new MainWindowModel();
        model2.SetMasterKey(pass2.ToSecureString(), model.Salt);

        // Act
        model2.LoadCertStore(cert_store.DocumentElement);

        string id2 = model2.CA_Certificate.GetSerialNumberString();
        bool pKey2 = model2.CA_Certificate.HasPrivateKey;

        // Assert
        Assert.IsFalse(string.IsNullOrWhiteSpace(id1));
        Assert.IsFalse(string.IsNullOrWhiteSpace(id2));
        Assert.IsTrue(pKey);
        Assert.IsTrue(pKey2);
        Assert.AreEqual(id1, id2);
    }


    #endregion


    #region Node tests

    [TestMethod]
    public void TestGetNodeById()
    {
        // Arrange
        string pass = new string("secret"); // force new instance
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        List<Project> ps = getSampleProjects();
        string id = ps[0].Milestones[0].Tasks[0].UUID.ToString();
        model.LoadProjects(ps);

        // Act
        NodeModel node = model.GetNodeById(id);


        // Assert
        Assert.AreEqual(id, node.Id);

    }

    [TestMethod]
    public void TestDeleteNode()
    {
        // Arrange
        string pass = new string("secret"); // force new instance
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        List<Project> ps = getSampleProjects();
        string id = ps[0].Milestones[0].Tasks[0].UUID.ToString();
        model.LoadProjects(ps);

        // Act
        model.DeleteNode(model.GetNodeById(id));
        NodeModel node = model.GetNodeById(id);

        // Assert
        Assert.IsNull(node);
    }

    [TestMethod]
    public void TestAddNode()
    {
        // Arrange
        string nodeId = "new";
        string pass = new string("secret"); // force new instance
        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        List<Project> ps = getSampleProjects();
        model.LoadProjects(ps);
        NodeModel node = new NodeModel();
        node.Id = nodeId;


        // Act
        model.AddNode(node);
        NodeModel n = model.GetNodeById(nodeId);

        // Assert
        Assert.AreEqual(nodeId, n.Id);

    }

    #endregion



    [TestMethod]
    public void Test2()
    {
        // Arrange


        // Act


        // Assert

    }

    [TestMethod]
    public void Test3()
    {
        // Arrange


        // Act


        // Assert

    }

    public static List<Project> getSampleProjects()
    {
        List<Project> ps = new List<Project>();

        Project p = new Project("Top node 1");
        Milestone m1 = new Milestone("Milestone 1");
        Milestone m2 = new Milestone("Milestone 2");
        Task t1 = new Task("Task1");
        Task t2 = new Task("Task2");
        Task t3 = new Task("Task3");
        Subtask s1 = new Subtask("Subtask1");
        Subtask s2 = new Subtask("Subtask2");
        ProtectedItem pi1 = new ProtectedItem("Protected1");
        ProtectedItem pi2 = new ProtectedItem("Protected2");
        ProtectedItem pi3 = new ProtectedItem("Protected3");
        ProtectedItem pi4 = new ProtectedItem("Protected4");
        ProtectedItem pi5 = new ProtectedItem("Protected5");
        ProtectedItem pi6 = new ProtectedItem("Protected6");

        pi1.Password = "password1";
        pi2.Password = "password2";
        pi3.Password = "password3";
        pi4.Password = "password4";
        pi5.Password = "password5";
        pi6.Password = "password6";

        pi5.Items.Add(pi6);
        pi3.Items.Add(pi4);
        pi3.Items.Add(pi5);

        t2.ProtectedItems.Add(pi1);
        s1.ProtectedItems.Add(pi2);
        t1.ProtectedItems.Add(pi3);
        t1.SubTasks.Add(s1);
        t1.SubTasks.Add(s2);

        m1.Tasks.Add(t1);
        m2.Tasks.Add(t2);
        m2.Tasks.Add(t3);

        p.Milestones.Add(m1);
        p.Milestones.Add(m2);

        ps.Add(p);

        return ps;
    }
}
