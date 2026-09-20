using System.ComponentModel.DataAnnotations;
using TM.Desktop.Services;
using TM.Desktop.ViewModels;
using TM.Services;
using ProjectItemType = TM.Entities.ProjectItemType;

namespace TM.Test;

[TestClass]
public sealed class WorkspaceServiceTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"TM.WorkspaceTests.{Guid.NewGuid():N}");
    private readonly TestProjectFileDialogs dialogs = new();
    private readonly WorkspaceService workspace;

    public WorkspaceServiceTests()
    {
        ProjectCryptoService crypto = new();
        workspace = new(new ProjectStore(crypto), crypto, dialogs);
    }

    [TestInitialize]
    public void Initialize() => Directory.CreateDirectory(directory);

    [TestCleanup]
    public void Cleanup()
    {
        workspace.Dispose();
        Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public async Task SaveLockUnlock_PreservesSecretsWithoutRetainingRevealedSnapshotAsync()
    {
        await NewAsync();
        WorkspaceViewModel root = await AddAsync(ProjectItemType.Project, "Project");
        WorkspaceViewModel item = await AddAsync(ProjectItemType.Protected, "Credentials", root.Editor!.Id);
        string itemId = item.Editor!.Id;
        await SendAsync(WorkspaceAction.Edit, command =>
        {
            command.NodeId = itemId;
            command.Name = "Credentials";
            command.Login = "synthetic-user";
            command.ReplaceSecret = true;
            command.Secret = "synthetic-secret";
        });
        WorkspaceViewModel revealed = await SendAsync(WorkspaceAction.Reveal, command => command.NodeId = itemId);
        Assert.AreEqual("synthetic-secret", revealed.RevealedPassword);
        Assert.IsNull((await workspace.SnapshotAsync()).RevealedPassword);
        dialogs.SavePath = Path.Combine(directory, "saved.xml");
        WorkspaceViewModel saved = await SendAsync(WorkspaceAction.Save);
        Assert.IsFalse(saved.IsDirty);
        Assert.IsFalse((await File.ReadAllTextAsync(dialogs.SavePath)).Contains("synthetic-secret", StringComparison.Ordinal));
        WorkspaceViewModel locked = await SendAsync(WorkspaceAction.Lock);
        Assert.IsTrue(locked.IsLocked);
        Assert.IsFalse(locked.IsLoaded);
        Assert.HasCount(0, locked.Tree);
        Assert.HasCount(0, locked.Todos);
        Assert.IsNull(locked.Editor);
        Assert.IsNull(locked.RevealedPassword);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SendAsync(WorkspaceAction.Reveal, command => command.NodeId = itemId));
        WorkspaceViewModel reopened = await SendAsync(WorkspaceAction.Unlock, command => command.Password = "test-only-password");
        Assert.IsTrue(reopened.IsLoaded);
        Assert.IsFalse(reopened.IsLocked);
        Assert.AreEqual("synthetic-secret", (await SendAsync(WorkspaceAction.Reveal, command => command.NodeId = itemId)).RevealedPassword);
    }

    [TestMethod]
    public async Task DirtyDocument_CannotBeReplacedOrLockedWithoutConfirmationAsync()
    {
        await NewAsync();
        await AddAsync(ProjectItemType.Project, "Keep me");
        dialogs.SavePath = Path.Combine(directory, "saved.xml");
        await SendAsync(WorkspaceAction.Save);
        await AddAsync(ProjectItemType.Project, "Unsaved");
        foreach (WorkspaceAction action in new[] { WorkspaceAction.New, WorkspaceAction.Open, WorkspaceAction.Lock })
            await Assert.ThrowsAsync<InvalidOperationException>(() => SendAsync(action, command => command.Password = "another-password"));
        Assert.HasCount(2, (await workspace.SnapshotAsync()).Tree);
    }

    [TestMethod]
    public async Task ConfirmedDiscardOnLock_RestoresLastSavedVersionAsync()
    {
        await NewAsync();
        await AddAsync(ProjectItemType.Project, "Saved");
        dialogs.SavePath = Path.Combine(directory, "saved.xml");
        await SendAsync(WorkspaceAction.Save);
        await AddAsync(ProjectItemType.Project, "Discard this");
        await SendAsync(WorkspaceAction.Lock, command => command.ConfirmDiscard = true);
        WorkspaceViewModel restored = await SendAsync(WorkspaceAction.Unlock, command => command.Password = "test-only-password");
        Assert.HasCount(1, restored.Tree);
        Assert.AreEqual("Saved", restored.Tree[0].Text);
    }

    [TestMethod]
    public async Task CanceledSaveAndOpen_LeaveStateAndRevisionUnchangedAsync()
    {
        await NewAsync();
        WorkspaceViewModel original = await AddAsync(ProjectItemType.Project, "Unsaved");
        WorkspaceViewModel save = await SendAsync(WorkspaceAction.Save);
        Assert.AreEqual(original.Revision, save.Revision);
        Assert.IsTrue(save.IsDirty);
        WorkspaceViewModel open = await SendAsync(WorkspaceAction.Open, command =>
        {
            command.Password = "test-only-password";
            command.ConfirmDiscard = true;
        });
        Assert.AreEqual(original.Revision, open.Revision);
        Assert.AreEqual("Unsaved", open.Tree.Single().Text);
    }

    [TestMethod]
    public async Task FailedOpen_PreservesActiveDocumentAsync()
    {
        await NewAsync();
        WorkspaceViewModel original = await AddAsync(ProjectItemType.Project, "Keep me");
        dialogs.OpenPath = Path.Combine(directory, "invalid.xml");
        await File.WriteAllTextAsync(dialogs.OpenPath, "<broken");
        await Assert.ThrowsAsync<XmlException>(() => SendAsync(WorkspaceAction.Open, command =>
        {
            command.ConfirmDiscard = true;
            command.Password = "test-only-password";
        }));
        WorkspaceViewModel after = await workspace.SnapshotAsync();
        Assert.AreEqual(original.Revision, after.Revision);
        Assert.AreEqual("Keep me", after.Tree.Single().Text);
    }

    [TestMethod]
    public async Task StaleRevision_CannotOverwriteNewerStateAsync()
    {
        await NewAsync();
        long oldRevision = (await workspace.SnapshotAsync()).Revision;
        await AddAsync(ProjectItemType.Project, "Current");
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.ExecuteAsync(new()
        {
            Action = WorkspaceAction.Add, Revision = oldRevision,
            NodeType = ProjectItemType.Project, Name = "Stale"
        }));
        Assert.HasCount(1, (await workspace.SnapshotAsync()).Tree);
    }

    [TestMethod]
    public async Task InvalidChildType_IsRejectedWithoutMutationAsync()
    {
        await NewAsync();
        WorkspaceViewModel root = await AddAsync(ProjectItemType.Project, "Root");
        WorkspaceViewModel protectedItem = await AddAsync(ProjectItemType.Protected, "Protected", root.Editor!.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AddAsync(ProjectItemType.Task, "Invalid", protectedItem.Editor!.Id));
        Assert.HasCount(0, (await workspace.SnapshotAsync()).Tree[0].Children[0].Children);
    }

    [TestMethod]
    public async Task InvalidEdit_IsRejectedBeforeChangingFieldsAsync()
    {
        await NewAsync();
        WorkspaceViewModel root = await AddAsync(ProjectItemType.Project, "Original");
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(WorkspaceAction.Edit, command =>
        {
            command.NodeId = root.Editor!.Id;
            command.Name = "Should not change";
            command.Progress = 101;
        }));
        Assert.AreEqual("Original", (await workspace.SnapshotAsync()).Tree[0].Text);
    }

    [TestMethod]
    public async Task Delete_RequiresConfirmationAndRemovesDescendantsAsync()
    {
        await NewAsync();
        WorkspaceViewModel root = await AddAsync(ProjectItemType.Project, "Root");
        string id = root.Editor!.Id;
        await AddAsync(ProjectItemType.Task, "Child", id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SendAsync(WorkspaceAction.Delete, command => command.NodeId = id));
        WorkspaceViewModel deleted = await SendAsync(WorkspaceAction.Delete, command =>
        {
            command.NodeId = id;
            command.ConfirmDelete = true;
        });
        Assert.HasCount(0, deleted.Tree);
        Assert.IsNull(deleted.Editor);
        Assert.IsTrue(deleted.IsDirty);
    }

    [TestMethod]
    public async Task Filter_RetainsMatchingAncestorsWithoutMutatingDocumentAsync()
    {
        await NewAsync();
        WorkspaceViewModel root = await AddAsync(ProjectItemType.Project, "Root");
        await AddAsync(ProjectItemType.Task, "Ångström", root.Editor!.Id);
        WorkspaceViewModel filtered = await SendAsync(WorkspaceAction.Filter, command => command.Filter = "ång");
        Assert.HasCount(1, filtered.Tree);
        Assert.AreEqual("Ångström", filtered.Tree[0].Children.Single().Text);
        Assert.HasCount(0, (await SendAsync(WorkspaceAction.Filter, command => command.Filter = "absent")).Tree);
        Assert.HasCount(1, (await SendAsync(WorkspaceAction.Filter)).Tree);
    }

    private Task<WorkspaceViewModel> NewAsync() => SendAsync(WorkspaceAction.New, command => command.Password = "test-only-password");
    private Task<WorkspaceViewModel> AddAsync(ProjectItemType type, string name, string? parent = null) =>
        SendAsync(WorkspaceAction.Add, command => { command.NodeType = type; command.Name = name; command.NodeId = parent; });
    private async Task<WorkspaceViewModel> SendAsync(WorkspaceAction action, Action<WorkspaceCommand>? configure = null)
    {
        WorkspaceCommand command = new() { Action = action, Revision = (await workspace.SnapshotAsync()).Revision };
        configure?.Invoke(command);
        return await workspace.ExecuteAsync(command);
    }
}

internal sealed class TestProjectFileDialogs : IProjectFileDialogs
{
    public string? OpenPath { get; set; }
    public string? SavePath { get; set; }
    public Task<string?> OpenAsync(CancellationToken cancellationToken = default) => Task.FromResult(OpenPath);
    public Task<string?> SaveAsync(string? currentPath, CancellationToken cancellationToken = default) => Task.FromResult(SavePath);
}
