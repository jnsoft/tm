using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TM.Common;
using TM.Entities;
using TM.Helpers;
using static TM.Helpers.HashHelper;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace TM;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Point lastMouseDown;
    private TreeViewItem? draggedItem;
    private TreeViewItem? dropTarget;
    private TreeViewItem? lastSelectedTreeViewItem;
    
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SelectNodeRequested += ViewModel_SelectNodeRequested;
    }

    private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Grid grid = (Grid)sender;
        double maxWidth = e.NewSize.Width - grid.ColumnDefinitions[2].MinWidth - grid.ColumnDefinitions[1].ActualWidth;
        grid.ColumnDefinitions[0].MaxWidth = maxWidth;
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl || sender is not TabControl tabControl || tabControl.SelectedItem is not TabItem tab)
            return;

        if (tab.Header?.ToString() == "Todo" && ViewModel.IsFileLoadedState)
            ViewModel.RefreshTodos();

        e.Handled = true;
    }

    private void Todo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not ListBox || sender is not ListBox listBox || listBox.SelectedItem is not ListItemModel item)
            return;

        ViewModel.SelectTodo(item);

        if (tcTabControl.SelectedIndex == 1)
            tcTabControl.SelectedIndex = 0;

        e.Handled = true;
    }

    private void TreeViewItemSelected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem treeViewItem || treeViewItem.Header is not NodeModel node)
            return;

        lastSelectedTreeViewItem = treeViewItem;
        ViewModel.SelectedNode = node;
    }

    private void dpDueDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DatePicker datePicker || ViewModel.SelectedNode is not { ParentItem: not null } node)
            return;

        if (node.NodeType == ProjectItemType.Protected || !node.ParentItem.DueDate.HasValue)
            return;

        if (datePicker.SelectedDate.HasValue && node.ParentItem.DueDate.Value < datePicker.SelectedDate.Value)
        {
            string oldDate = datePicker.SelectedDate.Value.ToShortDateString();
            datePicker.SelectedDate = node.ParentItem.DueDate;

            MessageBox.Show(
                $"Cannot set due date of child item ({oldDate}) before due date of parent item ({node.ParentItem.DueDate.Value.ToShortDateString()}). Save to apply changes.",
                "Invalid date",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void ProjectTree_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            GetDependencyObjectFromVisualTree(source, typeof(TreeViewItem)) is TreeViewItem item &&
            item.Header is NodeModel model)
        {
            string resourceKey = model.NodeType switch
            {
                ProjectItemType.Project => "cmProjectItem",
                ProjectItemType.Milestone => "cmMilestoneItem",
                ProjectItemType.Task => "cmTaskItem",
                ProjectItemType.Subtask => "cmTaskItem",
                ProjectItemType.Protected => "cmProtectedItem",
                _ => "cmNoItem"
            };

            if (FindResource(resourceKey) is ContextMenu menu)
            {
                menu.PlacementTarget = item;
                menu.DataContext = item;
                menu.IsOpen = true;
            }

            return;
        }

        if (FindResource("cmNoItem") is ContextMenu emptyMenu)
            emptyMenu.IsOpen = true;
    }

    private void ViewModel_SelectNodeRequested(object? sender, NodeModel node)
    {
        Dispatcher.BeginInvoke(() => SelectTreeNode(node), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void SelectTreeNode(NodeModel node)
    {
        ProjectTree.UpdateLayout();

        TreeViewItem? container = GetTreeViewItem(ProjectTree, node);
        if (container is null)
            return;

        container.IsSelected = true;
        container.Focus();

        lastSelectedTreeViewItem = container;
        ViewModel.SelectedNode = node;

        Keyboard.Focus(txtName);
        txtName.SelectAll();
    }

    private static TreeViewItem? GetTreeViewItem(ItemsControl parent, object item)
    {
        if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem directContainer)
            return directContainer;

        foreach (object child in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(child) is not TreeViewItem childContainer)
                continue;

            childContainer.IsExpanded = true;
            childContainer.UpdateLayout();

            TreeViewItem? result = GetTreeViewItem(childContainer, item);
            if (result is not null)
                return result;
        }

        return null;
    }



    private void ViewPasswordButton_Checked(object sender, RoutedEventArgs e)
    {
        ViewModel.BeginPasswordEdit();

        dummyLabel.Visibility = Visibility.Collapsed;
        txtPass.Visibility = Visibility.Visible;
        txtPass.IsEnabled = true;
        dockpanelGenerate.IsEnabled = true;
        tcTabControl.IsEnabled = false;
        btEditPassword.Content = "Save";
    }

    private void ViewPasswordButton_Unchecked(object sender, RoutedEventArgs e)
    {
        ViewModel.CommitPasswordEdit();

        txtPass.IsEnabled = false;
        txtPass.Visibility = Visibility.Collapsed;
        dummyLabel.Visibility = Visibility.Visible;
        dockpanelGenerate.IsEnabled = false;
        tcTabControl.IsEnabled = true;
        btEditPassword.Content = "Edit";
    }

    private void treeView_MouseMove(object sender, MouseEventArgs e)
    {
        try
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            Point currentPosition = e.GetPosition(ProjectTree);
            if (Math.Abs(currentPosition.X - lastMouseDown.X) <= 10.0 &&
                Math.Abs(currentPosition.Y - lastMouseDown.Y) <= 10.0)
            {
                return;
            }

            draggedItem = lastSelectedTreeViewItem;
            if (draggedItem is null)
                return;

            DragDropEffects finalDropEffect = DragDrop.DoDragDrop(ProjectTree, ProjectTree.SelectedValue, DragDropEffects.Move);
            if (finalDropEffect != DragDropEffects.Move || draggedItem is null)
                return;

            if (dropTarget is null || !Equals(draggedItem.Header, dropTarget.Header))
            {
                CopyItem(draggedItem, dropTarget);
                dropTarget = null;
                draggedItem = null;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error: MouseMove", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void treeView_DragOver(object sender, DragEventArgs e)
    {
        try
        {
            Point currentPosition = e.GetPosition(ProjectTree);
            if (Math.Abs(currentPosition.X - lastMouseDown.X) > 10.0 ||
                Math.Abs(currentPosition.Y - lastMouseDown.Y) > 10.0)
            {
                TreeViewItem? item = GetNearestContainer(e.OriginalSource as UIElement);
                e.Effects = CheckDropTarget(draggedItem, item) ? DragDropEffects.Move : DragDropEffects.None;
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error: DragOver", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void treeView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            TreeViewItem? targetItem = GetNearestContainer(e.OriginalSource as UIElement);
            if (draggedItem is not null)
            {
                dropTarget = targetItem;
                e.Effects = DragDropEffects.Move;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error: Drop", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CheckDropTarget(TreeViewItem? sourceItem, TreeViewItem? targetItem)
    {
        if (sourceItem?.Header is not NodeModel source || targetItem?.Header is not NodeModel target)
            return false;

        return ViewModel.CanDrop(source, target);
    }

    private void CopyItem(TreeViewItem sourceItem, TreeViewItem? targetItem)
    {
        if (sourceItem.Header is not NodeModel source)
            return;

        NodeModel? target = targetItem?.Header as NodeModel;
        ViewModel.MoveNode(source, target);
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

    private static DependencyObject? GetDependencyObjectFromVisualTree(DependencyObject startObject, Type type)
    {
        DependencyObject? parent = startObject;

        while (parent is not null)
        {
            if (type.IsInstanceOfType(parent))
                return parent;

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private static TreeViewItem? GetNearestContainer(UIElement? element)
    {
        TreeViewItem? container = element as TreeViewItem;

        while (container is null && element is not null)
        {
            element = VisualTreeHelper.GetParent(element) as UIElement;
            container = element as TreeViewItem;
        }

        return container;
    }

    

    
}
