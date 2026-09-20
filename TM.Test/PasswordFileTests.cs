using System.Security.Cryptography;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class PasswordFileTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.PasswordFiles.{Guid.NewGuid():N}");
    private readonly PasswordFileService service = new();
    [TestInitialize] public void Initialize() => Directory.CreateDirectory(directory);
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private string PathFor(string name) => Path.Combine(directory, name);
    private static SecureString Password(string value = "synthetic-file-password") => value.ToCharArray().ToSecureStringAndClear();

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(200001)]
    public async Task PasswordFiles_InteroperateWithLegacyAndKeepSourceAsync(int size)
    {
        byte[] data = RandomNumberGenerator.GetBytes(size);
        await File.WriteAllBytesAsync(PathFor("source"), data);
        using (SecureString password = Password())
            await service.ExecuteAsync(PasswordFileOperation.Encrypt, PathFor("source"), PathFor("encrypted"), password);
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("source")));
        using (SecureString password = Password())
            Assert.IsTrue(SecurityHelper.AesDecryptFile(PathFor("encrypted"), PathFor("legacy-plain"), password));
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("legacy-plain")));
        using (SecureString password = Password())
            Assert.IsTrue(SecurityHelper.AesEncryptFile(PathFor("source"), PathFor("legacy-encrypted"), password));
        byte[] originalCipher = await File.ReadAllBytesAsync(PathFor("legacy-encrypted"));
        using (SecureString password = Password())
            await service.ExecuteAsync(PasswordFileOperation.Decrypt, PathFor("legacy-encrypted"), PathFor("plain"), password);
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("plain")));
        CollectionAssert.AreEqual(originalCipher, await File.ReadAllBytesAsync(PathFor("legacy-encrypted")));
        Assert.HasCount(0, Directory.GetDirectories(directory, ".tm-crypto-*"));
    }

    [TestMethod]
    public async Task MalformedCiphertext_DoesNotPublishOutputAsync()
    {
        await File.WriteAllBytesAsync(PathFor("malformed"), [1, 2, 3]);
        using SecureString password = Password();
        bool rejected = false;
        try
        {
            await service.ExecuteAsync(PasswordFileOperation.Decrypt, PathFor("malformed"), PathFor("plain"), password);
        }
        catch (Exception error) when (error is CryptographicException or ArgumentException or IOException)
        {
            rejected = true;
        }
        Assert.IsTrue(rejected, "Malformed ciphertext was accepted.");
        Assert.IsFalse(File.Exists(PathFor("plain")));
        Assert.HasCount(0, Directory.GetDirectories(directory));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(PathFor("malformed")));
    }

    [TestMethod]
    public async Task RejectedOperations_PreserveFilesAndRemoveTemporaryOutputAsync()
    {
        await File.WriteAllTextAsync(PathFor("source"), "synthetic plaintext");
        using SecureString password = Password();
        await service.ExecuteAsync(PasswordFileOperation.Encrypt, PathFor("source"), PathFor("encrypted"), password);
        byte[] encrypted = await File.ReadAllBytesAsync(PathFor("encrypted"));
        using SecureString wrong = Password("incorrect-file-password");
        await Assert.ThrowsAsync<CryptographicException>(() => service.ExecuteAsync(PasswordFileOperation.Decrypt, PathFor("encrypted"), PathFor("wrong"), wrong));
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(PasswordFileOperation.Encrypt, PathFor("source"), PathFor("encrypted"), password));
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(PasswordFileOperation.Encrypt, PathFor("source"), PathFor("source"), password));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync((PasswordFileOperation)99, PathFor("source"), PathFor("invalid"), password));
        using SecureString empty = Password("");
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(PasswordFileOperation.Encrypt, PathFor("source"), PathFor("empty"), empty));
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExecuteAsync(PasswordFileOperation.Encrypt, PathFor("source"), PathFor("canceled"), password, canceled.Token));
        CollectionAssert.AreEqual(encrypted, await File.ReadAllBytesAsync(PathFor("encrypted")));
        Assert.AreEqual("synthetic plaintext", await File.ReadAllTextAsync(PathFor("source")));
        Assert.HasCount(2, Directory.GetFiles(directory));
        Assert.HasCount(0, Directory.GetDirectories(directory));
    }
}
