using System.Security.Cryptography;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class ProjectStoreTests
{
    private readonly ProjectCryptoService crypto = new();
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.StoreTests.{Guid.NewGuid():N}");
    private readonly List<ProjectDocument> documents = [];

    [TestInitialize]
    public void Initialize() => Directory.CreateDirectory(directory);

    [TestCleanup]
    public void Cleanup()
    {
        ProjectStore store = new(crypto);
        foreach (ProjectDocument document in documents)
            store.Close(document);
        Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public async Task SaveAndOpenAsync_PreservesSyntheticDocumentAndClearsDirtyStateAsync()
    {
        ProjectStore store = new(crypto);
        ProjectDocument source = TestDataBuilder.CreateLoadedEncryptedDocument(crypto);
        documents.Add(source);
        source.Nodes[0].Text = "Portable application boundary";
        string path = Path.Combine(directory, "roundtrip.xml");
        await store.SaveAsync(new(source, path));
        Assert.IsFalse(source.Nodes[0].IsChanged);
        ProjectDocumentSession loaded = await store.OpenAsync(path, Password("secret"));
        documents.Add(loaded.Model);
        Assert.AreEqual(Path.GetFullPath(path), loaded.FilePath);
        Assert.AreEqual(source.Nodes[0].Text, loaded.Model.Nodes[0].Text);
        NodeModel protectedNode = TestDataBuilder.GetFirstProtectedNode(loaded.Model);
        Assert.IsTrue(crypto.DecryptSecret(loaded.Model, protectedNode.Password).StartsWith("password", StringComparison.Ordinal));
        Assert.HasCount(1, Directory.GetFiles(directory));
    }

    [TestMethod]
    public async Task OpenFrozenFixtureAsync_PreservesSecretsAsync()
    {
        ProjectStore store = new(crypto);
        string path = Path.Combine(directory, "frozen.xml");
        using Stream fixture = typeof(ProjectStoreTests).Assembly
            .GetManifestResourceStream("TM.Test.Fixtures.baseline-encrypted.xml")
            ?? throw new InvalidOperationException("Missing fixture.");
        await using (FileStream output = File.Create(path))
            await fixture.CopyToAsync(output);
        ProjectDocumentSession session = await store.OpenAsync(path, Password("baseline-test-only"));
        documents.Add(session.Model);
        Assert.AreEqual("Top node 1", session.Model.Nodes[0].Text);
        Assert.IsTrue(session.Model.IsDiffieHellmanEnabled);
        Assert.IsTrue(session.Model.IsPkiEnabled);
    }

    [TestMethod]
    public async Task SaveCanceledAsync_DoesNotOverwriteExistingFileAsync()
    {
        ProjectStore store = new(crypto);
        ProjectDocument source = TestDataBuilder.CreateLoadedEncryptedDocument(crypto);
        documents.Add(source);
        string path = Path.Combine(directory, "existing.xml");
        await File.WriteAllTextAsync(path, "keep original");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(new(source, path), cancellation.Token));
        Assert.AreEqual("keep original", await File.ReadAllTextAsync(path));
        Assert.HasCount(1, Directory.GetFiles(directory));
    }

    [TestMethod]
    public async Task SaveLockedAsync_DoesNotOverwriteExistingFileAsync()
    {
        ProjectStore store = new(crypto);
        ProjectDocument source = TestDataBuilder.CreateLoadedEncryptedDocument(crypto);
        documents.Add(source);
        string path = Path.Combine(directory, "existing.xml");
        await File.WriteAllTextAsync(path, "keep original");
        crypto.Lock(source);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(new(source, path)));
        Assert.AreEqual("keep original", await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task OpenAsync_RejectsDtdAsync()
    {
        ProjectStore store = new(crypto);
        string path = Path.Combine(directory, "invalid.xml");
        await File.WriteAllTextAsync(path, "<!DOCTYPE project_store [<!ENTITY secret 'unexpected'>]><project_store>&secret;</project_store>");
        using SecureString password = Password("test");
        await Assert.ThrowsAsync<XmlException>(() => store.OpenAsync(path, password));
    }

    [TestMethod]
    public void Close_ClearsDocumentAndCertificateState()
    {
        ProjectStore store = new(crypto);
        ProjectDocument document = TestDataBuilder.CreateLoadedEncryptedDocument(crypto);
        documents.Add(document);
        crypto.EnsureCaCertificate(document);
        store.Close(document);
        Assert.IsNull(document.Security.CaCertificate);
        Assert.IsFalse(document.IsFileLoaded);
        Assert.IsTrue(document.IsLocked);
        Assert.IsTrue(document.IsEmpty);
        Assert.IsNull(document.Security.ProtectedMasterKey);
    }

    [TestMethod]
    public void CoreAssembly_HasNoWpfAssemblyReferences()
    {
        string?[] references = [.. typeof(ProjectDocument).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name)];
        Assert.AreEqual("TM.Core", typeof(ProjectDocument).Assembly.GetName().Name);
        foreach (string forbidden in new[] { "PresentationCore", "PresentationFramework", "WindowsBase", "TM" })
            Assert.DoesNotContain(forbidden, references);
    }

    [TestMethod]
    public void FilterNodes_UsesUiIndependentVisibility()
    {
        ProjectDocument document = new();
        document.LoadProjects(TestDataBuilder.CreateSampleProjects());
        document.FilterNodes("no matching name");
        Assert.IsFalse(document.Nodes[0].IsVisible);
        document.FilterNodes(string.Empty);
        Assert.IsTrue(document.Nodes[0].IsVisible);
    }

    private static SecureString Password(string value) => value.ToCharArray().ToSecureStringAndClear();
}
