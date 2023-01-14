namespace TM.Test;

[TestClass]
public class HashHelperTests
{
    [TestMethod]
    public void TestHashes()
    {
        // Arrange
        string fn = Directory.GetCurrentDirectory() + "\\file1.txt";

        string content = "this is a file";

        // tested with certutil, e.g. "certutil -hashfile file1.txt sha512"
        string md5 = "35e03be09191a228f64d7a6c58b87947";
        string sha1 = "1769c61a6296b92349b7bff38f4bea71abf92287";
        string sha256 = "d4a310bf86d4acb9e5cc355de2b71fa19e404205e21b59e1a5d1607eaa2203ef";
        string sha384 = "85f0b8eb7e471edde9fb97f6364a6e978abf5c2367478eaea4f1d96a748bb526edf4ffb6a7e28fa00da930957612e444";
        string sha512 = "cf46c3ccddc17830c5b9067bd3d1ad2b7eaa0ac343072cfe5fa9acd1fde71ceff140d76a6d702b09162a5065070fddfd13bbc3520286a0ac2ae72a6bfef827e1";

        if (File.Exists(fn))
            File.Delete(fn);

        File.WriteAllText(fn, content, Encoding.UTF8);

        // Act
        string md5File = HashHelper.MD5File(fn);
        string sha1File = HashHelper.Sha1File(fn);
        string sha256File = HashHelper.Sha256File(fn);
        string sha384File = HashHelper.Sha384File(fn);
        string sha512File = HashHelper.Sha512File(fn);

        string calculated_md5 = File.ReadAllText(md5File).Replace(" ", "").ToLower();
        string calculated_sha1 = File.ReadAllText(sha1File).Replace(" ", "").ToLower();
        string calculated_sha256 = File.ReadAllText(sha256File).Replace(" ", "").ToLower();
        string calculated_sha384 = File.ReadAllText(sha384File).Replace(" ", "").ToLower();
        string calculated_sha512 = File.ReadAllText(sha512File).Replace(" ", "").ToLower();

        File.Delete(md5File);
        File.Delete(sha1File);
        File.Delete(sha256File);
        File.Delete(sha384File);
        File.Delete(sha512File);


        // Assert
        Assert.AreEqual(md5, calculated_md5);
        Assert.AreEqual(sha1, calculated_sha1);
        Assert.AreEqual(sha256, calculated_sha256);
        Assert.AreEqual(sha384, calculated_sha384);
        Assert.AreEqual(sha512, calculated_sha512);
    }

    [TestMethod]
    public void TestMAC()
    {
        // Arrange
        string fn = Directory.GetCurrentDirectory() + "\\file1.txt";

        string content = "this is a file";

        if (File.Exists(fn))
            File.Delete(fn);

        File.WriteAllText(fn, content, Encoding.UTF8);

        byte[] fileContent = File.ReadAllBytes(fn);

        byte[] key = SecurityHelper.GetRandomKey(256);

        byte[] md5 = SecurityHelper.GetMAC(SecurityHelper.MACTypes.MD5, fileContent, key);
        byte[] sha1 = SecurityHelper.GetMAC(SecurityHelper.MACTypes.SHA1, fileContent, key);
        byte[] sha256 = SecurityHelper.GetMAC(SecurityHelper.MACTypes.SHA256, fileContent, key);
        // byte[] sha384 = SecurityHelper.GetMAC(SecurityHelper.MACTypes.SHA384, fileContent, key);
        byte[] sha512 = SecurityHelper.GetMAC(SecurityHelper.MACTypes.SHA512, fileContent, key);

        // Act
        string md5File = HashHelper.MD5SignFile(key, fn);
        string sha1File = HashHelper.Sha1SignFile(key, fn);
        string sha256File = HashHelper.Sha256SignFile(key, fn);
        string sha384File = HashHelper.Sha384SignFile(key, fn);
        string sha512File = HashHelper.Sha512SignFile(key, fn);

        string calculated_md5 = File.ReadAllText(md5File).Replace(" ", "");
        string calculated_sha1 = File.ReadAllText(sha1File).Replace(" ", "");
        string calculated_sha256 = File.ReadAllText(sha256File).Replace(" ", "");
        string calculated_sha384 = File.ReadAllText(sha384File).Replace(" ", "");
        string calculated_sha512 = File.ReadAllText(sha512File).Replace(" ", "");

        bool md5Verify = HashHelper.MD5VerifyFile(key, fn, md5);
        bool sha1Verify = HashHelper.Sha1VerifyFile(key, fn, sha1);
        bool sha256Verify = HashHelper.Sha256VerifyFile(key, fn, sha256);
        // bool sha384Verify = HashHelper.Sha384VerifyFile(key, fn, sha384);
        bool sha512Verify = HashHelper.Sha512VerifyFile(key, fn, sha512);

        File.Delete(md5File);
        File.Delete(sha1File);
        File.Delete(sha256File);
        File.Delete(sha384File);
        File.Delete(sha512File);



        // Assert
        Assert.AreEqual(md5.ToHexString(), calculated_md5);
        Assert.AreEqual(sha1.ToHexString(), calculated_sha1);
        Assert.AreEqual(sha256.ToHexString(), calculated_sha256);
        // Assert.AreEqual(sha384.ToHexString(), calculated_sha384);
        Assert.AreEqual(sha512.ToHexString(), calculated_sha512);

        Assert.IsTrue(md5Verify);
        Assert.IsTrue(sha1Verify);
        Assert.IsTrue(sha256Verify);
        // Assert.IsTrue(sha384Verify);
        Assert.IsTrue(sha512Verify);
    }
}
