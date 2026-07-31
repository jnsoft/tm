using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TM.Entities;
using TM.Helpers;
using static TM.Helpers.HashHelper;

namespace TM;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    #region Const / Props

    private const string DEFAULT_TITLE = "Task manager";
    private const string DEFAULT_FILENAME = "projects.xml";
    private const string FILE_OPEN_ERROR = "Failed to open file";
    private const int SECONDS_TO_HOLD_PASSWORD = 20;

    public static string DefaultPath => Directory.GetCurrentDirectory() + "\\" + DEFAULT_FILENAME;

    public bool SaveEnabled => CanExecuteSave();

    private MainWindowModel? viewModel;
    public MainWindowModel ViewModel => viewModel ??= new MainWindowModel();

    private string xmlFilePath = DefaultPath;
    public string XmlFilePath
    {
        get => xmlFilePath;
        private set
        {
            if (xmlFilePath == value)
                return;

            xmlFilePath = value;
            OnPropertyChanged(nameof(XmlFilePath));
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string WindowTitle =>
    $" {(viewModel is { IsFileLoaded: true } ? Path.GetFileName(XmlFilePath) : DEFAULT_TITLE + " " + getRunningVersion())}";


    // drag & drop
    private Point _lastMouseDown;
    private TreeViewItem? draggedItem, _target;
    private TreeViewItem? lastSelectedTreeViewItem;

    #region Commands

    public static RelayCommand? NewCommand { get; private set; }
    public static RelayCommand? OpenCommand { get; private set; }
    public static RelayCommand? SaveCommand { get; private set; }
    public static RelayCommand? SaveAsCommand { get; private set; }
    public static RelayCommand? ExitCommand { get; private set; }
    public static RelayCommand? LockCommand { get; private set; }
    public static RelayCommand? ChangePasswordCommand { get; private set; }
    public static RelayCommand? SaveUnencryptedCommand { get; private set; }
    public static RelayCommand? LoadUnencryptedCommand { get; private set; }


    public static RelayCommand? ExpandTreeCommand { get; private set; }
    public static RelayCommand? CollapseTreeCommand { get; private set; }
    public static RelayCommand? FocusTreeItemCommand { get; private set; }
    public static RelayCommand? SortTreeByNameCommand { get; private set; }
    public static RelayCommand? SortTreeByDateCommand { get; private set; }
    public static RelayCommand? TimeStampCommand { get; private set; }

    public static RelayCommand? FileToBase64Command { get; private set; }
    public static RelayCommand? FileFromBase64Command { get; private set; }

    public static RelayCommand? EncryptFileCommand { get; private set; }
    public static RelayCommand? DecryptFileCommand { get; private set; }
    public static RelayCommand? EncryptFileSharedCommand { get; private set; }
    public static RelayCommand? DecryptFileSharedCommand { get; private set; }
    public static RelayCommand? EncryptFileAccountCommand { get; private set; }
    public static RelayCommand? DecryptFileAccountCommand { get; private set; }

    public static RelayCommand? GenerateCertificateCommand { get; private set; }
    public static RelayCommand? ImportCertificateCommand { get; private set; }
    public static RelayCommand? ExportCertificateCommand { get; private set; }
    public static RelayCommand? CreateSignatureCommand { get; private set; }
    public static RelayCommand? VerifySignatureCommand { get; private set; }

    public static RelayCommand? GenerateKeysCommand { get; private set; }
    public static RelayCommand? GetPublicKeyCommand { get; private set; }
    public static RelayCommand? EncryptWithPublicKeyCommand { get; private set; }
    public static RelayCommand? DecryptWithPrivateKeyCommand { get; private set; }


    public static RelayCommand? HashCommand { get; private set; }
    public static RelayCommand? PurgeCommand { get; private set; }




    #endregion

    #endregion



    public MainWindow()
    {
        InitializeComponent(); // next executes Main_Loaded event handler if defined
        InitializeCommands();
        SetViewModel(new MainWindowModel());
    }

    

    #region Helpers

    private void SetViewModel(MainWindowModel newViewModel)
    {
        if (viewModel is not null)
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;

        viewModel = newViewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        DataContext = viewModel;
        OnPropertyChanged(nameof(WindowTitle));
        RefreshCommandStates();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowModel.IsFileLoaded))
            OnPropertyChanged(nameof(WindowTitle));

        if (e.PropertyName is nameof(MainWindowModel.IsFileLoaded)
            or nameof(MainWindowModel.IsLocked)
            or nameof(MainWindowModel.PublicKey)
            or nameof(MainWindowModel.CA_Certificate))
        {
            RefreshCommandStates();
        }
    }

    private static void ShowInfo(string message, string title = "Info") =>
    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    private static void ShowWarning(string message, string title = "Warning") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    private static void ShowError(string message, string title = "Error") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    private static void ShowError(Exception ex, string title = "Error") =>
        ShowError(ex.Message, title);

    private static bool TryGetOpenFilePath(
    string title,
    out string path,
    string fileTypes = "",
    string fileTypeEndingFilter = "") =>
    FileHelper.GetFileName(out path, title, fileTypes, fileTypeEndingFilter);

    private static bool TryGetSaveFilePath(
        string title,
        out string path,
        string fileTypes = "",
        string fileTypeEndingFilter = "") =>
        FileHelper.SetFileName(out path, title, fileTypes, fileTypeEndingFilter);

    private static bool TryGetPassword(string title, string prompt, out SecureString password) =>
        WpfDialogHelper.GetPassword(title, prompt, out password);

    private static void ExecuteWithUiErrorHandling(
        Action action,
        string cryptographicErrorMessage,
        string cryptographicTitle = "Encryption error")
    {
        try
        {
            action();
        }
        catch (CryptographicException)
        {
            ShowWarning(cryptographicErrorMessage, cryptographicTitle);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private RelayCommand?[] GetStateAwareCommands() =>
    [
        SaveCommand,
        SaveAsCommand,
        LockCommand,
        ChangePasswordCommand,
        SaveUnencryptedCommand,
        EncryptFileCommand,
        DecryptFileCommand,
        GenerateCertificateCommand,
        ImportCertificateCommand,
        ExportCertificateCommand,
        CreateSignatureCommand,
        VerifySignatureCommand,
        GenerateKeysCommand,
        GetPublicKeyCommand,
        EncryptWithPublicKeyCommand,
        DecryptWithPrivateKeyCommand
    ];

    private void RefreshCommandStates() => RaiseCanExecuteChanged(GetStateAwareCommands());

    private static RelayCommand CreateCommand(Action execute, Func<bool>? canExecute = null) =>
    canExecute is null ? new RelayCommand(execute) : new RelayCommand(execute, canExecute);

    private static void RaiseCanExecuteChanged(params RelayCommand?[] commands)
    {
        foreach (RelayCommand? command in commands)
            command?.RaiseCanExecuteChanged();
    }

    private static bool TryGetTreeNodeFromMenuItem(object sender, out TreeViewItem treeViewItem, out NodeModel node)
    {
        treeViewItem = null!;
        node = null!;

        if (sender is not MenuItem
            {
                DataContext: TreeViewItem
                {
                    Header: NodeModel headerNode
                } item
            })
        {
            return false;
        }

        treeViewItem = item;
        node = headerNode;
        return true;
    }

    private void InitializeCommands()
    {
        NewCommand = CreateCommand(New);
        OpenCommand = CreateCommand(Open);
        SaveCommand = CreateCommand(Save, CanExecuteSave);
        SaveAsCommand = CreateCommand(SaveAs, CanExecuteSave);
        LockCommand = CreateCommand(Lock, CanExecuteSave);
        ChangePasswordCommand = CreateCommand(ChangePassword, CanExecuteSave);
        SaveUnencryptedCommand = CreateCommand(SaveUnencrypted, CanExecuteSave);
        LoadUnencryptedCommand = CreateCommand(LoadUnencrypted);
        ExitCommand = CreateCommand(Exit);

        ExpandTreeCommand = CreateCommand(ExpandTree, IsFileLoaded);
        CollapseTreeCommand = CreateCommand(CollapseTree, IsFileLoaded);
        FocusTreeItemCommand = CreateCommand(FocusTree, IsFileLoaded);
        SortTreeByNameCommand = CreateCommand(SortTreeByName, IsFileLoaded);
        SortTreeByDateCommand = CreateCommand(SortTreeByDate, IsFileLoaded);
        TimeStampCommand = CreateCommand(TimeStamp, IsFileLoaded);

        FileToBase64Command = CreateCommand(FileToBase64);
        FileFromBase64Command = CreateCommand(FileFromBase64);

        EncryptFileCommand = CreateCommand(EncryptFile, IsFileLoadedAndUnlocked);
        DecryptFileCommand = CreateCommand(DecryptFile, IsFileLoadedAndUnlocked);
        EncryptFileSharedCommand = CreateCommand(EncryptFilePassword);
        DecryptFileSharedCommand = CreateCommand(DecryptFilePassword);
        EncryptFileAccountCommand = CreateCommand(EncryptFileAccount);
        DecryptFileAccountCommand = CreateCommand(DecryptFileAccount);

        GenerateCertificateCommand = CreateCommand(GenerateCertificate, IsFileLoaded);
        ImportCertificateCommand = CreateCommand(ImportCertificate, IsFileLoaded);
        ExportCertificateCommand = CreateCommand(ExportCertificate, IsPKIEnabled);
        CreateSignatureCommand = CreateCommand(CreateSignature, IsPKIEnabled);
        VerifySignatureCommand = CreateCommand(VerifySignature, IsPKIEnabled);

        GenerateKeysCommand = CreateCommand(GenerateKeys, IsFileLoaded);
        GetPublicKeyCommand = CreateCommand(GetPublicKey, IsDiffieHellmanEnabled);
        EncryptWithPublicKeyCommand = CreateCommand(EncryptWithPublicKey, IsDiffieHellmanEnabled);
        DecryptWithPrivateKeyCommand = CreateCommand(DecryptWithPrivateKey, IsDiffieHellmanEnabled);

        PurgeCommand = CreateCommand(PurgeFile);
    }

    private void Fire() => RefreshCommandStates();

    private static DependencyObject? GetDependencyObjectFromVisualTree(DependencyObject startObject, Type type)
    {
        var parent = startObject;
        while (parent != null)
        {
            if (type.IsInstanceOfType(parent))
                break;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return parent;
    }

    private static void CheckForChanges()
    {
        var focusedElement = Keyboard.FocusedElement as FrameworkElement;

        if (focusedElement is TextBox)
        {
            var expression = focusedElement.GetBindingExpression(TextBox.TextProperty);
            if (expression != null)
                expression.UpdateSource();
        }
    }

    private static void Collapse(TreeViewItem item)
    {
        if (item.HasItems && item.Items.Count > 0)
        {
            foreach (NodeModel n in item.Items)
            {
                TreeViewItem t = (TreeViewItem)item.ItemContainerGenerator.ContainerFromItem(n);
                Collapse(t);
            }

        }


        // Collapse item if expanded.
        if (item.IsExpanded)
            item.IsExpanded = false;

    }



    private NodeModel? GetSelectedItem()
    {
        if (ProjectTree.HasItems && ProjectTree.SelectedValue != null)
            return ProjectTree.SelectedItem as NodeModel;
        else
            return null;
    }

    private static void InsertTextInTextBox(TextBox textBox, string s)
    {

        if (textBox.IsSelectionActive)
        {
            textBox.SelectedText = s;
            textBox.SelectionLength = 0;
        }

        else if (textBox.IsFocused)
        {
            textBox.Text = textBox.Text.Insert(textBox.CaretIndex, s);
            textBox.SelectedText = s;
        }

        textBox.CaretIndex += s.Length;
        int lineIndex = textBox.GetLineIndexFromCharacterIndex(textBox.CaretIndex);
        textBox.ScrollToLine(lineIndex);

    }

    private string getRunningVersion()
    {
        try
        {
            Version? v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch (Exception)
        {
            return "";
        }
    }


    #endregion

    #region Visual tree

    private bool CheckDropTarget(TreeViewItem? _sourceItem, TreeViewItem? _targetItem)
    {
        bool _isEqual = false;
        if (_sourceItem != null && _targetItem != null)
        {
            NodeModel source = (NodeModel)_sourceItem.Header;
            NodeModel target = (NodeModel)_targetItem.Header;

            //Check whether the target item is meeting your condition

            bool DropToProtected = target.NodeType == ProjectItemType.Protected && source.NodeType != ProjectItemType.Protected;

            if (source.Id != target.Id && !target.IsChildOf(source) && !DropToProtected)
                _isEqual = true;
        }
        return _isEqual;
    }

    
    // with itemsSource for treeview
    private void CopyItem(TreeViewItem _sourceItem, TreeViewItem? _targetItem)
    {
        NodeModel source = (NodeModel)_sourceItem.Header;
        NodeModel? target = null;
        if (_targetItem != null)
            target = (NodeModel)_targetItem.Header;

        if (source != null && target != null && !target.IsChildOf(source))
        {
            //Asking user wether he want to drop the dragged TreeViewItem here or not
            if (MessageBox.Show($"Move {source.Text} ({source.NodeType.ToString().ToLower()}) into {target.Text} ({target.NodeType.ToString().ToLower()})", "Move project item", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    //adding dragged TreeViewItem in target TreeViewItem
                    NodeModel newSource = source.DeepCopy(!source.IsProtected, target);
                    target.Nodes.Add(newSource);
                    target.IsExpanded = true;
                    target.UpdateProgress();
                    target.UpdateParentProgress();
                    newSource.CheckForNewDueDate();

                    ViewModel.DeleteNode(source);
                }
                catch (Exception err)
                {
                    MessageBox.Show(err.Message, "Error: Move", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else if (source != null && target == null && !source.IsProtected)
        {
            if (MessageBox.Show($"Make new project from {source.Text} ({source.NodeType.ToString().ToLower()})", "New project", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    ViewModel.AddNode(source.DeepCopy(!source.IsProtected, target));
                    ViewModel.DeleteNode(source);
                }
                catch (Exception err)
                {
                    MessageBox.Show(err.Message, "Error: New project", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    // without itemsSource for treeview
    public void addChild(TreeViewItem _sourceItem, TreeViewItem _targetItem)
    {
        // add item in target TreeViewItem 
        TreeViewItem item1 = new TreeViewItem();
        item1.Header = _sourceItem.Header;
        _targetItem.Items.Add(item1);
        foreach (TreeViewItem item in _sourceItem.Items)
        {
            addChild(item, item1);
        }
    }

    private static TObject? FindVisualParent<TObject>(UIElement? child) where TObject : UIElement
    {
        if (child == null)
        {
            return null;
        }

        UIElement? parent = VisualTreeHelper.GetParent(child) as UIElement;

        while (parent != null)
        {
            TObject? found = parent as TObject;
            if (found != null)
            {
                return found;
            }
            else
            {
                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
        }

        return null;
    }

    private static TreeViewItem? GetNearestContainer(UIElement? element)
    {
        // Walk up the element tree to the nearest tree view item.
        TreeViewItem? container = element as TreeViewItem;
        while ((container == null) && (element != null))
        {
            element = VisualTreeHelper.GetParent(element) as UIElement;
            container = element as TreeViewItem;
        }
        return container;
    }


    #endregion

    #region Event handlers


    private void Main_Loaded(object sender, RoutedEventArgs e)
    {
        //  do anything on startup?
    }

    private void HashFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi)
            return;
        string selection = mi.Header.ToString()?.ToUpperInvariant() ?? string.Empty;
        string hashOutputFile = "";

        if (FileHelper.GetFileName(out string path, $"Select file to {selection}"))
        {
            if (selection.Equals("MD5 hash".ToUpperInvariant()))
                hashOutputFile = HashHelper.MD5File(path);
            if (selection.Equals("SHA1 hash".ToUpperInvariant()))
                hashOutputFile = HashHelper.Sha1File(path);
            if (selection.Equals("SHA256 hash".ToUpperInvariant()))
                hashOutputFile = HashHelper.Sha256File(path);
            if (selection.Equals("SHA384 hash".ToUpperInvariant()))
                hashOutputFile = HashHelper.Sha384File(path);
            if (selection.Equals("SHA512 hash".ToUpperInvariant()))
                hashOutputFile = HashHelper.Sha512File(path);

            MessageBox.Show($"Successfully hashed {path} to {hashOutputFile}", "Hash calculated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void HmacFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi)
            return;
        string selection = mi.Header.ToString()?.ToUpperInvariant() ?? string.Empty;
        string hmacOutputFile = "";

        if (FileHelper.GetFileName(out string path, $"Select file to {selection}"))
        {
            byte[] salt = SecurityHelper.GetRandomKey(HashHelper.SALT_LEN);
            byte[] key = ViewModel.DeriveKey("HMAC", salt, 64);

            if (selection.Equals("MD5 HMAC".ToUpperInvariant()))
                hmacOutputFile = HashHelper.MD5SignFile(key, path);
            if (selection.Equals("SHA1 HMAC".ToUpperInvariant()))
                hmacOutputFile = HashHelper.Sha1SignFile(key, path);
            if (selection.Equals("SHA256 HMAC".ToUpperInvariant()))
                hmacOutputFile = HashHelper.Sha256SignFile(key, path);
            if (selection.Equals("SHA384 HMAC".ToUpperInvariant()))
                hmacOutputFile = HashHelper.Sha384SignFile(key, path);
            if (selection.Equals("SHA512 HMAC".ToUpperInvariant()))
                hmacOutputFile = HashHelper.Sha512SignFile(key, path);

            File.AppendAllText(hmacOutputFile, "\n" + salt.ToBase64(), System.Text.Encoding.UTF8);

            MessageBox.Show($"Successfully signed {path} to {hmacOutputFile}", "HMAC calculated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void HmacVerifyFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi)
            return;
        string selection = mi.Header.ToString()?.ToUpperInvariant() ?? string.Empty;
        bool verified = false;

        if (FileHelper.GetFileName(out string path, $"Select file to {selection}"))
        {
            if (FileHelper.GetFileName(out string macPath, $"Select stored HMAC file"))
            {
                string macFile = File.ReadAllText(macPath);

                var lines = macFile.SplitToLines();
                if (lines.Length < 2)
                {
                    MessageBox.Show(
                        $"Invalid HMAC file format in:\n{macPath}\n\nExpected 2 lines:\n1) HMAC\n2) Base64 salt",
                        "HMAC verification error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }


                byte[] mac;
                try
                {
                    mac = lines[0].FromPrettyPrint();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Invalid HMAC value in:\n{macPath}\n\n{ex.Message}",
                        "HMAC verification error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                byte[] salt;
                try
                {
                    salt = lines[1].FromBase64();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Invalid salt value in:\n{macPath}\n\n{ex.Message}",
                        "HMAC verification error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                byte[] key = ViewModel.DeriveKey("HMAC", salt, 64);


                if (selection.Equals("Verify MD5 HMAC".ToUpperInvariant()))
                    verified = HashHelper.MD5VerifyFile(key, path, mac);
                if (selection.Equals("Verify SHA1 HMAC".ToUpperInvariant()))
                    verified = HashHelper.Sha1VerifyFile(key, path, mac);
                if (selection.Equals("Verify SHA256 HMAC".ToUpperInvariant()))
                    verified = HashHelper.Sha256VerifyFile(key, path, mac);
                if (selection.Equals("Verify SHA384 HMAC".ToUpperInvariant()))
                    verified = HashHelper.Sha384VerifyFile(key, path, mac);
                if (selection.Equals("Verify SHA512 HMAC".ToUpperInvariant()))
                    verified = HashHelper.Sha512VerifyFile(key, path, mac);

                if (verified)
                    MessageBox.Show($"Successfully verified {path}", "HMAC verified", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show($"Could not verify {path}", "HMAC verification error", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
        }
    }

    private void dpDueDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DatePicker dp)
            return;
        NodeModel? n = ProjectTree.SelectedItem as NodeModel;

        if (n?.ParentItem != null && n.ParentItem.DueDate.HasValue && n.NodeType != ProjectItemType.Protected)
        {
            if (!dp.SelectedDate.HasValue || n.ParentItem.DueDate.Value < dp.SelectedDate)
            {
                string oldDate = dp.SelectedDate.HasValue
                    ? dp.SelectedDate.Value.ToShortDateString()
                    : string.Empty;
                dp.SelectedDate = n.ParentItem.DueDate;
                MessageBox.Show($"Cannot set due date of child item ({oldDate}) before due date of parent item ({n.ParentItem.DueDate.Value.ToShortDateString()}). Save to apply changes.", "Invalid date", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void ProjectTree_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        CheckForChanges();
        if (e.OriginalSource is DependencyObject obj &&
        GetDependencyObjectFromVisualTree(obj, typeof(TreeViewItem)) is TreeViewItem item)
        {

            NodeModel model = (NodeModel)item.Header;

            string resourceKey = model.NodeType switch
            {
                ProjectItemType.Project => "cmProjectItem",
                ProjectItemType.Milestone => "cmMilestoneItem",
                ProjectItemType.Task => "cmTaskItem",
                ProjectItemType.Subtask => "cmTaskItem",
                ProjectItemType.Protected => "cmProtectedItem",
                _ => "cmNoItem"

            };

            if (this.FindResource(resourceKey) is ContextMenu cm)
            {
                cm.PlacementTarget = sender as TreeViewItem;
                cm.DataContext = item;
                cm.IsOpen = true;
            }
        }
        else
        {
            if (this.FindResource("cmNoItem") is ContextMenu cm)
                cm.IsOpen = true;
        }
    }

    
    private void MenuItem_AddProject(object sender, RoutedEventArgs e)
    {
        NodeModel node = new NodeModel(new Project("New Project"));
        ViewModel.AddNode(node);

        if (ProjectTree.ItemContainerGenerator.ContainerFromItem(node) is TreeViewItem treeViewItem)
            FocusTreeItemForEdit(treeViewItem);
    }

    private void FocusNewChild(TreeViewItem parentTreeViewItem)
    {
        parentTreeViewItem.Focus();
        parentTreeViewItem.ExpandSubtree();
        Collapse(parentTreeViewItem);
        parentTreeViewItem.IsExpanded = true;

        int childIndex = parentTreeViewItem.Items.Count - 1;
        if (parentTreeViewItem.ItemContainerGenerator.ContainerFromIndex(childIndex) is TreeViewItem newItem)
            FocusTreeItemForEdit(newItem);
    }

    private void FocusTreeItemForEdit(TreeViewItem treeViewItem)
    {
        treeViewItem.Focus();
        Keyboard.Focus(txtName);
        txtName.SelectAll();
    }

    private void AddChildNode(object sender, Func<NodeModel, NodeModel> createNode)
    {
        if (!TryGetTreeNodeFromMenuItem(sender, out TreeViewItem parentTreeViewItem, out NodeModel parent))
            return;

        NodeModel child = createNode(parent);
        parent.Nodes.Add(child);
        child.UpdateParentProgress();

        FocusNewChild(parentTreeViewItem);
    }

    private void MenuItem_AddTask(object sender, RoutedEventArgs e) =>
    AddChildNode(sender, parent => new NodeModel(new Entities.Task("New Task", parent.DueDate), parent));

    private void MenuItem_AddMilestone(object sender, RoutedEventArgs e) =>
        AddChildNode(sender, parent => new NodeModel(new Milestone("New Milestone", parent.DueDate), parent));

    private void MenuItem_AddSubtask(object sender, RoutedEventArgs e) =>
        AddChildNode(sender, parent => new NodeModel(new Subtask("New Subtask", parent.DueDate), parent));

    private void MenuItem_AddProtected(object sender, RoutedEventArgs e) =>
        AddChildNode(sender, parent => new NodeModel(new ProtectedItem("New Protected"), parent));

    private void MenuItem_Delete(object sender, RoutedEventArgs e)
    {
        if (!TryGetTreeNodeFromMenuItem(sender, out _, out NodeModel node))
            return;

        MessageBoxResult result = MessageBox.Show(
            $"Are you sure you want to delete {node.Text}?",
            "Delete project item",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        node.Progress = 100; // "finish" node before deleting
        ViewModel.DeleteNode(node);
    }

    private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var g = (Grid)sender;
        Double maxW = e.NewSize.Width - g.ColumnDefinitions[2].MinWidth - g.ColumnDefinitions[1].ActualWidth;
        g.ColumnDefinitions[0].MaxWidth = maxW;
    }

    private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        string text = ((TextBox)sender).Text;
        ViewModel.FilterNodes(text);
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is TabControl)
        {
            if (sender is TabControl tc && tc.SelectedItem is TabItem tab)
            {
                if (tab.Header.ToString() == "Todo" && ViewModel.IsFileLoaded)
                    ViewModel.FireTodos();
            }
        }

        e.Handled = true;
    }

    private void Todo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is ListBox)
        {
            if (sender is ListBox lb && lb.SelectedItem is ListItemModel item)
            {
                NodeModel? node = ViewModel.GetNodeById(item.Id);
                if (node == null)
                    return;

                node.ExpandParents();
                node.IsSelected = true;

                if (tcTabControl.SelectedIndex == 1)
                    tcTabControl.SelectedIndex = 0;

                e.Handled = true;
            }
        }
    }

    // to keep track of selected treeViewItem, not just selected NodeModel when using HierarchicalDataTemplate
    private void TreeViewItemSelected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem tvi)
        {
            this.lastSelectedTreeViewItem = tvi;
        }
    }

    #endregion

    #region ProtectedItem event handlers

    private void btCopyPassword_Click(object sender, RoutedEventArgs e)
    {
        NodeModel? item = GetSelectedItem();
        if (item is null)
            return;
        ClipBoardHelper.LoadClipBoard(ViewModel.DecryptSecret(item.Password), SECONDS_TO_HOLD_PASSWORD);
    }

    private void btCopyLogin_Click(object sender, RoutedEventArgs e) => ClipBoardHelper.LoadClipBoard(txtLogin.Text, SECONDS_TO_HOLD_PASSWORD * 10);
    private void btCopyUrl_Click(object sender, RoutedEventArgs e) => ClipBoardHelper.LoadClipBoard(txtUrl.Text, SECONDS_TO_HOLD_PASSWORD * 10);

    private void ViewPasswordButton_Checked(object sender, RoutedEventArgs e)
    {
        dummyLabel.Visibility = Visibility.Collapsed;
        txtPass.Visibility = Visibility.Visible;
        txtPass.IsEnabled = true;
        tcTabControl.IsEnabled = false;
        NodeModel? item = GetSelectedItem();
        if (item != null && !string.IsNullOrWhiteSpace(item.Password))
        {
            try
            {
                txtPass.Text = ViewModel.DecryptSecret(item.Password);
            }
            catch (Exception)
            {
                MessageBox.Show("Password decryption failed, resetting password.", "Decryption failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPass.Text = "";
            }
        }
        else
            txtPass.Text = "";

        dockpanelGenerate.IsEnabled = true;
        btEditPassword.Content = "Save";
    }

    private void ViewPasswordButton_Unchecked(object sender, RoutedEventArgs e)
    {
        NodeModel? item = GetSelectedItem();
        if (item != null)
        {
            item.Password = ViewModel.EncryptSecret(txtPass.Text);


            SecurityHelper.ZeroString(txtPass.Text);
            txtPass.Text = "";
            txtPass.IsEnabled = false;
            txtPass.Visibility = Visibility.Collapsed;
            dummyLabel.Visibility = Visibility.Visible;
            dockpanelGenerate.IsEnabled = false;
            tcTabControl.IsEnabled = true;
            btEditPassword.Content = "Edit";
        }
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        SecureString ss = SecurityHelper.GeneratePassword((int)slPassLen.Value, chkComplex.IsChecked.HasValue ? chkComplex.IsChecked.Value : false);
        txtPass.Text = ss.ToInsecureString();
        ss.Dispose();
    }

    #endregion

    #region Drag and drop event handlers

    private void treeView_MouseMove(object sender, MouseEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(ProjectTree);

                if ((Math.Abs(currentPosition.X - _lastMouseDown.X) > 10.0) ||
                    (Math.Abs(currentPosition.Y - _lastMouseDown.Y) > 10.0))
                {
                    draggedItem = lastSelectedTreeViewItem; //FindTreeViewSelectedItemContainer(ProjectTree, (NodeModel)ProjectTree.SelectedItem);

                    if (draggedItem != null)
                    {
                        DragDropEffects finalDropEffect = DragDrop.DoDragDrop(ProjectTree, ProjectTree.SelectedValue, DragDropEffects.Move);
                        //Checking target is not null and item is dragging(moving)
                        if(finalDropEffect == DragDropEffects.Move) //  && (_target != null))
                        {
                            if (draggedItem is null)
                                return;
                            // A Move drop was accepted
                            if (_target == null || !string.Equals(draggedItem.Header.ToString(), _target.Header.ToString()))
                            {
                                CopyItem(draggedItem, _target);
                                _target = null;
                                draggedItem = null;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception err)
        {
            MessageBox.Show(err.Message, "Error: MouseMove", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void treeView_DragOver(object sender, DragEventArgs e)
    {
        try
        {
            Point currentPosition = e.GetPosition(ProjectTree);

            if ((Math.Abs(currentPosition.X - _lastMouseDown.X) > 10.0) ||
               (Math.Abs(currentPosition.Y - _lastMouseDown.Y) > 10.0))
            {
                // Verify that this is a valid drop and then store the drop target
                TreeViewItem? item = GetNearestContainer(e.OriginalSource as UIElement);
                if (CheckDropTarget(draggedItem, item)) // no drop on self
                    e.Effects = DragDropEffects.Move;
                else
                    e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        catch (Exception err)
        {
            MessageBox.Show(err.Message, "Error: DragOver", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void treeView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            // Verify that this is a valid drop and then store the drop target
            TreeViewItem? TargetItem = GetNearestContainer(e.OriginalSource as UIElement);
            //if (TargetItem != null && draggedItem != null)
            if (draggedItem != null)
            {
                _target = TargetItem;
                e.Effects = DragDropEffects.Move;
            }
            else if (draggedItem != null)
            {
                TreeView? TargetItem2 = FindVisualParent<TreeView>(e.OriginalSource as UIElement);
                if (TargetItem2 != null)
                    e.Effects = DragDropEffects.Move;
            }
        }
        catch (Exception err)
        {
            MessageBox.Show(err.Message, "Error: Drop", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Can Execute?

    private bool CanExecuteSave() => ViewModel.IsFileLoaded && !ViewModel.IsLocked && btEditPassword.IsChecked != true;

    private bool IsFileLoaded() => ViewModel.IsFileLoaded;

    private bool IsFileLoadedAndUnlocked() => ViewModel.IsFileLoaded && !ViewModel.IsLocked;

    private bool IsPKIEnabled() => ViewModel.IsPKIenabled;

    private bool IsDiffieHellmanEnabled() => ViewModel.IsDiffieHellmanEnabled;


    #endregion

    #region File menu: New/Open/Save/Close...

    private void New()
    {
        bool okToClear = ViewModel == null || ViewModel.IsEmpty;

        if (!okToClear)
        {
            MessageBoxResult result = MessageBox.Show("Click OK to clear the project tree", "Clear project tree", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            okToClear = result == MessageBoxResult.OK;
        }

        if (okToClear)
        {
            if (WpfDialogHelper.GetPassword("New collection", "Set password:", out SecureString password))
            {
                ViewModel?.Nodes.Clear();

                XmlFilePath = DefaultPath;
                if (File.Exists(XmlFilePath))
                    XmlFilePath = Directory.GetCurrentDirectory() + "\\" + DateTime.Now.Ticks.ToString() + ".xml";

                MainWindowModel newViewModel = new(password)
                {
                    IsFileLoaded = true
                };

                SetViewModel(newViewModel);
            }
        }
    }

    private void Open()
    {
        string? tPath = null;
        if (FileHelper.GetFileName(out tPath, "Open project file", "Xml Files", "xml"))
        {
            try
            {
                if (WpfDialogHelper.GetPassword("Password", "Enter password", out SecureString password))
                {
                    try
                    {
                        XmlDocument doc = XMLhelper.XmlFromFile(tPath);
                        SetViewModel(new MainWindowModel(doc, password));

                        lastSelectedTreeViewItem = null;
                        draggedItem = null;
                        _target = null;
                        _lastMouseDown = new Point();

                        dpPass.IsEnabled = true;

                        XmlFilePath = tPath;
                        OnPropertyChanged(nameof(WindowTitle));

                        Fire();

                        MessageBox.Show($"{XmlFilePath} loaded", "Load file", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (CryptographicException)
                    {
                        MessageBox.Show("Decryption failed", FILE_OPEN_ERROR, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Could not parse XML", FILE_OPEN_ERROR, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch
            {
                MessageBox.Show($"Could not load {tPath}. Please check file.", "Load file", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Save() // remember to update integration test if updated!
    {
        CheckForChanges(); // force source update from textbox without focus_lost event

        //XmlDocument pki = null;
        //if (ViewModel.IsPKIenabled)
        //    pki = FileHelper.CreateCertStoreXML(ViewModel.CA_Certificate, ViewModel.MasterPassword);

        XMLhelper.XmlToFile(ViewModel.GetAsEncryptedXML().DocumentElement, XmlFilePath);


        foreach (NodeModel n in ViewModel.Nodes)
            n.ResetSave(); // This is not used??

        MessageBoxResult result = MessageBox.Show("Project tree saved to + " + XmlFilePath, "Save file", MessageBoxButton.OK, MessageBoxImage.Information);

    }

    private void SaveAs()
    {
        string? fPath = null;
        if (FileHelper.SetFileName(out fPath, "Save project file", "Xml Files", "xml"))
        {
            XmlFilePath = fPath;
            Save();
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    private void Lock()
    {
        dpPass.IsEnabled = false;
        ViewModel.Lock();
        Fire();
    }

    private void ChangePassword()
    {
        if (WpfDialogHelper.GetPassword("Change password", "Enter old password:", out SecureString oldPassword))
        {
            if (WpfDialogHelper.GetPassword("Change password", "Enter new password:", out SecureString newPassword))
            {
                CheckForChanges(); // force source update from textbox without focus_lost event
                if (ViewModel.ChangeMasterPassword(oldPassword, newPassword))
                {
                    lastSelectedTreeViewItem = null;
                    draggedItem = null;
                    _target = null;
                    _lastMouseDown = new Point();
                    MessageBox.Show($"Password changed successfully", "Password", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                    MessageBox.Show($"Could not change password", "Password", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void SaveUnencrypted()
    {
        if (FileHelper.SetFileName(out string path, "Save unencrypted project file", "Save Files", "sav"))
        {
            if (WpfDialogHelper.GetPassword("Password", "Enter password:", out SecureString pass))
            {
                CheckForChanges(); // force source update from textbox without focus_lost event

                try
                {
                    XmlDocument doc = ViewModel.GetUnencryptedXML(pass);
                    byte[] key = SecurityHelper.GetRandomKey(32);
                    string fileContent = SecurityHelper.GCMEncrypt(doc.InnerXml.ToByte(), key).ToBase64();
                    File.WriteAllText(path, fileContent);
                    string str_key = key.ToBase64();
                    MessageBox.Show($"Unencrypted projects file saved to {path}.\n\n To unlock file, use key:\n {str_key}\n\n(Press Ctrl+C to copy message box contents)", "Save file", MessageBoxButton.OK, MessageBoxImage.Information);
                    SecurityHelper.ZeroString(str_key);
                    ArrayHelper.ClearArray(ref key);
                }
                catch (Exception)
                {
                    MessageBox.Show($"Could not save unencrypted", "Save unencrypted", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

            }
        }
    }

    private void LoadUnencrypted()
    {
        if (FileHelper.GetFileName(out string fPath, "Open unencrypted file", "Save Files", "sav"))
        {
            if (WpfDialogHelper.GetPassword("Set password", "Set new password:", out SecureString password))
            {
                try
                {
                    WpfDialogHelper.GetPassword("Unlock file", "Enter unlock key:", out SecureString strKey);

                    byte[] bytekey = strKey.ToInsecureString().FromBase64();
                    string fileStringContent = File.ReadAllText(fPath);
                    byte[] fileContent = SecurityHelper.GCMDecrypt(fileStringContent.FromBase64(), bytekey);
                    XmlDocument doc = new XmlDocument();
                    string xml = fileContent.ToStringFromByte();
                    doc.LoadXml(xml);

                    SetViewModel(new MainWindowModel(password));
                    ViewModel.LoadUnencrypted(doc);
                    ViewModel.EncryptProtectedItemsAfterLoadingUnencryptedProjects();

                    if (File.Exists(XmlFilePath))
                        XmlFilePath = Directory.GetCurrentDirectory() + "\\" + DateTime.Now.Ticks.ToString() + ".xml";


                    ViewModel.IsFileLoaded = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not load {fPath}. Please check file. {ex.Message}", "Load unencrypted file", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void Exit()
    {
        ViewModel.ClearAll();
        Application.Current.Shutdown();
    }

    #endregion

    #region Common commands

    private void ExpandTree() => ViewModel.ExpandNodes();

    private void CollapseTree()
    {
        CheckForChanges();
        ViewModel.CollapseNodes();
    }

    private void FocusTree()
    {
        NodeModel? n = GetSelectedItem();
        txtFilter.Text = string.Empty;
        ViewModel.FilterNodes(string.Empty);
        if (n != null)
            ViewModel.FocusNode(n);
    }

    private void SortTreeByName()
    {
        CheckForChanges();
        ViewModel.SortNodes(false);
    }

    private void SortTreeByDate()
    {
        CheckForChanges();
        ViewModel.SortNodes(true);
    }

    private void TimeStamp() => InsertTextInTextBox(txtDescription, DateTime.Today.ToIsoDate(false) + " " + DateTime.Now.ToShortTimeString());


    #endregion

    #region Util commands

    private void FileToBase64()
    {
        if (FileHelper.GetFileName(out string path, "Select file to base64 encode"))
        {
            try
            {
                SecurityHelper.Base64EncodeFile(path, path + ".b64");
                MessageBox.Show($"{path} successfully encoded");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    private void FileFromBase64()
    {
        if (FileHelper.GetFileName(out string path, "Select base64 file to decode", "Base 64", "b64"))
        {
            try
            {
                string extension = Path.GetExtension(path);
                string newFilename = path;

                if (extension == ".b64")
                    newFilename = path.Substring(0, path.Length - extension.Length);
                else
                    newFilename = path + ".decoded";

                SecurityHelper.Base64DeccodeFile(path, newFilename);
                MessageBox.Show($"{path} successfully decoded");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void EncryptFile()
    {
        if (FileHelper.GetFileName(out string path, "Select file to encrypt"))
        {
            byte[]? key = null;
            try
            {
                byte[] salt = SecurityHelper.GetRandomKey(MainWindowModel.SALT_LEN);
                key = ViewModel.DeriveKey("file encryption", salt);

                if (FileHelper.EncryptFile(path, ref key, salt))
                    MessageBox.Show($"{path} successfully encrypted");
            }
            catch (CryptographicException)
            {
                MessageBox.Show($"Could not encrypt {path}", "Encryption error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MainWindowModel.ClearArr(ref key);
            }
        }
    }
    private void DecryptFile()
    {
        if (FileHelper.GetFileName(out string path, "Select file to decrypt"))
        {
            byte[]? key = null;
            try
            {
                byte[] salt = FileHelper.ReadSaltFromFile(path);
                key = ViewModel.DeriveKey("file encryption", salt);

                if (FileHelper.DecryptFile(path, ref key))
                    MessageBox.Show($"{path} successfully decrypted");
            }
            catch (CryptographicException)
            {
                MessageBox.Show($"Could not decrypt {path}", "Encryption error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                MainWindowModel.ClearArr(ref key);
            }
        }
    }
    private void EncryptFilePassword()
    {
        if (!TryGetOpenFilePath("Select file to encrypt", out string path))
            return;

        ExecuteWithUiErrorHandling(
            () =>
            {
                if (!TryGetPassword("Encrypt file", "Enter password:", out SecureString pass))
                    return;

                using (pass)
                {
                    if (FileHelper.EncryptFile(path, pass))
                        ShowInfo($"{path} successfully encrypted");
                }
            },
            $"Could not encrypt {path}");
    }
    private void DecryptFilePassword()
    {
        if (!TryGetOpenFilePath("Select file to decrypt", out string path))
            return;

        ExecuteWithUiErrorHandling(
            () =>
            {
                if (!TryGetPassword("Decrypt file", "Enter password", out SecureString pass))
                    return;

                using (pass)
                {
                    if (FileHelper.DecryptFile(path, pass))
                        ShowInfo($"{path} successfully decrypted");
                }
            },
            $"Could not decrypt {path}");
    }
    private void EncryptFileAccount()
    {
        if (!TryGetOpenFilePath("Select file to encrypt", out string path))
            return;

        ExecuteWithUiErrorHandling(
            () =>
            {
                if (FileHelper.EncryptFile(path))
                    ShowInfo($"{path} successfully encrypted");
            },
            $"Could not encrypt {path}");
    }
    private void DecryptFileAccount()
    {
        if (!TryGetOpenFilePath("Select file to decrypt", out string path))
            return;

        ExecuteWithUiErrorHandling(
            () =>
            {
                if (FileHelper.DecryptFile(path))
                    ShowInfo($"{path} successfully decrypted");
            },
            $"Could not decrypt {path}");
    }

    private void GenerateCertificate()
    {
        ViewModel.EnsureCAcert();
        Fire();
    }

    private void ImportCertificate()
    {
        const string header = "Import certificate";

        if (!TryGetOpenFilePath(header, out string path, "Pfx files", "pfx"))
            return;

        if (!TryGetPassword(header, "Password for pfx private key:", out SecureString pfxPass))
            return;

        using (pfxPass)
        {
            try
            {
                ViewModel.CA_Certificate = X509Helper.LoadPfxFromFile(path, pfxPass);
                RefreshCommandStates();
                ShowInfo($"Successfully imported {path}", header);
            }
            catch (Exception ex)
            {
                ShowWarning(ex.Message, "Error");
            }
        }
    }

    private void ExportCertificate()
    {
        if(!TryGetSaveFilePath("Export certificate", out string path, "Certificate", "cer"))
        return;

        ViewModel.EnsureCAcert();
        X509Helper.SaveX509ToCerFile(ViewModel.CA_Certificate, path);
        ShowInfo($"Certificate saved to {path}", "Certificate");
    }

    private void CreateSignature()
    {
        if (FileHelper.GetFileName(out string path, "Select file to sign"))
        {
            try
            {
                byte[] signature = ViewModel.SignFile(path);

                string signatureFileName = path + ".p7c";
                if (File.Exists(signatureFileName))
                    File.Delete(signatureFileName);

                File.WriteAllBytes(signatureFileName, signature);

                Fire();

                MessageBox.Show($"{signatureFileName} successfully written");
            }

            catch (Exception ex)
            {
                MessageBox.Show($"{path} could not be signed: " + ex.Message);
            }
        }
    }

    private void VerifySignature()
    {

        if (FileHelper.GetFileName(out string path, "Select signature to verify", "PKCS7 signature", "p7c"))
        {
            string fname = path.Substring(0, path.Length - 4);
            if (!path.Substring(path.Length - 4).Equals(".p7c") || !File.Exists(fname))
                FileHelper.GetFileName(out fname, "Select signed file to verify");

            try
            {
                byte[] dataToVerify = File.ReadAllBytes(fname);
                byte[] signature = File.ReadAllBytes(path);
                bool ok = CMSHelper.Verify(dataToVerify, signature, false);

                if (ok)
                    MessageBox.Show($"{fname} successfully verified!");

                else
                {
                    ok = CMSHelper.Verify(dataToVerify, signature, true);
                    if (ok)
                        MessageBox.Show($"Warning! Couldn't verify certificate chain for {path}", "Certificate validation failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                if (ok)
                    return;
                else
                    MessageBox.Show($"{fname} could not be verified!", "Failed validation", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            catch (Exception ex)
            {
                MessageBox.Show($"{fname} could not be verified!" + ex.Message, "Error in validation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void GenerateKeys()
    {
        ViewModel.GenerateNewPKIpair();
        Fire();
    }
    private void GetPublicKey()
    {
        if (ViewModel.PublicKey is not null)
            ClipBoardHelper.LoadClipBoard(ViewModel.PublicKey, 300);
    }
    private void EncryptWithPublicKey()
    {
        if (FileHelper.GetFileName(out string path, "Select file to encrypt"))
        {
            try
            {
                if (WpfDialogHelper.GetText("Encrypt file", "Enter recipient's public key", out string pubKey))
                {
                    string newFilename = path + ".aes";
                    if (File.Exists(newFilename))
                        File.Delete(newFilename);

                    byte[]? pk = ViewModel.GetUnprotectedPrivateKey();
                    byte[]? key = SecurityHelper.DeriveSymmetricKey(pk, pubKey.FromBase64());
                    MainWindowModel.ClearArr(ref pk);

                    byte[] encrypted = SecurityHelper.GCMEncrypt(File.ReadAllBytes(path), key);

                    MainWindowModel.ClearArr(ref key);

                    File.WriteAllBytes(newFilename, encrypted);

                    MessageBox.Show($"{newFilename} successfully encrypted");
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"{path} could not be encrypted: " + ex.Message);
            }
        }
    }
    private void DecryptWithPrivateKey()
    {
        if (FileHelper.GetFileName(out string path, "Select file to decrypt"))
        {
            try
            {
                if (WpfDialogHelper.GetText("Decrypt file", "Enter sender's public key", out string pubKey))
                {
                    string newFilename = path;
                    string extension = Path.GetExtension(path);

                    if (extension == ".aes")
                        newFilename = path.Substring(0, path.Length - extension.Length);
                    else
                        newFilename = path + ".decrypted";

                    while (File.Exists(newFilename))
                        newFilename += ".new";

                    byte[]? pk = ViewModel.GetUnprotectedPrivateKey();
                    byte[]? key = SecurityHelper.DeriveSymmetricKey(pk, pubKey.FromBase64());
                    MainWindowModel.ClearArr(ref pk);

                    byte[] decrypted = SecurityHelper.GCMDecrypt(File.ReadAllBytes(path), key);

                    MainWindowModel.ClearArr(ref key);

                    File.WriteAllBytes(newFilename, decrypted);

                    MessageBox.Show($"{newFilename} successfully decrypted");
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show($"{path} could not be decrypted: " + ex.Message);
            }
        }
    }
    private void PurgeFile()
    {
        if (FileHelper.GetFileName(out string path, "Select file to purge"))
        {
            try
            {
                SecurityHelper.WipeFile(path, 20);
                MessageBox.Show($"{path} successfully purged");
            }


            catch (Exception ex)
            {
                MessageBox.Show($"Could not purge {path}: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region INotify

    public event PropertyChangedEventHandler? PropertyChanged;


    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

