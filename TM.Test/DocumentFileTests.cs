using System.Security.Cryptography;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class DocumentFileTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.DocumentFiles.{Guid.NewGuid():N}");
    private readonly ProjectCryptoService crypto = new();
    private readonly ProjectDocument document = new();
    [TestInitialize]
    public void Initialize()
    {
        Directory.CreateDirectory(directory);
        crypto.InitializeNew(document, "synthetic-document-password".ToCharArray().ToSecureStringAndClear());
    }
    [TestCleanup]
    public void Cleanup()
    {
        crypto.ClearAll(document);
        Directory.Delete(directory, true);
    }
    private string PathFor(string name) => Path.Combine(directory, name);

    [TestMethod]
    [DataRow(0)]
    [DataRow(200001)]
    public async Task DocumentFiles_InteroperateWithLegacyAndPreserveSourceAsync(int length)
    {
        DocumentFileService service = new(crypto);
        byte[] bytes = RandomNumberGenerator.GetBytes(length);
        await File.WriteAllBytesAsync(PathFor("source"), bytes);
        await service.ExecuteAsync(document, DocumentFileOperation.Encrypt, PathFor("source"), PathFor("encrypted"));
        byte[] salt = SecurityHelper.AesGetSaltToDecryptFile(PathFor("encrypted"), 32);
        byte[]? key = crypto.DeriveKey(document, "file encryption", salt);
        try
        {
            Assert.IsTrue(SecurityHelper.AesDecryptFile(PathFor("encrypted"), PathFor("legacy-plain"), ref key, 32));
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(PathFor("legacy-plain")));
        }
        finally { ProjectCryptoService.ClearArray(ref key); }
        salt = SecurityHelper.GetRandomKey(32);
        key = crypto.DeriveKey(document, "file encryption", salt);
        try { Assert.IsTrue(SecurityHelper.AesEncryptFile(PathFor("source"), PathFor("legacy-encrypted"), ref key, salt)); }
        finally { ProjectCryptoService.ClearArray(ref key); }
        byte[] ciphertext = await File.ReadAllBytesAsync(PathFor("legacy-encrypted"));
        await service.ExecuteAsync(document, DocumentFileOperation.Decrypt, PathFor("legacy-encrypted"), PathFor("plain"));
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(PathFor("plain")));
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(PathFor("source")));
        CollectionAssert.AreEqual(ciphertext, await File.ReadAllBytesAsync(PathFor("legacy-encrypted")));
        Assert.HasCount(0, Directory.GetDirectories(directory));
    }

    [TestMethod]
    public async Task WrongDocumentAndInvalidFiles_DoNotPublishOrModifyInputsAsync()
    {
        DocumentFileService service = new(crypto);
        await File.WriteAllTextAsync(PathFor("source"), "synthetic plaintext");
        await service.ExecuteAsync(document, DocumentFileOperation.Encrypt, PathFor("source"), PathFor("encrypted"));
        byte[] cipher = await File.ReadAllBytesAsync(PathFor("encrypted"));
        ProjectDocument wrong = new();
        crypto.InitializeNew(wrong, "other-password".ToCharArray().ToSecureStringAndClear());
        try
        {
            await Assert.ThrowsAsync<CryptographicException>(() => service.ExecuteAsync(wrong, DocumentFileOperation.Decrypt, PathFor("encrypted"), PathFor("wrong")));
        }
        finally { crypto.ClearAll(wrong); }
        await File.WriteAllBytesAsync(PathFor("broken"), [1, 2, 3]);
        bool rejected = false;
        try { await service.ExecuteAsync(document, DocumentFileOperation.Decrypt, PathFor("broken"), PathFor("invalid")); }
        catch (Exception error) when (error is CryptographicException or ArgumentException or IOException) { rejected = true; }
        Assert.IsTrue(rejected);
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(document, DocumentFileOperation.Encrypt, PathFor("source"), PathFor("encrypted")));
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(document, DocumentFileOperation.Encrypt, PathFor("source"), PathFor("source")));
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExecuteAsync(document, DocumentFileOperation.Encrypt, PathFor("source"), PathFor("canceled"), canceled.Token));
        Assert.HasCount(3, Directory.GetFiles(directory));
        Assert.HasCount(0, Directory.GetDirectories(directory));
        CollectionAssert.AreEqual(cipher, await File.ReadAllBytesAsync(PathFor("encrypted")));
        Assert.AreEqual("synthetic plaintext", await File.ReadAllTextAsync(PathFor("source")));
    }
}
