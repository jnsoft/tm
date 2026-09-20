using System.Security.Cryptography;
using TM.Models;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class PublicKeyFileTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.PublicKeyFiles.{Guid.NewGuid():N}");
    private readonly ProjectCryptoService crypto = new();
    [TestInitialize] public void Initialize() => Directory.CreateDirectory(directory);
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private string PathFor(string name) => Path.Combine(directory, name);

    [TestMethod]
    [DataRow(0)]
    [DataRow(200001)]
    public async Task PublicKeyFiles_InteroperateWithLegacyGcmAndPreserveSourcesAsync(int size)
    {
        byte[] data = RandomNumberGenerator.GetBytes(size);
        await File.WriteAllBytesAsync(PathFor("source"), data);
        ProjectDocument recipient = CreateDocumentWithKeys();
        using ECDiffieHellman sender = ECDiffieHellman.Create();
        byte[] senderPublic = sender.ExportSubjectPublicKeyInfo();
        byte[] recipientPublic = recipient.Security.PublicKey!.FromBase64();
        byte[] recipientPrivate = crypto.GetUnprotectedPrivateKey(recipient)!;
        try
        {
            PublicKeyFileService service = new(crypto);
            await service.ExecuteAsync(recipient, PublicKeyFileOperation.Encrypt, senderPublic, PathFor("source"), PathFor("encrypted"));
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("source")));
            byte[] senderPrivate = sender.ExportECPrivateKey();
            byte[] senderShared = SecurityHelper.DeriveSymmetricKey(senderPrivate, recipientPublic);
            try
            {
                CollectionAssert.AreEqual(data, SecurityHelper.GCMDecrypt(await File.ReadAllBytesAsync(PathFor("encrypted")), senderShared));
                byte[] legacyShared = SecurityHelper.DeriveSymmetricKey(recipientPrivate, senderPublic);
                byte[] legacyEncrypted = SecurityHelper.GCMEncrypt(data, legacyShared);
                try { await File.WriteAllBytesAsync(PathFor("legacy"), legacyEncrypted); }
                finally { CryptographicOperations.ZeroMemory(legacyEncrypted); CryptographicOperations.ZeroMemory(legacyShared); }
            }
            finally { CryptographicOperations.ZeroMemory(senderPrivate); CryptographicOperations.ZeroMemory(senderShared); }
            await service.ExecuteAsync(recipient, PublicKeyFileOperation.Decrypt, senderPublic, PathFor("legacy"), PathFor("plain"));
            CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(PathFor("plain")));
            Assert.HasCount(0, Directory.GetDirectories(directory));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(senderPublic);
            CryptographicOperations.ZeroMemory(recipientPublic);
            CryptographicOperations.ZeroMemory(recipientPrivate);
            crypto.ClearAll(recipient);
        }
    }

    [TestMethod]
    public async Task InvalidPublicKeyOperations_PreserveInputsAndCleanupAsync()
    {
        await File.WriteAllTextAsync(PathFor("source"), "synthetic public-key file");
        await File.WriteAllTextAsync(PathFor("existing"), "keep destination");
        ProjectDocument recipient = CreateDocumentWithKeys();
        PublicKeyFileService service = new(crypto);
        byte[] validPeer;
        using (ECDiffieHellman peer = ECDiffieHellman.Create()) validPeer = peer.ExportSubjectPublicKeyInfo();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(recipient, PublicKeyFileOperation.Encrypt, Array.Empty<byte>(), PathFor("source"), PathFor("empty")));
            await Assert.ThrowsAsync<CryptographicException>(() => service.ExecuteAsync(recipient, PublicKeyFileOperation.Encrypt, new byte[] { 1, 2, 3 }, PathFor("source"), PathFor("invalid")));
            await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(recipient, PublicKeyFileOperation.Encrypt, validPeer, PathFor("source"), PathFor("source")));
            await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(recipient, PublicKeyFileOperation.Encrypt, validPeer, PathFor("source"), PathFor("existing")));
            await Assert.ThrowsAsync<FileNotFoundException>(() => service.ExecuteAsync(recipient, PublicKeyFileOperation.Encrypt, validPeer, PathFor("missing"), PathFor("output")));
            using CancellationTokenSource canceled = new();
            canceled.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExecuteAsync(recipient, PublicKeyFileOperation.Encrypt, validPeer, PathFor("source"), PathFor("canceled"), canceled.Token));
            await File.WriteAllBytesAsync(PathFor("malformed"), [1, 2, 3]);
            await Assert.ThrowsAsync<CryptographicException>(() => service.ExecuteAsync(recipient, PublicKeyFileOperation.Decrypt, validPeer, PathFor("malformed"), PathFor("plain")));
            Assert.AreEqual("synthetic public-key file", await File.ReadAllTextAsync(PathFor("source")));
            Assert.AreEqual("keep destination", await File.ReadAllTextAsync(PathFor("existing")));
            Assert.HasCount(3, Directory.GetFiles(directory));
            Assert.HasCount(0, Directory.GetDirectories(directory));
        }
        finally { CryptographicOperations.ZeroMemory(validPeer); crypto.ClearAll(recipient); }
    }

    [TestMethod]
    public async Task WrongPeerAndOversizedInput_DoNotPublishAsync()
    {
        await File.WriteAllTextAsync(PathFor("source"), "synthetic public-key file");
        ProjectDocument recipient = CreateDocumentWithKeys();
        PublicKeyFileService service = new(crypto);
        using ECDiffieHellman peer = ECDiffieHellman.Create();
        using ECDiffieHellman wrong = ECDiffieHellman.Create();
        byte[] peerPublic = peer.ExportSubjectPublicKeyInfo(), wrongPublic = wrong.ExportSubjectPublicKeyInfo();
        try
        {
            await service.ExecuteAsync(recipient, PublicKeyFileOperation.Encrypt, peerPublic, PathFor("source"), PathFor("encrypted"));
            await Assert.ThrowsAsync<CryptographicException>(() => service.ExecuteAsync(recipient, PublicKeyFileOperation.Decrypt, wrongPublic, PathFor("encrypted"), PathFor("wrong")));
            await using (FileStream large = new(PathFor("large"), FileMode.CreateNew, FileAccess.Write)) large.SetLength(PublicKeyFileService.MaximumInputBytes + 1);
            await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(recipient, PublicKeyFileOperation.Encrypt, peerPublic, PathFor("large"), PathFor("large-output")));
            Assert.IsFalse(File.Exists(PathFor("wrong")));
            Assert.IsFalse(File.Exists(PathFor("large-output")));
            Assert.HasCount(0, Directory.GetDirectories(directory));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(peerPublic);
            CryptographicOperations.ZeroMemory(wrongPublic);
            crypto.ClearAll(recipient);
        }
    }

    private ProjectDocument CreateDocumentWithKeys()
    {
        ProjectDocument document = new();
        crypto.InitializeNew(document, "synthetic-document-password".ToCharArray().ToSecureStringAndClear());
        crypto.GenerateKeys(document);
        return document;
    }
}
