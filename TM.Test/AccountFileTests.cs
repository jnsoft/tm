using System.Security.Cryptography;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class AccountFileTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.AccountFiles.{Guid.NewGuid():N}");
    [TestInitialize] public void Initialize() => Directory.CreateDirectory(directory);
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private string PathFor(string name) => Path.Combine(directory, name);

    [TestMethod]
    [DataRow(0)]
    [DataRow(200001)]
    public async Task AccountCopies_UseProtectionBoundaryAndPreserveSourceAsync(int size)
    {
        byte[] data = RandomNumberGenerator.GetBytes(size);
        await File.WriteAllBytesAsync(PathFor("source"), data);
        TestAccountFileProtection protection = new();
        AccountFileService service = new(protection);
        await service.ExecuteAsync(AccountFileOperation.Encrypt, PathFor("source"), PathFor("encrypted"));
        Assert.IsTrue(protection.LastEncrypted);
        Assert.AreNotEqual(PathFor("source"), protection.LastPath);
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("encrypted")));
        protection.EncryptedSources.Add(PathFor("encrypted"));
        await service.ExecuteAsync(AccountFileOperation.Decrypt, PathFor("encrypted"), PathFor("plain"));
        Assert.IsFalse(protection.LastEncrypted);
        Assert.AreEqual(2, protection.Calls);
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("source")));
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("encrypted")));
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("plain")));
        Assert.HasCount(0, Directory.GetDirectories(directory));
    }

    [TestMethod]
    public async Task InvalidAccountOperations_DoNotTouchFilesOrProtectionAsync()
    {
        await File.WriteAllTextAsync(PathFor("source"), "synthetic data");
        await File.WriteAllTextAsync(PathFor("existing"), "keep output");
        TestAccountFileProtection protection = new();
        AccountFileService service = new(protection);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync((AccountFileOperation)999, PathFor("source"), PathFor("invalid")));
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(AccountFileOperation.Encrypt, PathFor("source"), PathFor("source")));
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(AccountFileOperation.Encrypt, PathFor("source"), PathFor("existing")));
        await Assert.ThrowsAsync<CryptographicException>(() => service.ExecuteAsync(AccountFileOperation.Decrypt, PathFor("source"), PathFor("plain")));
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.ExecuteAsync(AccountFileOperation.Encrypt, PathFor("missing"), PathFor("plain")));
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExecuteAsync(AccountFileOperation.Encrypt, PathFor("source"), PathFor("canceled"), canceled.Token));
        Assert.AreEqual(0, protection.Calls);
        Assert.AreEqual("synthetic data", await File.ReadAllTextAsync(PathFor("source")));
        Assert.AreEqual("keep output", await File.ReadAllTextAsync(PathFor("existing")));
        Assert.HasCount(2, Directory.GetFiles(directory));
        Assert.HasCount(0, Directory.GetDirectories(directory));
    }

    [TestMethod]
    public async Task FailedNoOpOrRacingProtection_DoesNotPublishOrOverwriteAsync()
    {
        await File.WriteAllTextAsync(PathFor("source"), "synthetic data");
        TestAccountFileProtection protection = new() { DuringSet = () => throw new IOException("synthetic native failure") };
        AccountFileService service = new(protection);
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(AccountFileOperation.Encrypt, PathFor("source"), PathFor("failed")));
        protection.DuringSet = null;
        protection.IgnoreChange = true;
        await Assert.ThrowsAsync<CryptographicException>(() => service.ExecuteAsync(AccountFileOperation.Encrypt, PathFor("source"), PathFor("noop")));
        protection.IgnoreChange = false;
        protection.DuringSet = () => File.WriteAllText(PathFor("raced"), "keep raced output");
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(AccountFileOperation.Encrypt, PathFor("source"), PathFor("raced")));
        Assert.AreEqual("keep raced output", await File.ReadAllTextAsync(PathFor("raced")));
        Assert.AreEqual("synthetic data", await File.ReadAllTextAsync(PathFor("source")));
        Assert.HasCount(2, Directory.GetFiles(directory));
        Assert.HasCount(0, Directory.GetDirectories(directory));
    }

    [TestMethod]
    public async Task CancellationDuringProtection_WaitsThenDiscardsOutputAsync()
    {
        await File.WriteAllTextAsync(PathFor("source"), "synthetic data");
        using CancellationTokenSource canceled = new();
        TestAccountFileProtection protection = new() { DuringSet = canceled.Cancel };
        AccountFileService service = new(protection);
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExecuteAsync(AccountFileOperation.Encrypt, PathFor("source"), PathFor("canceled"), canceled.Token));
        Assert.AreEqual(1, protection.Calls);
        Assert.IsTrue(protection.LastEncrypted, "Protection completes before cleanup.");
        Assert.AreEqual("synthetic data", await File.ReadAllTextAsync(PathFor("source")));
        Assert.HasCount(1, Directory.GetFiles(directory));
        Assert.HasCount(0, Directory.GetDirectories(directory));
    }
}

// Tests never call EFS or create/change the machine's account encryption certificate.
internal sealed class TestAccountFileProtection : IAccountFileProtection
{
    public HashSet<string> EncryptedSources { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Action? DuringSet { get; set; }
    public bool IgnoreChange { get; set; }
    public bool LastEncrypted { get; private set; }
    public string? LastPath { get; private set; }
    public int Calls { get; private set; }
    public bool IsEncrypted(string path) => EncryptedSources.Contains(path) || (path == LastPath && LastEncrypted);
    public void SetEncrypted(string path, bool encrypted)
    {
        Calls++;
        LastPath = path;
        DuringSet?.Invoke();
        if (!IgnoreChange) LastEncrypted = encrypted;
    }
}
