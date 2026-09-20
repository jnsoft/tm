using System.Security.Cryptography.X509Certificates;
using TM.Common;
using TM.Entities;

namespace TM.Models;

public class ProjectDocument : ObservableObject
{
    public ProjectCryptoState Security { get; } = new();

    private ObservableCollection<NodeModel> nodes = [];
    public ObservableCollection<NodeModel> Nodes
    {
        get => nodes;
        set
        {
            if (!SetProperty(ref nodes, value))
                return;

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(Todos));
        }
    }

    private ObservableCollection<ListItemModel> todos = [];
    public ObservableCollection<ListItemModel> Todos
    {
        get => todos;
        private set => SetProperty(ref todos, value);
    }

    private bool isFileLoaded;
    public bool IsFileLoaded
    {
        get => isFileLoaded;
        set => SetProperty(ref isFileLoaded, value);
    }

    private bool isLocked;
    public bool IsLocked
    {
        get => isLocked;
        set => SetProperty(ref isLocked, value);
    }

    public bool IsEmpty => Nodes.Count == 0;
    public bool IsDiffieHellmanEnabled => Security.IsDiffieHellmanEnabled;
    public bool IsPkiEnabled => Security.IsPkiEnabled;

    public ProjectDocument()
    {
        IsLocked = false;
    }

    public void LoadProjects(List<Project> projects)
    {
        SetNodesFromProjects(projects);
        IsLocked = false;
        IsFileLoaded = true;
    }

    public void LoadUnencrypted(XmlDocument unencryptedXml)
    {
        List<XmlNode> projects = unencryptedXml.ChildNodes.FindAllNodesByName("project", true, true);
        SetNodesFromProjects(Project.FromXml(projects));
    }

    public List<Project> GetProjects() =>
        [.. Nodes.Where(node => node.NodeType == ProjectItemType.Project).Select(node => new Project(node))];

    public NodeModel? GetNodeById(string id) =>
        Nodes.SelectMany(node => node.AllChildNodesFlat).FirstOrDefault(node => node.Id == id);

    public void AddNode(NodeModel node)
    {
        nodes.Add(node);
        NotifyNodeCollectionChanged();
        RefreshTodos();
    }

    public void DeleteNode(NodeModel node)
    {
        if (!nodes.Remove(node))
        {
            foreach (NodeModel rootNode in Nodes.Where(nodeItem => !nodeItem.IsLeaf))
                rootNode.DeleteNode(node);
        }

        NotifyNodeCollectionChanged();
        RefreshTodos();
    }

    public void FilterNodes(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            foreach (NodeModel node in Nodes)
                node.Visualize();

            return;
        }

        string normalizedFilter = filter.Trim().ToUpperInvariant();
        foreach (NodeModel node in Nodes)
            node.Filter(normalizedFilter);
    }

    public void ExpandNodes() => ExpandNodesRecursive(nodes);
    public void CollapseNodes() => CollapseNodesRecursive(nodes);

    /// <summary>Expands the selected node's ancestors and marks it selected for a UI adapter.</summary>
    public void FocusNode(NodeModel node)
    {
        CollapseNodesRecursive(Nodes);
        node.ExpandParents();
        node.IsSelected = true;
    }

    public void SortNodes(bool byDate = false)
    {
        if (Nodes.Count == 0)
            return;

        foreach (NodeModel node in Nodes)
            node.SortNodes(byDate);

        if (Nodes.Count > 1)
        {
            Nodes = byDate
                ? new ObservableCollection<NodeModel>(Nodes.OrderBy(node => node.NodeType).ThenBy(node => node.Created))
                : new ObservableCollection<NodeModel>(Nodes.OrderBy(node => node.NodeType).ThenBy(node => node.Text));
        }
    }

    public void RefreshTodos()
    {
        Todos = new ObservableCollection<ListItemModel>(
            Nodes.SelectMany(node => node.AllChildNodesFlat)
                .Where(node => node.IsLeaf)
                .Select(node => new ListItemModel(node))
                .Where(item => item.DueDate.HasValue && item.Progress != 100)
                .OrderBy(item => item.DueDate.GetValueOrDefault())
                .ThenByDescending(item => item.Priority));
    }

    public void FireTodos() => OnPropertyChanged(nameof(Todos));

    private void NotifyNodeCollectionChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Todos));
    }

    private void SetNodesFromProjects(List<Project> projects)
    {
        Nodes = new ObservableCollection<NodeModel>(
            projects.OrderBy(x => x.Name).Select(project => new NodeModel(project)));
    }

    private static void ExpandNodesRecursive(IEnumerable<NodeModel> sourceNodes)
    {
        foreach (NodeModel node in sourceNodes)
        {
            node.IsExpanded = true;
            ExpandNodesRecursive(node.Nodes);
        }
    }

    private static void CollapseNodesRecursive(IEnumerable<NodeModel> sourceNodes)
    {
        foreach (NodeModel node in sourceNodes)
        {
            CollapseNodesRecursive(node.Nodes);
            node.IsExpanded = false;
        }
    }

    /// <summary>Moves a node using the legacy drag/drop conversion rules.</summary>
    public void MoveNode(NodeModel source, NodeModel? target)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (target is not null)
        {
            if (source.Id == target.Id || target.IsChildOf(source) ||
                (target.NodeType is ProjectItemType.Protected && source.NodeType is not ProjectItemType.Protected))
                throw new InvalidOperationException("That item cannot be moved to the selected target.");

            NodeModel moved = source.DeepCopy(!source.IsProtected, target);
            DeleteNode(source);
            target.Nodes.Add(moved);
            target.IsExpanded = true;
            target.UpdateProgress();
            target.UpdateParentProgress();
            moved.CheckForNewDueDate();
        }
        else
        {
            if (source.IsProtected)
                throw new InvalidOperationException("Protected items cannot be promoted to root projects.");
            NodeModel moved = source.DeepCopy(convertTypes: true);
            DeleteNode(source);
            AddNode(moved);
            RefreshTodos();
            return;
        }

        RefreshTodos();
    }
}
