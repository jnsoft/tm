namespace TM.Test;

[TestClass]
public class IntegrationTests
{
    [TestMethod]
    public void TestSaveAndOpenXmlFile()
    {
        // Arrange
        string fn = Directory.GetCurrentDirectory() + "\\file1.sav";
        if (File.Exists(fn))
            File.Delete(fn);

        MainWindowModel model = new MainWindowModel(new string("secret").ToSecureString());
        model.LoadProjects(MainWindowModelTests.getSampleProjects());
        model.EncryptProtectedItemsAfterLoadingUnencryptedProjects();
        model.GenerateNewPKIpair();
        model.EnsureCAcert();

        // Act
        XMLhelper.XmlToFile(model.GetAsEncryptedXML().DocumentElement, fn);
        model.ClearAll();
        model = null;

        XmlDocument doc = XMLhelper.XmlFromFile(fn);
        model = new MainWindowModel(doc, new string("secret").ToSecureString());

        // Assert
        Assert.IsFalse(model.IsEmpty);
        Assert.IsTrue(model.IsDiffieHellmanEnabled);
        Assert.IsTrue(model.IsPKIenabled);
    }

    [TestMethod]
    public void TestSaveAndOpenXmlFileUnencrypted()
    {
        // Arrange
        string fn = Directory.GetCurrentDirectory() + "\\file1.sav";
        if (File.Exists(fn))
            File.Delete(fn);

        MainWindowModel model = new MainWindowModel(new string("secret").ToSecureString());
        model.LoadProjects(MainWindowModelTests.getSampleProjects());
        model.EncryptProtectedItemsAfterLoadingUnencryptedProjects();

        // Act
        XmlDocument doc = model.GetUnencryptedXML(new string("secret").ToSecureString());
        byte[] key = SecurityHelper.GetRandomKey(32);
        string fileContent = SecurityHelper.GCMEncrypt(doc.InnerXml.ToByte(), key).ToBase64();
        File.WriteAllText(fn, fileContent);
        string str_key = key.ToBase64();

        byte[] bytekey = str_key.FromBase64();
        string fileStringContent = File.ReadAllText(fn);
        byte[] fileContent2 = SecurityHelper.GCMDecrypt(fileStringContent.FromBase64(), bytekey);
        XmlDocument doc2 = new XmlDocument();
        string xml = fileContent2.ToStringFromByte();
        doc2.LoadXml(xml);

        MainWindowModel model2 = new MainWindowModel(new string("secret").ToSecureString());
        model2.LoadUnencrypted(doc);
        model2.EncryptProtectedItemsAfterLoadingUnencryptedProjects();


        // Assert
        Assert.IsTrue(!model2.IsEmpty);
        Assert.AreEqual(model.Nodes.Count, model2.Nodes.Count);
        //Assert.AreEqual(ps[0].AllProtectedItems()[0].UUID, ps2[0].AllProtectedItems()[0].UUID); // model vs model2
        //Assert.AreNotEqual(ps[0].AllProtectedItems()[0].Password, ps2[0].AllProtectedItems()[0].Password); // unencrypted passwords should differ between model and model2
        //Assert.AreEqual(pass, pass2); // check if unencrypted password from protected item in model matches that password in model2
    }
}
