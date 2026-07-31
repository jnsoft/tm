using System.Reflection;
using TM.Common;
using TM.Entities;
using TM.Services;

namespace TM.Models;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string DefaultTitle = "Task manager";
    private const int SecondsToHoldPassword = 20;

    private readonly ProjectDocumentService projectDocumentService;
    private readonly SecurityToolsService securityToolsService;
    private readonly UserInteractionService interactions;
    private readonly ShellService shell;
    private readonly ProjectCryptoService crypto;

    private ProjectDocumentSession session;
    private NodeModel? selectedNode;
    private string filterText = string.Empty;
    private string editablePassword = string.Empty;
    private bool isPasswordEditorActive;

    private int generatedPasswordLength = 12;
    private bool useComplexGeneratedPassword;

    public int GeneratedPasswordLength
    {
        get => generatedPasswordLength;
        set => SetProperty(ref generatedPasswordLength, value);
    }

    public bool UseComplexGeneratedPassword
    {
        get => useComplexGeneratedPassword;
        set => SetProperty(ref useComplexGeneratedPassword, value);
    }

    public event EventHandler<NodeModel>? SelectNodeRequested;

    public MainWindowViewModel(
        ProjectDocumentService projectDocumentService,
        SecurityToolsService securityToolsService,
        UserInteractionService interactions,
        ShellService shell,
        ProjectCryptoService crypto)
    {
        this.projectDocumentService = projectDocumentService;
        this.securityToolsService = securityToolsService;
        this.interactions = interactions;
        this.shell = shell;
        this.crypto = crypto;

        session = projectDocumentService.CreateEmptySession();
        AttachDocument(session.Model);

        NewCommand = new RelayCommand(CreateNew);
        OpenCommand = new RelayCommand(Open);
        SaveCommand = new RelayCommand(Save, CanSave);
        SaveAsCommand = new RelayCommand(SaveAs, CanSave);
        ExitCommand = new RelayCommand(Exit);
        LockCommand = new RelayCommand(Lock, CanSave);
        ChangePasswordCommand = new RelayCommand(ChangePassword, CanSave);
        SaveUnencryptedCommand = new RelayCommand(SaveUnencrypted, CanSave);
        LoadUnencryptedCommand = new RelayCommand(LoadUnencrypted);

        ExpandTreeCommand = new RelayCommand(() => Document.ExpandNodes(), IsFileLoaded);
        CollapseTreeCommand = new RelayCommand(() => Document.CollapseNodes(), IsFileLoaded);
        FocusTreeItemCommand = new RelayCommand(FocusSelectedNode, IsFileLoaded);
        SortTreeByNameCommand = new RelayCommand(() => Document.SortNodes(false), IsFileLoaded);
        SortTreeByDateCommand = new RelayCommand(() => Document.SortNodes(true), IsFileLoaded);
        TimeStampCommand = new RelayCommand(AppendTimestamp, () => SelectedNode is not null && IsFileLoaded());

        FileToBase64Command = new RelayCommand(securityToolsService.EncodeFileToBase64);
        FileFromBase64Command = new RelayCommand(securityToolsService.DecodeFileFromBase64);

        EncryptFileCommand = new RelayCommand(() => securityToolsService.EncryptFile(Document), IsFileLoadedAndUnlocked);
        DecryptFileCommand = new RelayCommand(() => securityToolsService.DecryptFile(Document), IsFileLoadedAndUnlocked);
        EncryptFileSharedCommand = new RelayCommand(securityToolsService.EncryptFileWithPassword);
        DecryptFileSharedCommand = new RelayCommand(securityToolsService.DecryptFileWithPassword);
        EncryptFileAccountCommand = new RelayCommand(securityToolsService.EncryptFileForCurrentAccount);
        DecryptFileAccountCommand = new RelayCommand(securityToolsService.DecryptFileForCurrentAccount);

        GenerateCertificateCommand = new RelayCommand(() => securityToolsService.GenerateCertificate(Document), IsFileLoaded);
        ImportCertificateCommand = new RelayCommand(() => securityToolsService.ImportCertificate(Document), IsFileLoaded);
        ExportCertificateCommand = new RelayCommand(() => securityToolsService.ExportCertificate(Document), IsPkiEnabled);
        CreateSignatureCommand = new RelayCommand(() => securityToolsService.CreateSignature(Document), IsPkiEnabled);
        VerifySignatureCommand = new RelayCommand(securityToolsService.VerifySignature, IsPkiEnabled);

        GenerateKeysCommand = new RelayCommand(() => securityToolsService.GenerateKeys(Document), IsFileLoaded);
        GetPublicKeyCommand = new RelayCommand(() => securityToolsService.CopyPublicKey(Document), IsDiffieHellmanEnabled);
        EncryptWithPublicKeyCommand = new RelayCommand(() => securityToolsService.EncryptWithPublicKey(Document), IsDiffieHellmanEnabled);
        DecryptWithPrivateKeyCommand = new RelayCommand(() => securityToolsService.DecryptWithPrivateKey(Document), IsDiffieHellmanEnabled);

        HashCommand = new RelayCommand<string>(algorithm => securityToolsService.ComputeHash(algorithm ?? string.Empty));
        HmacCommand = new RelayCommand<string>(
            algorithm => securityToolsService.CreateHmac(Document, algorithm ?? string.Empty),
            _ => IsFileLoadedAndUnlocked());
        VerifyHmacCommand = new RelayCommand<string>(
            algorithm => securityToolsService.VerifyHmac(Document, algorithm ?? string.Empty),
            _ => IsFileLoadedAndUnlocked());

        PurgeCommand = new RelayCommand(securityToolsService.PurgeFile);

        CopyPasswordCommand = new RelayCommand(CopySelectedPassword, CanCopyProtectedFields);
        CopyLoginCommand = new RelayCommand(CopySelectedLogin, CanCopyProtectedFields);
        CopyUrlCommand = new RelayCommand(CopySelectedUrl, CanCopyProtectedFields);

        AddProjectCommand = new RelayCommand(AddProjectAndSelect, IsFileLoaded);

        AddTaskCommand = new RelayCommand<NodeModel>(
            parent => AddChildAndSelect(parent, p => new NodeModel(new Entities.Task("New Task", p.DueDate), p)),
            parent => parent is not null);

        AddMilestoneCommand = new RelayCommand<NodeModel>(
            parent => AddChildAndSelect(parent, p => new NodeModel(new Milestone("New Milestone", p.DueDate), p)),
            parent => parent is not null);

        AddSubtaskCommand = new RelayCommand<NodeModel>(
            parent => AddChildAndSelect(parent, p => new NodeModel(new Subtask("New Subtask", p.DueDate), p)),
            parent => parent is not null);

        AddProtectedCommand = new RelayCommand<NodeModel>(
            parent => AddChildAndSelect(parent, p => new NodeModel(new ProtectedItem("New Protected"), p)),
            parent => parent is not null);

        DeleteNodeCommand = new RelayCommand<NodeModel>(
            node =>
            {
                if (node is not null)
                    DeleteNode(node);
            },
            node => node is not null);

        GeneratePasswordCommand = new RelayCommand(GeneratePassword);
    }

    
    public ProjectDocument Document => session.Model;
    public string CurrentFilePath => session.FilePath;
    public ObservableCollection<NodeModel> Nodes => Document.Nodes;
    public ObservableCollection<ListItemModel> Todos => Document.Todos;
    public bool IsFileLoadedState => Document.IsFileLoaded;
    public bool IsLocked => Document.IsLocked;
    public string WindowTitle => Document.IsFileLoaded
        ? Path.GetFileName(CurrentFilePath)
        : $"{DefaultTitle} {GetRunningVersion()}".Trim();

    public string FilterText
    {
        get => filterText;
        set
        {
            if (!SetProperty(ref filterText, value))
                return;

            Document.FilterNodes(value);
        }
    }


    public NodeModel? SelectedNode
    {
        get => selectedNode;
        set
        {
            if (!SetProperty(ref selectedNode, value))
                return;

            RefreshCommandStates();
        }
    }

    public string EditablePassword
    {
        get => editablePassword;
        set => SetProperty(ref editablePassword, value);
    }

    public bool IsPasswordEditorActive
    {
        get => isPasswordEditorActive;
        private set
        {
            if (!SetProperty(ref isPasswordEditorActive, value))
                return;

            RefreshCommandStates();
        }
    }

    #region Commands

    public RelayCommand NewCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand ExitCommand { get; }
    public RelayCommand LockCommand { get; }
    public RelayCommand ChangePasswordCommand { get; }
    public RelayCommand SaveUnencryptedCommand { get; }
    public RelayCommand LoadUnencryptedCommand { get; }

    public RelayCommand ExpandTreeCommand { get; }
    public RelayCommand CollapseTreeCommand { get; }
    public RelayCommand FocusTreeItemCommand { get; }
    public RelayCommand SortTreeByNameCommand { get; }
    public RelayCommand SortTreeByDateCommand { get; }
    public RelayCommand TimeStampCommand { get; }

    public RelayCommand FileToBase64Command { get; }
    public RelayCommand FileFromBase64Command { get; }
    public RelayCommand EncryptFileCommand { get; }
    public RelayCommand DecryptFileCommand { get; }
    public RelayCommand EncryptFileSharedCommand { get; }
    public RelayCommand DecryptFileSharedCommand { get; }
    public RelayCommand EncryptFileAccountCommand { get; }
    public RelayCommand DecryptFileAccountCommand { get; }

    public RelayCommand GenerateCertificateCommand { get; }
    public RelayCommand ImportCertificateCommand { get; }
    public RelayCommand ExportCertificateCommand { get; }
    public RelayCommand CreateSignatureCommand { get; }
    public RelayCommand VerifySignatureCommand { get; }

    public RelayCommand GenerateKeysCommand { get; }
    public RelayCommand GetPublicKeyCommand { get; }
    public RelayCommand EncryptWithPublicKeyCommand { get; }
    public RelayCommand DecryptWithPrivateKeyCommand { get; }

    public RelayCommand<string> HashCommand { get; }
    public RelayCommand<string> HmacCommand { get; }
    public RelayCommand<string> VerifyHmacCommand { get; }
    public RelayCommand PurgeCommand { get; }

    public RelayCommand CopyPasswordCommand { get; }
    public RelayCommand CopyLoginCommand { get; }
    public RelayCommand CopyUrlCommand { get; }

    public RelayCommand AddProjectCommand { get; }
    public RelayCommand<NodeModel> AddTaskCommand { get; }
    public RelayCommand<NodeModel> AddMilestoneCommand { get; }
    public RelayCommand<NodeModel> AddSubtaskCommand { get; }
    public RelayCommand<NodeModel> AddProtectedCommand { get; }
    public RelayCommand<NodeModel> DeleteNodeCommand { get; }
    public RelayCommand GeneratePasswordCommand { get; }

    #endregion

    #region Methods for working with nodes
    public NodeModel AddProject()
    {
        NodeModel node = new(new Project("New Project"));
        Document.AddNode(node);
        return node;
    }

    public NodeModel AddTask(NodeModel parent) =>
        AddChild(parent, () => new NodeModel(new Entities.Task("New Task", parent.DueDate), parent));

    public NodeModel AddMilestone(NodeModel parent) =>
        AddChild(parent, () => new NodeModel(new Milestone("New Milestone", parent.DueDate), parent));

    public NodeModel AddSubtask(NodeModel parent) =>
        AddChild(parent, () => new NodeModel(new Subtask("New Subtask", parent.DueDate), parent));

    public NodeModel AddProtected(NodeModel parent) =>
        AddChild(parent, () => new NodeModel(new ProtectedItem("New Protected"), parent));

    public bool DeleteNode(NodeModel node)
    {
        bool confirmed = interactions.Confirm(
            $"Are you sure you want to delete {node.Text}?",
            "Delete project item");

        if (!confirmed)
            return false;

        node.Progress = 100;
        Document.DeleteNode(node);

        if (ReferenceEquals(SelectedNode, node))
            SelectedNode = null;

        return true;
    }

    public bool CanDrop(NodeModel source, NodeModel target)
    {
        bool dropToProtected = target.NodeType == ProjectItemType.Protected && source.NodeType != ProjectItemType.Protected;
        return source.Id != target.Id && !target.IsChildOf(source) && !dropToProtected;
    }

    public void MoveNode(NodeModel source, NodeModel? target)
    {
        try
        {
            if (target is not null && !target.IsChildOf(source))
            {
                bool confirmed = interactions.Confirm(
                    $"Move {source.Text} ({source.NodeType.ToString().ToLowerInvariant()}) into {target.Text} ({target.NodeType.ToString().ToLowerInvariant()})",
                    "Move project item");

                if (!confirmed)
                    return;

                NodeModel newSource = source.DeepCopy(!source.IsProtected, target);
                target.Nodes.Add(newSource);
                target.IsExpanded = true;
                target.UpdateProgress();
                target.UpdateParentProgress();
                newSource.CheckForNewDueDate();
                Document.DeleteNode(source);
                return;
            }

            if (source.IsProtected)
                return;

            bool newProjectConfirmed = interactions.Confirm(
                $"Make new project from {source.Text} ({source.NodeType.ToString().ToLowerInvariant()})",
                "New project");

            if (!newProjectConfirmed)
                return;

            Document.AddNode(source.DeepCopy(!source.IsProtected, null));
            Document.DeleteNode(source);
        }
        catch (Exception ex)
        {
            interactions.ShowError(ex);
        }
    }

    #endregion

    public void BeginPasswordEdit()
    {
        if (SelectedNode is null)
            return;

        EditablePassword = string.IsNullOrWhiteSpace(SelectedNode.Password)
            ? string.Empty
            : crypto.DecryptSecret(Document, SelectedNode.Password);

        IsPasswordEditorActive = true;
    }

    public void CommitPasswordEdit()
    {
        if (SelectedNode is null)
            return;

        SelectedNode.Password = crypto.EncryptSecret(Document, EditablePassword);
        SecurityHelper.ZeroString(EditablePassword);
        EditablePassword = string.Empty;
        IsPasswordEditorActive = false;
    }

    public void CancelPasswordEdit()
    {
        SecurityHelper.ZeroString(EditablePassword);
        EditablePassword = string.Empty;
        IsPasswordEditorActive = false;
    }

    public void SelectTodo(ListItemModel item)
    {
        NodeModel? node = Document.GetNodeById(item.Id);
        if (node is null)
            return;

        Document.FocusNode(node);
        SelectedNode = node;
    }

    public void RefreshTodos()
    {
        Document.RefreshTodos();
        OnPropertyChanged(nameof(Todos));
    }

    private NodeModel AddChild(NodeModel parent, Func<NodeModel> createNode)
    {
        NodeModel child = createNode();
        parent.Nodes.Add(child);
        child.UpdateParentProgress();
        return child;
    }

    private void CreateNew()
    {
        ProjectDocumentSession? newSession = projectDocumentService.CreateNewSession(!Document.IsEmpty);
        if (newSession is not null)
            SetSession(newSession);
    }

    private void Open()
    {
        ProjectDocumentSession? newSession = projectDocumentService.OpenSession();
        if (newSession is not null)
            SetSession(newSession);
    }

    private void Save() => projectDocumentService.SaveSession(session);

    private void SaveAs()
    {
        ProjectDocumentSession? newSession = projectDocumentService.SaveSessionAs(session);
        if (newSession is not null)
            SetSession(newSession);
    }

    private void Lock() => crypto.Lock(Document);

    private void ChangePassword()
    {
        if (!interactions.TryGetPassword("Change password", "Enter old password:", out SecureString oldPassword))
            return;

        if (!interactions.TryGetPassword("Change password", "Enter new password:", out SecureString newPassword))
            return;

        bool changed = crypto.ChangeMasterPassword(Document, oldPassword, newPassword);

        if (changed)
            interactions.ShowInfo("Password changed successfully", "Password");
        else
            interactions.ShowWarning("Could not change password", "Password");
    }

    private void SaveUnencrypted() => projectDocumentService.SaveUnencryptedSession(Document);

    private void LoadUnencrypted()
    {
        ProjectDocumentSession? newSession = projectDocumentService.LoadUnencryptedSession();
        if (newSession is not null)
            SetSession(newSession);
    }

    private void Exit()
    {
        crypto.ClearAll(Document);
        shell.Shutdown();
    }

    private void FocusSelectedNode()
    {
        FilterText = string.Empty;
        Document.FilterNodes(string.Empty);

        if (SelectedNode is not null)
            Document.FocusNode(SelectedNode);
    }

    private void AppendTimestamp()
    {
        if (SelectedNode is null)
            return;

        string stamp = $"{DateTime.Today.ToIsoDate(false)} {DateTime.Now.ToShortTimeString()}";
        SelectedNode.Description = string.IsNullOrWhiteSpace(SelectedNode.Description)
            ? stamp
            : $"{SelectedNode.Description}{Environment.NewLine}{stamp}";
    }

    private void CopySelectedPassword()
    {
        if (SelectedNode is { IsProtected: true })
            shell.CopyToClipboard(crypto.DecryptSecret(Document, SelectedNode.Password), SecondsToHoldPassword);
    }

    private void CopySelectedLogin()
    {
        if (SelectedNode is { IsProtected: true })
            shell.CopyToClipboard(SelectedNode.Login, SecondsToHoldPassword * 10);
    }

    private void CopySelectedUrl()
    {
        if (SelectedNode is { IsProtected: true })
            shell.CopyToClipboard(SelectedNode.Url, SecondsToHoldPassword * 10);
    }

    private bool CanSave() => Document.IsFileLoaded && !Document.IsLocked && !IsPasswordEditorActive;
    private bool IsFileLoaded() => Document.IsFileLoaded;
    private bool IsFileLoadedAndUnlocked() => Document.IsFileLoaded && !Document.IsLocked;
    private bool IsPkiEnabled() => Document.IsPkiEnabled;
    private bool IsDiffieHellmanEnabled() => Document.IsDiffieHellmanEnabled;
    private bool CanCopyProtectedFields() => SelectedNode is { IsProtected: true } && IsFileLoadedAndUnlocked();

    private void SetSession(ProjectDocumentSession newSession)
    {
        if (!ReferenceEquals(session.Model, newSession.Model))
            session.Model.PropertyChanged -= DocumentPropertyChanged;

        session = newSession;
        SelectedNode = null;
        FilterText = string.Empty;
        CancelPasswordEdit();
        AttachDocument(session.Model);

        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(Nodes));
        OnPropertyChanged(nameof(Todos));
        OnPropertyChanged(nameof(IsFileLoadedState));
        RefreshCommandStates();
    }

    private void AttachDocument(ProjectDocument document)
    {
        document.PropertyChanged -= DocumentPropertyChanged;
        document.PropertyChanged += DocumentPropertyChanged;

        document.Security.PropertyChanged -= SecurityPropertyChanged;
        document.Security.PropertyChanged += SecurityPropertyChanged;
    }

    private void DocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectDocument.IsFileLoaded))
        {
            OnPropertyChanged(nameof(IsFileLoadedState));
            OnPropertyChanged(nameof(WindowTitle));
        }

        if (e.PropertyName is nameof(ProjectDocument.Todos))
            OnPropertyChanged(nameof(Todos));

        if (e.PropertyName is nameof(ProjectDocument.Nodes))
            OnPropertyChanged(nameof(Nodes));

        if (e.PropertyName is nameof(ProjectDocument.IsFileLoaded)
            or nameof(ProjectDocument.IsLocked))
        {
            RefreshCommandStates();
        }
    }

    private void SecurityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectCryptoState.PublicKey)
            or nameof(ProjectCryptoState.CaCertificate)
            or nameof(ProjectCryptoState.IsDiffieHellmanEnabled)
            or nameof(ProjectCryptoState.IsPkiEnabled))
        {
            RefreshCommandStates();
        }
    }

    private void RefreshCommandStates()
    {
        SaveCommand.RaiseCanExecuteChanged();
        SaveAsCommand.RaiseCanExecuteChanged();
        LockCommand.RaiseCanExecuteChanged();
        ChangePasswordCommand.RaiseCanExecuteChanged();
        SaveUnencryptedCommand.RaiseCanExecuteChanged();

        ExpandTreeCommand.RaiseCanExecuteChanged();
        CollapseTreeCommand.RaiseCanExecuteChanged();
        FocusTreeItemCommand.RaiseCanExecuteChanged();
        SortTreeByNameCommand.RaiseCanExecuteChanged();
        SortTreeByDateCommand.RaiseCanExecuteChanged();
        TimeStampCommand.RaiseCanExecuteChanged();

        EncryptFileCommand.RaiseCanExecuteChanged();
        DecryptFileCommand.RaiseCanExecuteChanged();

        GenerateCertificateCommand.RaiseCanExecuteChanged();
        ImportCertificateCommand.RaiseCanExecuteChanged();
        ExportCertificateCommand.RaiseCanExecuteChanged();
        CreateSignatureCommand.RaiseCanExecuteChanged();
        VerifySignatureCommand.RaiseCanExecuteChanged();

        GenerateKeysCommand.RaiseCanExecuteChanged();
        GetPublicKeyCommand.RaiseCanExecuteChanged();
        EncryptWithPublicKeyCommand.RaiseCanExecuteChanged();
        DecryptWithPrivateKeyCommand.RaiseCanExecuteChanged();

        HmacCommand.RaiseCanExecuteChanged();
        VerifyHmacCommand.RaiseCanExecuteChanged();

        CopyPasswordCommand.RaiseCanExecuteChanged();
        CopyLoginCommand.RaiseCanExecuteChanged();
        CopyUrlCommand.RaiseCanExecuteChanged();
    }

    private static string GetRunningVersion()
    {
        try
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? string.Empty : $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private void AddProjectAndSelect() => SelectNode(AddProject());

    private void AddChildAndSelect(NodeModel? parent, Func<NodeModel, NodeModel> createNode)
    {
        if (parent is null)
            return;

        SelectNode(AddChild(parent, () => createNode(parent)));
    }

    private void SelectNode(NodeModel node)
    {
        Document.FocusNode(node);
        SelectedNode = node;
        SelectNodeRequested?.Invoke(this, node);
    }

    private void GeneratePassword()
    {
        using SecureString password = SecurityHelper.GeneratePassword(
            GeneratedPasswordLength,
            UseComplexGeneratedPassword);

        EditablePassword = password.ToInsecureString();
    }
}
