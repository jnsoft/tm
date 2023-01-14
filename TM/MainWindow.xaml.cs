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

    public MainWindowModel ViewModel { get; set; }

    public string XML_File_Path = DefaultPath;
    public string WindowTitle => " " + (ViewModel != null && ViewModel.IsFileLoaded ? Path.GetFileName(XML_File_Path) : DEFAULT_TITLE + " " + getRunningVersion());


    // drag & drop
    private Point _lastMouseDown;
    private TreeViewItem draggedItem, _target;
    private TreeViewItem lastSelectedTreeViewItem;

    #region Commands

    public static RelayCommand NewCommand { get; private set; }
    public static RelayCommand OpenCommand { get; private set; }
    public static RelayCommand SaveCommand { get; private set; }
    public static RelayCommand SaveAsCommand { get; private set; }
    public static RelayCommand ExitCommand { get; private set; }
    public static RelayCommand LockCommand { get; private set; }
    public static RelayCommand ChangePasswordCommand { get; private set; }
    public static RelayCommand SaveUnencryptedCommand { get; private set; }
    public static RelayCommand LoadUnencryptedCommand { get; private set; }


    public static RelayCommand ExpandTreeCommand { get; private set; }
    public static RelayCommand CollapseTreeCommand { get; private set; }
    public static RelayCommand FocusTreeItemCommand { get; private set; }
    public static RelayCommand SortTreeByNameCommand { get; private set; }
    public static RelayCommand SortTreeByDateCommand { get; private set; }
    public static RelayCommand TimeStampCommand { get; private set; }

    public static RelayCommand FileToBase64Command { get; private set; }
    public static RelayCommand FileFromBase64Command { get; private set; }

    public static RelayCommand EncryptFileCommand { get; private set; }
    public static RelayCommand DecryptFileCommand { get; private set; }
    public static RelayCommand EncryptFileSharedCommand { get; private set; }
    public static RelayCommand DecryptFileSharedCommand { get; private set; }
    public static RelayCommand EncryptFileAccountCommand { get; private set; }
    public static RelayCommand DecryptFileAccountCommand { get; private set; }

    public static RelayCommand GenerateCertificateCommand { get; private set; }
    public static RelayCommand ImportCertificateCommand { get; private set; }
    public static RelayCommand ExportCertificateCommand { get; private set; }
    public static RelayCommand CreateSignatureCommand { get; private set; }
    public static RelayCommand VerifySignatureCommand { get; private set; }

    public static RelayCommand GenerateKeysCommand { get; private set; }
    public static RelayCommand GetPublicKeyCommand { get; private set; }
    public static RelayCommand EncryptWithPublicKeyCommand { get; private set; }
    public static RelayCommand DecryptWithPrivateKeyCommand { get; private set; }


    public static RelayCommand HashCommand { get; private set; }
    public static RelayCommand PurgeCommand { get; private set; }




    #endregion

    #endregion



    public MainWindow()
    {
        InitializeComponent(); // next executes Main_Loaded event handler if defined
        ViewModel = new MainWindowModel();

        NewCommand = new RelayCommand(new Action(New));
        OpenCommand = new RelayCommand(new Action(Open));
        SaveCommand = new RelayCommand(new Action(Save), CanExecuteSave);
        SaveAsCommand = new RelayCommand(new Action(SaveAs), CanExecuteSave);
        LockCommand = new RelayCommand(new Action(Lock), CanExecuteSave);
        ChangePasswordCommand = new RelayCommand(new Action(ChangePassword), CanExecuteSave);
        SaveUnencryptedCommand = new RelayCommand(new Action(SaveUnencrypted), CanExecuteSave);
        LoadUnencryptedCommand = new RelayCommand(new Action(LoadUnencrypted));
        ExitCommand = new RelayCommand(new Action(Exit));

        ExpandTreeCommand = new RelayCommand(new Action(ExpandTree), IsFileLoaded);
        CollapseTreeCommand = new RelayCommand(new Action(CollapseTree), IsFileLoaded);
        FocusTreeItemCommand = new RelayCommand(new Action(FocusTree), IsFileLoaded);
        SortTreeByNameCommand = new RelayCommand(new Action(SortTreeByName), IsFileLoaded);
        SortTreeByDateCommand = new RelayCommand(new Action(SortTreeByDate), IsFileLoaded);
        TimeStampCommand = new RelayCommand(new Action(TimeStamp), IsFileLoaded);

        FileToBase64Command = new RelayCommand(new Action(FileToBase64));
        FileFromBase64Command = new RelayCommand(new Action(FileFromBase64));

        EncryptFileCommand = new RelayCommand(new Action(EncryptFile), IsFileLoadedAndUnlocked);
        DecryptFileCommand = new RelayCommand(new Action(DecryptFile), IsFileLoadedAndUnlocked);
        EncryptFileSharedCommand = new RelayCommand(new Action(EncryptFilePassword));
        DecryptFileSharedCommand = new RelayCommand(new Action(DecryptFilePassword));
        EncryptFileAccountCommand = new RelayCommand(new Action(EncryptFileAccount));
        DecryptFileAccountCommand = new RelayCommand(new Action(DecryptFileAccount));

        GenerateCertificateCommand = new RelayCommand(new Action(GenerateCertificate), IsFileLoaded);
        ImportCertificateCommand = new RelayCommand(new Action(ImportCertificate), IsFileLoaded);
        ExportCertificateCommand = new RelayCommand(new Action(ExportCertificate), IsPKIEnabled);
        CreateSignatureCommand = new RelayCommand(new Action(CreateSignature), IsPKIEnabled);
        VerifySignatureCommand = new RelayCommand(new Action(VerifySignature), IsPKIEnabled);

        GenerateKeysCommand = new RelayCommand(new Action(GenerateKeys), IsFileLoaded);
        GetPublicKeyCommand = new RelayCommand(new Action(GetPublicKey), IsDiffieHellmanEnabled);
        EncryptWithPublicKeyCommand = new RelayCommand(new Action(EncryptWithPublicKey), IsDiffieHellmanEnabled);
        DecryptWithPrivateKeyCommand = new RelayCommand(new Action(DecryptWithPrivateKey), IsDiffieHellmanEnabled);

        PurgeCommand = new RelayCommand(new Action(PurgeFile));
    }


    #region Helpers

    private void Fire()
    {
        SaveCommand.RaiseCanExecuteChanged();
        SaveAsCommand.RaiseCanExecuteChanged();
        LockCommand.RaiseCanExecuteChanged();
        ChangePasswordCommand.RaiseCanExecuteChanged();
        SaveUnencryptedCommand.RaiseCanExecuteChanged();
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
    }

    private static DependencyObject GetDependencyObjectFromVisualTree(DependencyObject startObject, Type type)
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



    private NodeModel GetSelctedItem()
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
            //return ApplicationDeployment.CurrentDeployment.CurrentVersion;
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch (Exception)
        {
            return "";
        }
    }


    #endregion

    #region Visual tree

    private bool CheckDropTarget(TreeViewItem _sourceItem, TreeViewItem _targetItem)
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

    // without itemsSource for treeview
    private void _CopyItem(TreeViewItem _sourceItem, TreeViewItem _targetItem)
    {
        //Asking user wether he want to drop the dragged TreeViewItem here or not
        if (MessageBox.Show("Would you like to drop " + _sourceItem.Header.ToString() + " into " + _targetItem.Header.ToString() + "", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            try
            {
                //adding dragged TreeViewItem in target TreeViewItem
                addChild(_sourceItem, _targetItem);

                //finding Parent TreeViewItem of dragged TreeViewItem 
                TreeViewItem ParentItem = FindVisualParent<TreeViewItem>(_sourceItem);
                // if parent is null then remove from TreeView else remove from Parent TreeViewItem
                if (ParentItem == null)
                {
                    ProjectTree.Items.Remove(_sourceItem);
                }
                else
                {
                    ParentItem.Items.Remove(_sourceItem);
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error: CopyItem", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // with itemsSource for treeview
    private void CopyItem(TreeViewItem _sourceItem, TreeViewItem _targetItem)
    {
        NodeModel source = (NodeModel)_sourceItem.Header;
        NodeModel target = null;
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

    private static TObject FindVisualParent<TObject>(UIElement child) where TObject : UIElement
    {
        if (child == null)
        {
            return null;
        }

        UIElement parent = VisualTreeHelper.GetParent(child) as UIElement;

        while (parent != null)
        {
            TObject found = parent as TObject;
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

    private TreeViewItem GetNearestContainer(UIElement element)
    {
        // Walk up the element tree to the nearest tree view item.
        TreeViewItem container = element as TreeViewItem;
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
        MenuItem mi = sender as MenuItem;
        string selection = mi.Header.ToString().ToUpperInvariant();
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
        MenuItem mi = sender as MenuItem;
        string selection = mi.Header.ToString().ToUpperInvariant();
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
        MenuItem mi = sender as MenuItem;
        string selection = mi.Header.ToString().ToUpperInvariant();
        bool verified = false;

        if (FileHelper.GetFileName(out string path, $"Select file to {selection}"))
        {
            if (FileHelper.GetFileName(out string macPath, $"Select stored HMAC file"))
            {
                string macFile = File.ReadAllText(macPath);
                byte[] mac = macFile.SplitToLines()[0].FromPrettyPrint();
                byte[] salt = macFile.SplitToLines()[1].FromBase64();
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
        DatePicker dp = sender as DatePicker;
        NodeModel n = ProjectTree.SelectedItem as NodeModel;

        if (n.ParentItem != null && n.ParentItem.DueDate.HasValue && n.NodeType != ProjectItemType.Protected)
        {
            if (!dp.SelectedDate.HasValue || n.ParentItem.DueDate.Value < dp.SelectedDate)
            {
                string oldDate = dp.SelectedDate.Value.ToShortDateString();
                dp.SelectedDate = n.ParentItem.DueDate;
                MessageBox.Show($"Cannot set due date of child item ({oldDate}) before due date of parent item ({n.ParentItem.DueDate.Value.ToShortDateString()}). Save to apply changes.", "Invalid date", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void ProjectTree_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        CheckForChanges();
        DependencyObject obj = e.OriginalSource as DependencyObject;
        TreeViewItem item = GetDependencyObjectFromVisualTree(obj, typeof(TreeViewItem)) as TreeViewItem;
        if (item != null)
        {
            NodeModel model = (NodeModel)item.Header;

            ContextMenu cm = new ContextMenu();
            if (model.NodeType == ProjectItemType.Project)
                cm = this.FindResource("cmProjectItem") as ContextMenu;
            else if (model.NodeType == ProjectItemType.Milestone)
                cm = this.FindResource("cmMilestoneItem") as ContextMenu;
            else if (model.NodeType == ProjectItemType.Task || model.NodeType == ProjectItemType.Subtask)
                cm = this.FindResource("cmTaskItem") as ContextMenu;
            else if (model.NodeType == ProjectItemType.Protected)
                cm = this.FindResource("cmProtectedItem") as ContextMenu;

            cm.PlacementTarget = sender as TreeViewItem;
            cm.DataContext = item;
            cm.IsOpen = true;
        }
        else
        {
            ContextMenu cm = this.FindResource("cmNoItem") as ContextMenu;
            cm.IsOpen = true;
        }

    }

    private void MenuItem_AddProject(object sender, RoutedEventArgs e)
    {
        NodeModel n = new NodeModel(new Project("New Project"));
        ViewModel.AddNode(n);

        int children = ProjectTree.Items.Count;
        TreeViewItem t = (TreeViewItem)ProjectTree.ItemContainerGenerator.ContainerFromItem(n);
        if (t != null)
        {
            t.Focus();
            Keyboard.Focus(txtName);
            txtName.SelectAll();
        }
    }

    private void MenuItem_AddMilestone(object sender, RoutedEventArgs e)
    {
        MenuItem mi = sender as MenuItem;
        if (mi != null)
        {
            TreeViewItem t = mi.DataContext as TreeViewItem;
            NodeModel node = t.Header as NodeModel;
            if (node != null)
            {
                NodeModel n = new NodeModel(new Milestone("New Milestone", node.DueDate), node);
                node.Nodes.Add(n);
                n.UpdateParentProgress();

                t.Focus();
                t.ExpandSubtree(); // force visual objects to be created
                Collapse(t);
                t.IsExpanded = true;
                int children = t.Items.Count;
                TreeViewItem tNew = (TreeViewItem)t.ItemContainerGenerator.ContainerFromIndex(children - 1);
                tNew.Focus();
                Keyboard.Focus(txtName);
                txtName.SelectAll();
            }
        }
    }

    private void MenuItem_AddTask(object sender, RoutedEventArgs e)
    {
        MenuItem mi = sender as MenuItem;
        if (mi != null)
        {
            TreeViewItem t = mi.DataContext as TreeViewItem;
            NodeModel node = t.Header as NodeModel;
            if (node != null)
            {
                NodeModel n = new NodeModel(new Entities.Task("New Task", node.DueDate), node);
                node.Nodes.Add(n);
                n.UpdateParentProgress();

                t.Focus();
                t.ExpandSubtree(); // force visual objects to be created
                Collapse(t);
                t.IsExpanded = true;
                int children = t.Items.Count;
                TreeViewItem tNew = (TreeViewItem)t.ItemContainerGenerator.ContainerFromIndex(children - 1);
                tNew.Focus();
                Keyboard.Focus(txtName);
                txtName.SelectAll();
            }
        }
    }

    private void MenuItem_AddSubtask(object sender, RoutedEventArgs e)
    {
        MenuItem mi = sender as MenuItem;
        if (mi != null)
        {
            TreeViewItem t = mi.DataContext as TreeViewItem;
            NodeModel node = t.Header as NodeModel;
            if (node != null)
            {
                NodeModel n = new NodeModel(new Subtask("New Subtask", node.DueDate), node);
                node.Nodes.Add(n);
                n.UpdateParentProgress();

                t.Focus();
                t.ExpandSubtree(); // force visual objects to be created
                Collapse(t);
                t.IsExpanded = true;
                int children = t.Items.Count;
                TreeViewItem tNew = (TreeViewItem)t.ItemContainerGenerator.ContainerFromIndex(children - 1);
                tNew.Focus();
                Keyboard.Focus(txtName);
                txtName.SelectAll();
            }
        }
    }

    private void MenuItem_AddProtected(object sender, RoutedEventArgs e)
    {
        MenuItem mi = sender as MenuItem;
        if (mi != null)
        {
            TreeViewItem t = mi.DataContext as TreeViewItem;
            if (t != null)
            {
                NodeModel node = t.Header as NodeModel;
                if (node != null)
                {
                    NodeModel n = new NodeModel(new ProtectedItem("New Protected"), node);
                    node.Nodes.Add(n);
                    n.UpdateParentProgress();

                    t.Focus();
                    t.ExpandSubtree(); // force visual objects to be created
                    Collapse(t);
                    t.IsExpanded = true;
                    int children = t.Items.Count;
                    TreeViewItem tNew = (TreeViewItem)t.ItemContainerGenerator.ContainerFromIndex(children - 1);
                    tNew.Focus();
                    Keyboard.Focus(txtName);
                    txtName.SelectAll();
                }
            }
        }
    }

    private void MenuItem_Delete(object sender, RoutedEventArgs e)
    {
        MenuItem mi = sender as MenuItem;
        if (mi != null)
        {
            TreeViewItem t = mi.DataContext as TreeViewItem;

            NodeModel node = t.Header as NodeModel;
            if (node != null)
            {
                MessageBoxResult res = MessageBox.Show("Are you sure you want to delete " + node.Text + "?", "Delete project item", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    node.Progress = 100; // "finish" node before deleting
                    ViewModel.DeleteNode(node);
                }
            }
        }
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
            TabControl tc = sender as TabControl;
            TabItem tab = tc.SelectedItem as TabItem;
            if (tab.Header.ToString() == "Todo" && ViewModel.IsFileLoaded)
                ViewModel.FireTodos();
        }

        e.Handled = true;
    }

    private void Todo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is ListBox)
        {
            ListBox lb = sender as ListBox;
            if (lb.SelectedItem != null)
            {
                ListItemModel item = lb.SelectedItem as ListItemModel;
                NodeModel node = ViewModel.GetNodeById(item.Id);

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
        TreeViewItem tvi = e.OriginalSource as TreeViewItem;
        this.lastSelectedTreeViewItem = tvi;
    }

    #endregion

    #region ProtectedItem event handlers

    private void btCopyPassword_Click(object sender, RoutedEventArgs e) =>
        ClipBoardHelper.LoadClipBoard(
            ViewModel.DecryptSecret(GetSelctedItem().Password)
           , SECONDS_TO_HOLD_PASSWORD);

    private void btCopyLogin_Click(object sender, RoutedEventArgs e) => ClipBoardHelper.LoadClipBoard(txtLogin.Text, SECONDS_TO_HOLD_PASSWORD * 10);
    private void btCopyUrl_Click(object sender, RoutedEventArgs e) => ClipBoardHelper.LoadClipBoard(txtUrl.Text, SECONDS_TO_HOLD_PASSWORD * 10);

    private void ViewPasswordButton_Checked(object sender, RoutedEventArgs e)
    {
        dummyLabel.Visibility = Visibility.Collapsed;
        txtPass.Visibility = Visibility.Visible;
        txtPass.IsEnabled = true;
        tcTabControl.IsEnabled = false;
        if (!string.IsNullOrWhiteSpace(GetSelctedItem().Password))
        {
            try
            {
                txtPass.Text = ViewModel.DecryptSecret(GetSelctedItem().Password);
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
        GetSelctedItem().Password = ViewModel.EncryptSecret(txtPass.Text);
        SecurityHelper.ZeroString(txtPass.Text);
        txtPass.Text = "";
        txtPass.IsEnabled = false;
        txtPass.Visibility = Visibility.Collapsed;
        dummyLabel.Visibility = Visibility.Visible;
        dockpanelGenerate.IsEnabled = false;
        tcTabControl.IsEnabled = true;
        btEditPassword.Content = "Edit";
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
                        if ((finalDropEffect == DragDropEffects.Move)) //  && (_target != null))
                        {
                            // A Move drop was accepted
                            if (_target == null || !draggedItem.Header.ToString().Equals(_target.Header.ToString()))
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
                TreeViewItem item = GetNearestContainer(e.OriginalSource as UIElement);
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
            TreeViewItem TargetItem = GetNearestContainer(e.OriginalSource as UIElement);
            //if (TargetItem != null && draggedItem != null)
            if (draggedItem != null)
            {
                _target = TargetItem;
                e.Effects = DragDropEffects.Move;
            }
            else if (draggedItem != null)
            {
                TreeView TargetItem2 = FindVisualParent<TreeView>(e.OriginalSource as UIElement);
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

    private bool CanExecuteSave() => ViewModel.IsFileLoaded && !ViewModel.IsLocked && !btEditPassword.IsChecked.Value;

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
                ViewModel.Nodes.Clear();

                XML_File_Path = DefaultPath;
                if (File.Exists(XML_File_Path))
                    XML_File_Path = Directory.GetCurrentDirectory() + "\\" + DateTime.Now.Ticks.ToString() + ".xml";

                ViewModel = new MainWindowModel(password);
                ViewModel.IsFileLoaded = true;
                DataContext = ViewModel;
                OnPropertyChanged("WindowTitle");
                Fire();
            }
        }
    }

    private void Open()
    {
        string tPath = null;
        if (FileHelper.GetFileName(out tPath, "Open project file", "Xml Files", "xml"))
        {
            try
            {
                if (WpfDialogHelper.GetPassword("Password", "Enter password", out SecureString password))
                {
                    try
                    {
                        XmlDocument doc = XMLhelper.XmlFromFile(tPath);
                        ViewModel = new MainWindowModel(doc, password);

                        DataContext = ViewModel;
                        lastSelectedTreeViewItem = null;
                        draggedItem = null;
                        _target = null;
                        _lastMouseDown = new Point();

                        dpPass.IsEnabled = true;

                        XML_File_Path = tPath;
                        OnPropertyChanged("WindowTitle");

                        Fire();

                        MessageBox.Show($"{XML_File_Path} loaded", "Load file", MessageBoxButton.OK, MessageBoxImage.Information);
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

        XMLhelper.XmlToFile(ViewModel.GetAsEncryptedXML().DocumentElement, XML_File_Path);


        foreach (NodeModel n in ViewModel.Nodes)
            n.ResetSave(); // This is not used??

        MessageBoxResult result = MessageBox.Show("Project tree saved to + " + XML_File_Path, "Save file", MessageBoxButton.OK, MessageBoxImage.Information);

    }

    private void SaveAs()
    {
        string fPath = null;
        if (FileHelper.SetFileName(out fPath, "Save project file", "Xml Files", "xml"))
        {
            XML_File_Path = fPath;
            Save();
            OnPropertyChanged("WindowTitle");
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

                    ViewModel = new MainWindowModel(password);
                    ViewModel.LoadUnencrypted(doc);
                    ViewModel.EncryptProtectedItemsAfterLoadingUnencryptedProjects();

                    if (File.Exists(XML_File_Path))
                        XML_File_Path = Directory.GetCurrentDirectory() + "\\" + DateTime.Now.Ticks.ToString() + ".xml";


                    ViewModel.IsFileLoaded = true;
                    DataContext = ViewModel;
                    OnPropertyChanged("WindowTitle");
                    Fire();
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
        NodeModel n = GetSelctedItem();
        txtFilter.Text = string.Empty;
        ViewModel.FilterNodes(string.Empty);
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
            byte[] key = null;
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
            byte[] key = null;
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
        if (FileHelper.GetFileName(out string path, "Select file to encrypt"))
        {
            try
            {
                if (WpfDialogHelper.GetPassword("Encrypt file", "Enter password:", out SecureString pass))
                {
                    if (FileHelper.EncryptFile(path, pass))
                        MessageBox.Show($"{path} successfully encrypted");
                    pass.Dispose();
                }
            }
            catch (CryptographicException)
            {
                MessageBox.Show($"Could not encrypt {path}", "Encryption error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    private void DecryptFilePassword()
    {
        if (FileHelper.GetFileName(out string path, "Select file to decrypt"))
        {
            try
            {
                if (WpfDialogHelper.GetPassword("Decrypt file", "Enter password", out SecureString pass))
                {
                    if (FileHelper.DecryptFile(path, pass))
                        MessageBox.Show($"{path} successfully decrypted");
                    pass.Dispose();
                }
            }
            catch (CryptographicException)
            {
                MessageBox.Show($"Could not decrypt {path}", "Encryption error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
    private void EncryptFileAccount()
    {
        string path = null;
        if (FileHelper.GetFileName(out path, "Select file to encrypt"))
        {
            try
            {
                if (FileHelper.EncryptFile(path))
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
        }
    }
    private void DecryptFileAccount()
    {
        string path = null;
        if (FileHelper.GetFileName(out path, "Select file to decrypt"))
        {
            try
            {
                if (FileHelper.DecryptFile(path))
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
        }
    }

    private void GenerateCertificate()
    {
        ViewModel.EnsureCAcert();
        Fire();
    }

    private void ImportCertificate()
    {
        try
        {
            string header = "Import certificate";
            if (FileHelper.GetFileName(out string path, header, "Pfx files", "pfx"))
            {
                WpfDialogHelper.GetPassword(header, "Password for pfx private key:", out SecureString pfxPass);

                if (pfxPass != null)
                    ViewModel.CA_Certificate = X509Helper.LoadPfxFromFile(path, pfxPass);
                else
                    ViewModel.CA_Certificate = X509Helper.LoadPfxFromFile(path);

                Fire();

                MessageBox.Show($"Successfully imported {path}", header, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExportCertificate()
    {
        if (FileHelper.SetFileName(out string path, "Export certificate", "Certificate", "cer"))
        {
            ViewModel.EnsureCAcert();
            X509Helper.SaveX509ToCerFile(ViewModel.CA_Certificate, path);
            MessageBox.Show($"Certificate saved to {path}", "Certificate", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        //RSA rsa = RSA.Create();
        //rsa.ImportSubjectPublicKeyInfo(PublicKey.FromBase64(), out _);
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
    private void GetPublicKey() => ClipBoardHelper.LoadClipBoard(ViewModel.PublicKey, 300);
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

                    byte[] pk = ViewModel.GetUnprotectedPrivateKey();
                    byte[] key = SecurityHelper.DeriveSymmetricKey(pk, pubKey.FromBase64());
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

                    byte[] pk = ViewModel.GetUnprotectedPrivateKey();
                    byte[] key = SecurityHelper.DeriveSymmetricKey(pk, pubKey.FromBase64());
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

    public event PropertyChangedEventHandler PropertyChanged;


    protected void OnPropertyChanged(string propertyName)
    {
        if (PropertyChanged != null)
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

