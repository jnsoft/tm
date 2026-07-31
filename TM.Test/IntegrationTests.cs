using TM.Services;

namespace TM.Test;

[TestClass]
public class IntegrationTests
{
    private static readonly ProjectCryptoService Crypto = new();

    [TestMethod]
    public void TestSaveAndOpenXmlFile()
    {
        // Arrange
        string fn = Directory.GetCurrentDirectory() + "\\file1.sav";
        if (File.Exists(fn))
            File.Delete(fn);

        ProjectDocument model = new ProjectDocument();
        Crypto.InitializeNew(model, new string("secret").ToSecureString());
        model.LoadProjects(ProjectDocumentTests.getSampleProjects());
        Crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(model);
        Crypto.GenerateKeys(model);
        Crypto.EnsureCaCertificate(model);

        // Act
        XMLhelper.XmlToFile(Crypto.GetAsEncryptedXml(model).DocumentElement, fn);
        Crypto.ClearAll(model);

        XmlDocument doc = XMLhelper.XmlFromFile(fn);
        model = new ProjectDocument();
        Crypto.LoadEncryptedDocument(model, doc, new string("secret").ToSecureString());

        // Assert
        Assert.IsFalse(model.IsEmpty);
        Assert.IsTrue(model.IsDiffieHellmanEnabled);
        Assert.IsTrue(model.Security.IsPkiEnabled);
    }

    [TestMethod]
    public void TestSaveAndOpenXmlFileUnencrypted()
    {
        // Arrange
        string fn = Directory.GetCurrentDirectory() + "\\file1.sav";
        if (File.Exists(fn))
            File.Delete(fn);

        ProjectDocument model = new ProjectDocument();
        Crypto.InitializeNew(model, new string("secret").ToSecureString());
        model.LoadProjects(ProjectDocumentTests.getSampleProjects());
        Crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(model);

        // Act
        XmlDocument doc = Crypto.GetUnencryptedXml(model, new string("secret").ToSecureString());
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

        ProjectDocument model2 = new ProjectDocument();
        Crypto.InitializeNew(model2, new string("secret").ToSecureString());
        model2.LoadUnencrypted(doc);
        Crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(model2);


        // Assert
        Assert.IsTrue(!model2.IsEmpty);
        Assert.AreEqual(model.Nodes.Count, model2.Nodes.Count);
        //Assert.AreEqual(ps[0].AllProtectedItems()[0].UUID, ps2[0].AllProtectedItems()[0].UUID); // model vs model2
        //Assert.AreNotEqual(ps[0].AllProtectedItems()[0].Password, ps2[0].AllProtectedItems()[0].Password); // unencrypted passwords should differ between model and model2
        //Assert.AreEqual(pass, pass2); // check if unencrypted password from protected item in model matches that password in model2
    }
}
