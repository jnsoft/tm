using System.ComponentModel.DataAnnotations;
using System.Security;
using jnUtil;
using TM.Desktop.ViewModels;
using TM.Entities;
using TM.Models;
using TM.Services;

namespace TM.Desktop.Services;

public sealed class WorkspaceService(ProjectStore store, ProjectCryptoService crypto, IProjectFileDialogs dialogs,
    ExpiringClipboardService clipboard, FileToolsService fileTools) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private ProjectDocument? document;
    private string? path;
    private string? selectedId;
    private string filter = "";
    private bool dirty;
    private bool locked;
    private long revision;

    public async Task<WorkspaceViewModel> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { return Snapshot(); }
        finally { gate.Release(); }
    }

    public async Task<WorkspaceViewModel> ExecuteAsync(WorkspaceCommand command, CancellationToken cancellationToken = default)
    {
        command.Name ??= "";
        command.Description ??= "";
        command.Login ??= "";
        command.Url ??= "";
        command.Password ??= "";
        command.NewPassword ??= "";
        command.ConfirmNewPassword ??= "";
        command.Secret ??= "";
        command.Filter ??= "";
        Validator.ValidateObject(command, new ValidationContext(command), validateAllProperties: true);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (command.Revision != revision)
                throw new InvalidOperationException("The document changed. Review the refreshed view and try again.");
            string? revealed = null;
            switch (command.Action)
            {
                case WorkspaceAction.DocumentFile:
                    ProjectDocument fileDocument = RequireDocument();
                    if (path is null || dirty)
                        throw new InvalidOperationException("Save the document and all password changes before using its file key.");
                    if (!Enum.IsDefined(command.DocumentFileOperation))
                        throw new InvalidOperationException("Choose a supported document-file operation.");
                    string fileMessage = await fileTools.ExecuteDocumentAsync(fileDocument, command.DocumentFileOperation, cancellationToken);
                    return Snapshot() with { Message = fileMessage };
                case WorkspaceAction.Refresh: break;
                case WorkspaceAction.New:
                    RequireDiscard(command);
                    RequirePassword(command.Password);
                    using (SecureString password = SecurePassword(command.Password))
                        Replace(store.Create(password), null);
                    await clipboard.ClearOwnedAsync(CancellationToken.None);
                    dirty = true;
                    break;
                case WorkspaceAction.Open:
                case WorkspaceAction.Unlock:
                    RequireDiscard(command);
                    RequirePassword(command.Password);
                    string? openPath = command.Action is WorkspaceAction.Unlock && locked
                        ? path : await dialogs.OpenAsync(cancellationToken);
                    if (openPath is null) return Snapshot();
                    using (SecureString password = SecurePassword(command.Password))
                    {
                        ProjectDocumentSession opened = await store.OpenAsync(openPath, password, cancellationToken);
                        Replace(opened.Model, opened.FilePath);
                    }
                    await clipboard.ClearOwnedAsync(CancellationToken.None);
                    break;
                case WorkspaceAction.Save:
                case WorkspaceAction.SaveAs:
                    RequireDocument();
                    string? savePath = command.Action is WorkspaceAction.SaveAs || path is null
                        ? await dialogs.SaveAsync(path, cancellationToken) : path;
                    if (savePath is null) return Snapshot();
                    await store.SaveAsync(new(RequireDocument(), savePath), cancellationToken);
                    path = Path.GetFullPath(savePath);
                    dirty = false;
                    break;
                case WorkspaceAction.Lock:
                    RequireDocument();
                    RequireDiscard(command);
                    if (path is null)
                        throw new InvalidOperationException("Save the new document before locking it.");
                    store.Close(RequireDocument());
                    document = null;
                    selectedId = null;
                    dirty = false;
                    locked = true;
                    filter = "";
                    await clipboard.ClearOwnedAsync(CancellationToken.None);
                    break;
                case WorkspaceAction.Select:
                    selectedId = FindNode(command.NodeId).Id;
                    break;
                case WorkspaceAction.Add:
                    Add(command);
                    dirty = true;
                    break;
                case WorkspaceAction.Edit:
                    Edit(command);
                    dirty = true;
                    break;
                case WorkspaceAction.Delete:
                    if (!command.ConfirmDelete)
                        throw new InvalidOperationException("Confirm deletion first.");
                    NodeModel removed = FindNode(command.NodeId);
                    NodeModel? parent = removed.ParentItem;
                    RequireDocument().DeleteNode(removed);
                    parent?.UpdateProgress();
                    parent?.UpdateParentProgress();
                    selectedId = null;
                    dirty = true;
                    break;
                case WorkspaceAction.Filter:
                    filter = command.Filter;
                    break;
                case WorkspaceAction.Reveal:
                    NodeModel secret = FindNode(command.NodeId);
                    if (!secret.IsProtected) throw new InvalidOperationException("Select a protected item.");
                    selectedId = secret.Id;
                    revealed = crypto.DecryptSecret(RequireDocument(), secret.Password);
                    break;
                case WorkspaceAction.GeneratePassword:
                    GeneratePassword(command);
                    dirty = true;
                    break;
                case WorkspaceAction.CopyPassword:
                    NodeModel copied = FindNode(command.NodeId);
                    if (!copied.IsProtected) throw new InvalidOperationException("Select a protected item.");
                    string plain = crypto.DecryptSecret(RequireDocument(), copied.Password);
                    try { await clipboard.CopyAsync(plain, cancellationToken); }
                    finally { SecurityHelper.ZeroString(plain); }
                    break;
                case WorkspaceAction.ChangePassword:
                    RequireDocument();
                    RequirePassword(command.Password);
                    if (string.IsNullOrEmpty(command.NewPassword))
                        throw new InvalidOperationException("Enter a new document password.");
                    if (!string.Equals(command.NewPassword, command.ConfirmNewPassword, StringComparison.Ordinal))
                        throw new InvalidOperationException("The new passwords do not match.");
                    if (!command.ConfirmPasswordChange)
                        throw new InvalidOperationException("Acknowledge the password-change warning first.");
                    using (SecureString oldPassword = SecurePassword(command.Password))
                    using (SecureString newPassword = SecurePassword(command.NewPassword))
                        crypto.ChangeMasterPassword(RequireDocument(), oldPassword, newPassword);
                    dirty = true;
                    break;
                default: throw new InvalidOperationException("Unsupported operation.");
            }
            revision++;
            WorkspaceViewModel snapshot = Snapshot(revealed);
            return command.Action is WorkspaceAction.CopyPassword
                ? snapshot with { Message = "Password copied. TM will attempt to clear it after 15 seconds unless the clipboard changes." }
                : snapshot;
        }
        finally { gate.Release(); }
    }

    private void Add(WorkspaceCommand command)
    {
        ProjectDocument current = RequireDocument();
        string name = RequireName(command.Name);
        if (current.Nodes.Sum(node => node.AllChildNodesFlat.Count) >= 10000)
            throw new InvalidOperationException("The document has reached the editing limit.");
        NodeModel node;
        if (command.NodeType is ProjectItemType.Project)
        {
            node = new NodeModel(new Project(name));
            current.AddNode(node);
        }
        else
        {
            NodeModel parent = FindNode(command.NodeId);
            node = (command.NodeType, parent.NodeType) switch
            {
                (ProjectItemType.Milestone, ProjectItemType.Project) => new(new Milestone(name, parent.DueDate), parent),
                (ProjectItemType.Task, ProjectItemType.Project or ProjectItemType.Milestone) => new(new TM.Entities.Task(name, parent.DueDate), parent),
                (ProjectItemType.Subtask, ProjectItemType.Task or ProjectItemType.Subtask) => new(new Subtask(name, parent.DueDate), parent),
                (ProjectItemType.Protected, _) => new(new ProtectedItem(name) { Password = crypto.EncryptSecret(current, "") }, parent),
                _ => throw new InvalidOperationException("That item type cannot be added under the selected parent.")
            };
            parent.Nodes.Add(node);
            node.UpdateParentProgress();
        }
        selectedId = node.Id;
    }

    private void Edit(WorkspaceCommand command)
    {
        NodeModel node = FindNode(command.NodeId);
        string name = RequireName(command.Name);
        if (!Enum.IsDefined(command.Priority) || !Enum.IsDefined(command.Difficulty) || command.Progress is < 0 or > 100)
            throw new InvalidOperationException("Invalid scheduling values.");
        // Encrypt before mutating fields so a crypto failure cannot leave a partial edit.
        string? encrypted = node.IsProtected && command.ReplaceSecret
            ? crypto.EncryptSecret(RequireDocument(), command.Secret) : null;
        node.Text = name;
        node.Description = command.Description;
        if (node.IsProtected)
        {
            node.Login = command.Login;
            node.Url = command.Url;
            if (encrypted is not null) node.Password = encrypted;
        }
        else
        {
            node.Priority = command.Priority;
            if (node.CanEditDifficulty) node.Difficulty = command.Difficulty;
            node.DueDate = command.DueDate;
            if (node.IsLeaf) node.Progress = command.Progress;
        }
        selectedId = node.Id;
    }

    private void GeneratePassword(WorkspaceCommand command)
    {
        NodeModel node = FindNode(command.NodeId);
        if (!node.IsProtected) throw new InvalidOperationException("Select a protected item.");
        if (!command.ConfirmGeneratePassword)
            throw new InvalidOperationException("Confirm replacing the protected password first.");

        using SecureString generated = SecurityHelper.GeneratePassword(
            command.GeneratedPasswordLength, command.UseComplexGeneratedPassword);
        string plain = generated.ToInsecureString();
        try
        {
            string encrypted = crypto.EncryptSecret(RequireDocument(), plain);
            node.Password = encrypted;
            selectedId = node.Id;
        }
        finally { SecurityHelper.ZeroString(plain); }
    }

    private WorkspaceViewModel Snapshot(string? revealed = null)
    {
        if (document is null)
            return new(revision, false, locked, false, path is null ? "No document" : Path.GetFileName(path), "", [], [], null);
        document.RefreshTodos();
        NodeModel? selected = selectedId is null ? null : document.GetNodeById(selectedId);
        EditorViewModel? editor = selected is null ? null : new(selected.Id, selected.Text, selected.NodeType,
            selected.Description, selected.Login, selected.Url, selected.Priority, selected.Difficulty,
            selected.DueDate, selected.Progress, selected.IsLeaf);
        TreeItemViewModel? Map(NodeModel node)
        {
            TreeItemViewModel[] children = [.. node.Nodes.Select(Map).OfType<TreeItemViewModel>()];
            bool matches = string.IsNullOrWhiteSpace(filter)
                || node.Text.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || node.Description.Contains(filter, StringComparison.OrdinalIgnoreCase);
            return matches || children.Length > 0 ? new(node.Id, node.Text, node.NodeType, node.Id == selectedId, children) : null;
        }
        return new(revision, true, false, dirty, path is null ? "Unsaved document" : Path.GetFileName(path), filter,
            [.. document.Nodes.Select(Map).OfType<TreeItemViewModel>()],
            [.. document.Todos.Select(todo => new TodoViewModel(todo.Id, todo.Title, todo.DueDate))], editor, revealed);
    }

    private void Replace(ProjectDocument replacement, string? newPath)
    {
        if (document is not null) store.Close(document);
        document = replacement;
        path = newPath;
        selectedId = null;
        filter = "";
        dirty = false;
        locked = false;
    }

    private ProjectDocument RequireDocument() => document is { IsLocked: false } current
        ? current : throw new InvalidOperationException("Open or unlock a document first.");
    private NodeModel FindNode(string? id) => RequireDocument().GetNodeById(id ?? "")
        ?? throw new InvalidOperationException("Select an existing item.");
    private void RequireDiscard(WorkspaceCommand command)
    {
        if (dirty && !command.ConfirmDiscard)
            throw new InvalidOperationException("Save changes or explicitly allow discarding unsaved changes.");
    }
    private static string RequireName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 256
        ? name.Trim() : throw new InvalidOperationException("Enter a name of at most 256 characters.");
    private static void RequirePassword(string password)
    {
        if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("Enter the document password.");
    }
    private static SecureString SecurePassword(string password) => password.ToCharArray().ToSecureStringAndClear();
    public void Dispose()
    {
        if (document is not null) store.Close(document);
        gate.Dispose();
    }
}
