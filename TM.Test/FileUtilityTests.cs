using System.Security.Cryptography;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class FileUtilityTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.FileTools.{Guid.NewGuid():N}");
    private readonly FileUtilityService service = new();
    [TestInitialize] public void Initialize() => Directory.CreateDirectory(directory);
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private string PathFor(string name) => Path.Combine(directory, name);

    [TestMethod]
    [DataRow(FileUtilityOperation.Md5)]
    [DataRow(FileUtilityOperation.Sha1)]
    [DataRow(FileUtilityOperation.Sha256)]
    [DataRow(FileUtilityOperation.Sha384)]
    [DataRow(FileUtilityOperation.Sha512)]
    public async Task Hash_MatchesLegacySidecarAsync(FileUtilityOperation operation)
    {
        string input = PathFor("synthetic.bin"), output = PathFor("result.txt");
        await File.WriteAllBytesAsync(input, [0, 1, 2, 255, 128]);
        string legacy = operation switch
        {
            FileUtilityOperation.Md5 => TM.Helpers.HashHelper.MD5File(input),
            FileUtilityOperation.Sha1 => TM.Helpers.HashHelper.Sha1File(input),
            FileUtilityOperation.Sha256 => TM.Helpers.HashHelper.Sha256File(input),
            FileUtilityOperation.Sha384 => TM.Helpers.HashHelper.Sha384File(input),
            _ => TM.Helpers.HashHelper.Sha512File(input)
        };
        await service.ExecuteAsync(operation, input, output);
        Assert.AreEqual(await File.ReadAllTextAsync(legacy), await File.ReadAllTextAsync(output));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(65537)]
    [DataRow(200001)]
    public async Task Base64_RoundTripsAndMatchesLegacyAsync(int size)
    {
        byte[] data = RandomNumberGenerator.GetBytes(size);
        string input = PathFor("binary"), encoded = PathFor("encoded"), decoded = PathFor("decoded");
        await File.WriteAllBytesAsync(input, data);
        await service.ExecuteAsync(FileUtilityOperation.Base64Encode, input, encoded);
        Assert.AreEqual(Convert.ToBase64String(data), await File.ReadAllTextAsync(encoded));
        await SecurityHelper.Base64DeccodeFile(encoded, PathFor("legacy-decoded"));
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("legacy-decoded")));
        await SecurityHelper.Base64EncodeFile(input, PathFor("legacy-encoded"));
        await service.ExecuteAsync(FileUtilityOperation.Base64Decode, PathFor("legacy-encoded"), decoded);
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(decoded));
    }

    [TestMethod]
    [DataRow("A")]
    [DataRow("AAA")]
    [DataRow("!!!!")]
    [DataRow("YQ==Yg==")]
    [DataRow("=AAA")]
    [DataRow("YQ==é")]
    public async Task InvalidBase64_LeavesNoOutputOrTemporaryFileAsync(string text)
    {
        await File.WriteAllTextAsync(PathFor("input"), text);
        await Assert.ThrowsAsync<FormatException>(() => service.ExecuteAsync(FileUtilityOperation.Base64Decode, PathFor("input"), PathFor("output")));
        Assert.IsFalse(File.Exists(PathFor("output")));
        Assert.HasCount(1, Directory.GetFiles(directory));
        Assert.AreEqual(text, await File.ReadAllTextAsync(PathFor("input")));
    }

    [TestMethod]
    public async Task Decode_AcceptsBomAndWhitespaceAsync()
    {
        await File.WriteAllBytesAsync(PathFor("input"), [0xEF, 0xBB, 0xBF, .. Encoding.ASCII.GetBytes(" Y W\r\nJj\t ")]);
        await service.ExecuteAsync(FileUtilityOperation.Base64Decode, PathFor("input"), PathFor("output"));
        Assert.AreEqual("abc", await File.ReadAllTextAsync(PathFor("output")));
    }

    [TestMethod]
    public async Task FailedAndCanceledOperations_PreserveExistingFilesAsync()
    {
        await File.WriteAllTextAsync(PathFor("input"), "synthetic");
        await File.WriteAllTextAsync(PathFor("output"), "keep");
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(FileUtilityOperation.Base64Encode, PathFor("input"), PathFor("output")));
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(FileUtilityOperation.Sha256, PathFor("input"), PathFor("input")));
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.ExecuteAsync(FileUtilityOperation.Sha256, PathFor("missing"), PathFor("new")));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync((FileUtilityOperation)999, PathFor("input"), PathFor("new")));
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExecuteAsync(FileUtilityOperation.Sha256, PathFor("input"), PathFor("new"), canceled.Token));
        Assert.AreEqual("synthetic", await File.ReadAllTextAsync(PathFor("input")));
        Assert.AreEqual("keep", await File.ReadAllTextAsync(PathFor("output")));
        Assert.HasCount(2, Directory.GetFiles(directory));
    }
}
