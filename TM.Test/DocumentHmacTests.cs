using System.Security.Cryptography;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class DocumentHmacTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.Hmac.{Guid.NewGuid():N}");
    private readonly ProjectCryptoService crypto = new();
    private readonly ProjectDocument document = new();
    [TestInitialize] public void Initialize()
    {
        Directory.CreateDirectory(directory);
        crypto.InitializeNew(document, "test-hmac-password".ToCharArray().ToSecureStringAndClear());
    }
    [TestCleanup] public void Cleanup() { crypto.ClearAll(document); Directory.Delete(directory, true); }
    private string FilePath(string name) => Path.Combine(directory, name);

    [TestMethod]
    [DataRow(HmacAlgorithm.Md5)]
    [DataRow(HmacAlgorithm.Sha1)]
    [DataRow(HmacAlgorithm.Sha256)]
    [DataRow(HmacAlgorithm.Sha384)]
    [DataRow(HmacAlgorithm.Sha512)]
    public async Task Sidecars_InteroperateWithWpfAsync(HmacAlgorithm algorithm)
    {
        DocumentHmacService service = new(crypto);
        string source = FilePath("source"), sidecar = FilePath("sidecar");
        await File.WriteAllBytesAsync(source, RandomNumberGenerator.GetBytes(200001));
        await service.CreateAsync(document, algorithm, source, sidecar);
        string[] lines = await File.ReadAllLinesAsync(sidecar);
        byte[] salt = Convert.FromBase64String(lines[1]);
        byte[]? key = crypto.DeriveKey(document, "HMAC", salt, 64);
        try
        {
            string legacy = algorithm switch
            {
                HmacAlgorithm.Md5 => TM.Helpers.HashHelper.MD5SignFile(key, source),
                HmacAlgorithm.Sha1 => TM.Helpers.HashHelper.Sha1SignFile(key, source),
                HmacAlgorithm.Sha256 => TM.Helpers.HashHelper.Sha256SignFile(key, source),
                HmacAlgorithm.Sha384 => TM.Helpers.HashHelper.Sha384SignFile(key, source),
                _ => TM.Helpers.HashHelper.Sha512SignFile(key, source)
            };
            Assert.AreEqual(lines[0], await File.ReadAllTextAsync(legacy));
            byte[] mac = Convert.FromHexString(lines[0].Replace(" ", ""));
            bool verified = algorithm switch
            {
                HmacAlgorithm.Md5 => TM.Helpers.HashHelper.MD5VerifyFile(key, source, mac),
                HmacAlgorithm.Sha1 => TM.Helpers.HashHelper.Sha1VerifyFile(key, source, mac),
                HmacAlgorithm.Sha256 => TM.Helpers.HashHelper.Sha256VerifyFile(key, source, mac),
                HmacAlgorithm.Sha384 => TM.Helpers.HashHelper.Sha384VerifyFile(key, source, mac),
                _ => TM.Helpers.HashHelper.Sha512VerifyFile(key, source, mac)
            };
            Assert.IsTrue(verified);
            await File.AppendAllTextAsync(legacy, "\n" + lines[1], Encoding.UTF8);
            Assert.IsTrue(await service.VerifyAsync(document, algorithm, source, legacy));
        }
        finally { ProjectCryptoService.ClearArray(ref key); }
    }

    [TestMethod]
    public async Task ChangedDataMacSaltOrDocument_FailVerificationAsync()
    {
        DocumentHmacService service = new(crypto);
        string source = FilePath("source"), sidecar = FilePath("sidecar");
        await File.WriteAllBytesAsync(source, []);
        await service.CreateAsync(document, HmacAlgorithm.Sha256, source, sidecar);
        string original = await File.ReadAllTextAsync(sidecar);
        Assert.IsTrue(await service.VerifyAsync(document, HmacAlgorithm.Sha256, source, sidecar));
        await File.WriteAllTextAsync(source, "changed");
        Assert.IsFalse(await service.VerifyAsync(document, HmacAlgorithm.Sha256, source, sidecar));
        await File.WriteAllBytesAsync(source, []);
        await File.WriteAllTextAsync(sidecar, (original[0] == '0' ? "1" : "0") + original[1..]);
        Assert.IsFalse(await service.VerifyAsync(document, HmacAlgorithm.Sha256, source, sidecar));
        string[] lines = original.Split('\n');
        byte[] salt = Convert.FromBase64String(lines[1]); salt[0] ^= 1;
        await File.WriteAllTextAsync(sidecar, lines[0] + "\n" + Convert.ToBase64String(salt));
        Assert.IsFalse(await service.VerifyAsync(document, HmacAlgorithm.Sha256, source, sidecar));
        await File.WriteAllTextAsync(sidecar, original);
        ProjectDocument wrong = new();
        crypto.InitializeNew(wrong, "different-password".ToCharArray().ToSecureStringAndClear());
        try { Assert.IsFalse(await service.VerifyAsync(wrong, HmacAlgorithm.Sha256, source, sidecar)); }
        finally { crypto.ClearAll(wrong); }
        Assert.AreEqual(original, await File.ReadAllTextAsync(sidecar));
    }

    [TestMethod]
    public async Task MalformedSidecarsAndCanceledCreation_LeaveFilesUnchangedAsync()
    {
        DocumentHmacService service = new(crypto);
        string source = FilePath("source"), sidecar = FilePath("sidecar");
        await File.WriteAllTextAsync(source, "unchanged");
        string mac = new('0', 64), salt = Convert.ToBase64String(new byte[32]);
        foreach (string invalid in new[] { "", mac, "GG\n" + salt, mac + "\nAA==", "00\n" + salt,
            mac + "\n" + salt + "\nextra", new string('A', 4097), mac + "\ninvalid!" })
        {
            await File.WriteAllTextAsync(sidecar, invalid);
            await Assert.ThrowsAsync<FormatException>(() => service.VerifyAsync(document, HmacAlgorithm.Sha256, source, sidecar));
            Assert.AreEqual(invalid, await File.ReadAllTextAsync(sidecar));
        }
        await service.CreateAsync(document, HmacAlgorithm.Sha256, source, FilePath("valid"));
        string content = await File.ReadAllTextAsync(FilePath("valid"));
        await File.WriteAllTextAsync(sidecar, content.Replace("\n", "\r\n") + "\r\n", new UTF8Encoding(true));
        Assert.IsTrue(await service.VerifyAsync(document, HmacAlgorithm.Sha256, source, sidecar));
        await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(document, HmacAlgorithm.Sha256, source, sidecar));
        await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(document, HmacAlgorithm.Sha256, source, source));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.CreateAsync(document, (HmacAlgorithm)999, source, FilePath("bad")));
        using CancellationTokenSource canceled = new(); canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.CreateAsync(document, HmacAlgorithm.Sha256, source, FilePath("canceled"), canceled.Token));
        Assert.HasCount(3, Directory.GetFiles(directory));
        Assert.AreEqual("unchanged", await File.ReadAllTextAsync(source));
    }
}
