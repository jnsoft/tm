namespace TM.Test;

[TestClass]
public class FileHelperTests
{

    [TestMethod]
    public void TestFileEncryption()
    {
        // Arrange
        string fn1 = Directory.GetCurrentDirectory() + "\\file1.txt";
        string fn2 = Directory.GetCurrentDirectory() + "\\file2.txt";
        string fn3 = Directory.GetCurrentDirectory() + "\\file3.txt";

        string filecontent1 = "this is a file";
        string filecontent2 = "this is another file";
        string filecontent3 = "this is the third file";

        string pass = new string("secret");
        string pass2 = new string("secret");

        byte[] key = SecurityHelper.GetRandomKey(32);
        byte[] salt = SecurityHelper.GetRandomKey(32);

        if (File.Exists(fn1))
            File.Delete(fn1);

        if (File.Exists(fn2))
            File.Delete(fn2);

        if (File.Exists(fn3))
            File.Delete(fn3);

        if (File.Exists(fn1 + ".aes"))
            File.Delete(fn1 + ".aes");

        if (File.Exists(fn2 + ".enc"))
            File.Delete(fn2 + ".enc");

        if (File.Exists(fn3 + ".aes"))
            File.Delete(fn3 + ".aes");

        File.WriteAllText(fn1, filecontent1);
        File.WriteAllText(fn2, filecontent2);
        File.WriteAllText(fn3, filecontent3);

        // Act
        FileHelper.EncryptFile(fn1, pass.ToSecureString());
        FileHelper.EncryptFile(fn2);
        FileHelper.EncryptFile(fn3, ref key, salt);

        bool encryptedFile1Exists = File.Exists(fn1 + ".aes");
        bool encryptedFile2Exists = File.Exists(fn2 + ".enc");
        bool encryptedFile3Exists = File.Exists(fn3 + ".aes");
        bool plainFile1DoesNotExists = !File.Exists(fn1);
        bool plainFile2DoesNotExists = !File.Exists(fn2);
        bool plainFile3DoesNotExists = !File.Exists(fn3);

        FileHelper.DecryptFile(fn1 + ".aes", pass2.ToSecureString());
        FileHelper.DecryptFile(fn2 + ".enc");
        FileHelper.DecryptFile(fn3 + ".aes", ref key);

        string returnedFilecontent1 = File.ReadAllText(fn1);
        string returnedFilecontent2 = File.ReadAllText(fn2);
        string returnedFilecontent3 = File.ReadAllText(fn3);


        // Assert
        Assert.IsTrue(encryptedFile1Exists);
        Assert.IsTrue(encryptedFile2Exists);
        Assert.IsTrue(encryptedFile3Exists);
        Assert.IsTrue(plainFile1DoesNotExists);
        Assert.IsTrue(plainFile2DoesNotExists);
        Assert.IsTrue(plainFile3DoesNotExists);
        Assert.AreEqual(filecontent1, returnedFilecontent1);
        Assert.AreEqual(filecontent2, returnedFilecontent2);
        Assert.AreEqual(filecontent3, returnedFilecontent3);
    }

    [TestMethod]
    public void TestFileEncryptionDerivedKey()
    {
        // Arrange
        string fn1 = Directory.GetCurrentDirectory() + "\\file1.txt";
        string filecontent1 = "this is a file";

        if (File.Exists(fn1))
            File.Delete(fn1);

        if (File.Exists(fn1 + ".aes"))
            File.Delete(fn1 + ".aes");

        File.WriteAllText(fn1, filecontent1);

        string pass = new string("secret");
        string pass2 = new string("secret");

        MainWindowModel model = new MainWindowModel(pass.ToSecureString());
        MainWindowModel model2 = new MainWindowModel();
        model2.SetMasterKey(pass2.ToSecureString(), model.Salt);

        byte[] salt = SecurityHelper.GetRandomKey(MainWindowModel.SALT_LEN);
        byte[] key = model.DeriveKey("test file encryption", salt);

        // Act
        FileHelper.EncryptFile(fn1, ref key, salt);

        bool encryptedFile1Exists = File.Exists(fn1 + ".aes");
        bool plainFile1DoesNotExists = !File.Exists(fn1);


        byte[] salt2 = FileHelper.ReadSaltFromFile(fn1 + ".aes");
        byte[] key2 = model2.DeriveKey("test file encryption", salt2);

        FileHelper.DecryptFile(fn1 + ".aes", ref key2);

        string returnedFilecontent1 = File.ReadAllText(fn1);

        // Assert
        Assert.IsTrue(encryptedFile1Exists);
        Assert.IsTrue(plainFile1DoesNotExists);
        Assert.AreEqual(filecontent1, returnedFilecontent1);
    }
}
