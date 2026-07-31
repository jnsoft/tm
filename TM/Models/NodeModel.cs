using TM.Common;
using TM.Entities;

namespace TM.Models;

public class NodeModel : ObservableObject
{
    #region props

    private bool constructed = false;
    public string Id { get; set; } = string.Empty;
    public string Parent { get; set; } = string.Empty;
    public NodeModel? ParentItem { get; set; }
    public ProjectItemType NodeType { get; set; }

    private string text = string.Empty;
    public string Text
    {
        get => text;
        set => SetTrackedProperty(ref text, value);
    }

    private string login = string.Empty;
    public string Login
    {
        get => login;
        set
        {
            if (login == value)
                return;

            if (constructed)
                IsChanged = true;

            SetProperty(ref login, value);
        }
    }

    private string password = string.Empty;
    public string Password
    {
        get => password;
        set
        {
            if (password == value)
                return;

            if (constructed)
                IsChanged = true;

            SetProperty(ref password, value);
        }
    }

    private string url = string.Empty;
    public string Url
    {
        get => url;
        set
        {
            if (url == value)
                return;

            if (constructed)
                IsChanged = true;

            SetProperty(ref url, value);
        }
    }

    private string description = string.Empty;
    public string Description
    {
        get => description;
        set
        {
            if (description == value)
                return;

            if (constructed)
                IsChanged = true;

            SetProperty(ref description, value);
        }
    }

    private Priority priority;
    public Priority Priority
    {
        get => priority;
        set
        {
            if (priority == value)
                return;

            if (constructed)
                IsChanged = true;

            SetProperty(ref priority, value);
            Notify();
        }
    }

    private Difficulty difficulty;
    public Difficulty Difficulty
    {
        get => difficulty;
        set
        {
            if (difficulty == value)
                return;

            if (constructed)
                IsChanged = true;

            SetProperty(ref difficulty, value);
            UpdateParentProgress();
            Notify();
        }
    }

    public DateTime Created { get; set; }
    public DateTime? Changed { get; set; }

    private DateTime? dueDate;
    public DateTime? DueDate
    {
        get => dueDate;
        set
        {
            DateTime? normalizedValue = NormalizeDueDate(value);
            if (dueDate == normalizedValue)
                return;

            if (constructed)
                IsChanged = true;

            SetProperty(ref dueDate, normalizedValue);
            updateDueDate();
            Notify();
        }
    }

    private DateTime? NormalizeDueDate(DateTime? value)
    {
        if (IsProtected || ParentItem is not { DueDate: DateTime parentDueDate })
            return value;

        return value is not DateTime childDueDate || childDueDate > parentDueDate
            ? parentDueDate
            : childDueDate;
    }

    public DateTime? Finished { get; set; }

    private int progress;
    public int Progress
    {
        get => progress;
        set
        {
            if (progress == value)
                return;

            if (constructed)
                IsChanged = true;

            int oldProgress = progress;
            SetProperty(ref progress, value);
            Finish(oldProgress, progress);
            UpdateParentProgress();
            Notify();
        }
    }

    private bool isExpanded;
    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    private Visibility visibility;
    public Visibility Visibility
    {
        get => visibility;
        set => SetProperty(ref visibility, value);
    }

    private bool isFound;
    public bool IsFound
    {
        get => isFound;
        set => SetProperty(ref isFound, value);
    }

    private bool isChanged;
    public bool IsChanged
    {
        get => isChanged;
        set
        {
            if (!SetProperty(ref isChanged, value))
                return;

            if (value)
                OnPropertyChanged(nameof(ChangedString));
        }
    }

    private static object? selectedItem = null;
    public static object? SelectedItem
    {
        get => selectedItem;
        private set
        {
            if (selectedItem != value)
                selectedItem = value;
        }
    }

    public ObservableCollection<NodeModel> Nodes { get; private set; } = [];

    #endregion

    #region calc props

    private IEnumerable<NodeModel> ChildNodes => IsLeaf ? [] : Nodes.Where(node => node.Parent == Id);

    private IEnumerable<NodeModel> EnumerateSelfAndDescendants()
    {
        yield return this;

        foreach (NodeModel childNode in Nodes)
        {
            foreach (NodeModel descendant in childNode.EnumerateSelfAndDescendants())
                yield return descendant;
        }
    }
    public List<NodeModel> AllChildNodesFlat => [.. EnumerateSelfAndDescendants()];

    public bool IsLeaf => Nodes.Count == 0;

    public bool IsProtected => NodeType == ProjectItemType.Protected;

    public bool IsProjectItem => !IsProtected;

    public bool CanEditDifficulty => NodeType != ProjectItemType.Project && !IsProtected;

    public Visibility IsProjectVisible => IsProjectItem ? Visibility.Visible : Visibility.Collapsed;

    public Visibility IsProtectedVisible => IsProtected ? Visibility.Visible : Visibility.Collapsed;

    public string DotColor => getColor.ToString();

    internal Color getColor
    {
        get
        {
            Color color = Color.White;

            if (Progress == 100) // avslutade ärenden
                color = Color.Gray;
            else if (DueDate.HasValue && DueDate.Value <= DateTime.Today) // alla som passerat sin deadline
                color = Color.Red;
            else if (DueDate.HasValue && DueDate.Value <= DateTime.Today.AddDays( // alla som ska vara klara inom en vecka, eller som är viktiga eller stora och ska vara klara om två veckor
                (Priority == Priority.Critical || Priority == Priority.High) || (Difficulty == Difficulty.Project || Difficulty == Difficulty.Hard) ? 14
                : (Priority == Priority.Medium ? 7
                : (Priority == Priority.Low ? 3
                : 1))))
                color = Color.Orange;
            else if (DueDate.HasValue) // alla som inte är klara, men deadline är långt borta
                color = Color.LightGreen;
            else if (!DueDate.HasValue) // alla som saknar deadline
                color = Color.LightBlue;

            foreach (NodeModel n in ChildNodes)
            {
                if ((int)n.getColor > (int)color)
                    color = n.getColor;
            }
            return color;
        }
    }

    public string ItemPrefix => NodeType switch
    {
        ProjectItemType.Project => "P",
        ProjectItemType.Milestone => "M",
        ProjectItemType.Task => "T",
        ProjectItemType.Subtask => "S",
        ProjectItemType.Protected => "!",
        _ => string.Empty
    };

    private int Effort =>
    IsProtected || !IsLeaf
        ? 0
        : Difficulty switch
        {
            Difficulty.Direct => 1,
            Difficulty.Easy => 2,
            Difficulty.Medium => 10,
            Difficulty.Hard => 20,
            Difficulty.Project => 100,
            _ => 0
        };

    public bool IsFinished => Finished.HasValue;

    public string FinishedString => Finished is DateTime finished ? finished.ToIsoDate(false) : "";

    public string CreatedString => Created.ToIsoDate(false);

    public string ChangedString
    {
        get
        {
            if (IsChanged)
                return DateTime.Now.ToIsoDate(false) + " " + DateTime.Now.ToShortTimeString();
            else
                return Changed.HasValue ? Changed.Value.ToIsoDate(false) + " " + Changed.Value.ToShortTimeString() : "";
        }
    }

    public DateTime? MaxDueDate => ParentItem?.DueDate;

    public DateTime DateEnd => ParentItem?.DueDate ?? DateTime.Now.AddYears(10);

    #endregion

    #region constructors

    private void InitializeNodeCore(
    string id,
    string? text,
    string? description,
    NodeModel? parent,
    ProjectItemType nodeType,
    Priority priority,
    Difficulty difficulty,
    DateTime created,
    DateTime? changed,
    DateTime? nodeDueDate,
    int progress,
    bool isExpanded,
    bool isSelected,
    string? login = null,
    string? password = null,
    string? url = null)
    {
        Id = id;
        Parent = parent?.Id ?? string.Empty;
        ParentItem = parent;
        NodeType = nodeType;

        this.text = text ?? string.Empty;
        this.description = description ?? string.Empty;
        this.login = login ?? string.Empty;
        this.password = password ?? string.Empty;
        this.url = url ?? string.Empty;

        this.priority = priority;
        this.difficulty = difficulty;
        Created = created;
        Changed = changed;
        dueDate = nodeDueDate;
        this.progress = progress;
        this.isExpanded = isExpanded;
        this.isSelected = isSelected;
        visibility = Visibility.Visible;
    }

    private void CompleteConstruction(IEnumerable<NodeModel> childNodes)
    {
        Nodes = new ObservableCollection<NodeModel>(childNodes);
        constructed = true;
    }


    public NodeModel() { }

    public NodeModel(Project p)
    {
        InitializeNodeCore(
            id: p.UUID.ToString(),
            text: p.Name,
            description: p.Description,
            parent: null,
            nodeType: ProjectItemType.Project,
            priority: p.Priority,
            difficulty: Difficulty.Project,
            created: p.Created,
            changed: p.Changed,
            nodeDueDate: p.DueDate,
            progress: p.Progress,
            isExpanded: p.IsExpanded,
            isSelected: p.IsSelected);

        CompleteConstruction(
            p.Milestones.OrderBy(x => x.Name).Select(m => new NodeModel(m, this))
                .Concat(p.Tasks.OrderBy(x => x.Name).Select(t => new NodeModel(t, this)))
                .Concat(p.ProtectedItems.OrderBy(x => x.Name).Select(item => new NodeModel(item, this))));
    }

    public NodeModel(Milestone m, NodeModel parent)
    {
        InitializeNodeCore(
            id: m.UUID.ToString(),
            text: m.Name,
            description: m.Description,
            parent: parent,
            nodeType: ProjectItemType.Milestone,
            priority: m.Priority,
            difficulty: m.Difficulty,
            created: m.Created,
            changed: m.Changed,
            nodeDueDate: m.DueDate,
            progress: m.Progress,
            isExpanded: m.IsExpanded,
            isSelected: m.IsSelected);

        CompleteConstruction(
            m.Tasks.OrderBy(x => x.Name).Select(t => new NodeModel(t, this))
                .Concat(m.ProtectedItems.OrderBy(x => x.Name).Select(item => new NodeModel(item, this))));
    }

    public NodeModel(Entities.Task t, NodeModel parent)
    {
        InitializeNodeCore(
            id: t.UUID.ToString(),
            text: t.Name,
            description: t.Description,
            parent: parent,
            nodeType: ProjectItemType.Task,
            priority: t.Priority,
            difficulty: t.Difficulty,
            created: t.Created,
            changed: t.Changed,
            nodeDueDate: t.DueDate,
            progress: t.Progress,
            isExpanded: t.IsExpanded,
            isSelected: t.IsSelected);

        CompleteConstruction(
            t.SubTasks.OrderBy(x => x.Name).Select(subtask => new NodeModel(subtask, this))
                .Concat(t.ProtectedItems.OrderBy(x => x.Name).Select(item => new NodeModel(item, this))));
    }

    public NodeModel(Subtask s, NodeModel parent)
    {
        InitializeNodeCore(
            id: s.UUID.ToString(),
            text: s.Name,
            description: s.Description,
            parent: parent,
            nodeType: ProjectItemType.Subtask,
            priority: s.Priority,
            difficulty: s.Difficulty,
            created: s.Created,
            changed: s.Changed,
            nodeDueDate: s.DueDate,
            progress: s.Progress,
            isExpanded: s.IsExpanded,
            isSelected: s.IsSelected);

        CompleteConstruction(
            s.SubTasks.OrderBy(x => x.Name).Select(subtask => new NodeModel(subtask, this))
                .Concat(s.ProtectedItems.OrderBy(x => x.Name).Select(item => new NodeModel(item, this))));
    }

    public NodeModel(ProtectedItem i, NodeModel parent)
    {
        InitializeNodeCore(
            id: i.UUID.ToString(),
            text: i.Name,
            description: i.Description,
            parent: parent,
            nodeType: ProjectItemType.Protected,
            priority: default,
            difficulty: default,
            created: i.Created,
            changed: i.Changed,
            nodeDueDate: null,
            progress: 0,
            isExpanded: i.IsExpanded,
            isSelected: i.IsSelected,
            login: i.Login,
            password: i.Password,
            url: i.Url);

        CompleteConstruction(
            i.Items.OrderBy(x => x.Name).Select(item => new NodeModel(item, this)));
    }

    #endregion

    public NodeModel DeepCopy(bool convertTypes = false, NodeModel? parent = null)
    {
        ProjectItemType nodeType = convertTypes
        ? parent is null
            ? ProjectItemType.Project
            : GetNewTypeFromParentType(parent.NodeType, NodeType)
        : NodeType;

        NodeModel clone = new();
        clone.InitializeNodeCore(
            id: Id,
            text: Text,
            description: Description,
            parent: parent ?? ParentItem,
            nodeType: nodeType,
            priority: Priority,
            difficulty: Difficulty,
            created: Created,
            changed: Changed,
            nodeDueDate: DueDate,
            progress: Progress,
            isExpanded: IsExpanded,
            isSelected: IsSelected,
            login: Login,
            password: Password,
            url: Url);

        clone.CompleteConstruction(Nodes.Select(node => node.DeepCopy(convertTypes, clone)));
        return clone;
    }

    private static ProjectItemType GetNewTypeFromParentType(ProjectItemType parent, ProjectItemType node) =>
    (parent, node) switch
    {
        (_, ProjectItemType.Protected) => ProjectItemType.Protected,
        (ProjectItemType.Project, ProjectItemType.Project) => ProjectItemType.Milestone,
        (ProjectItemType.Project, ProjectItemType.Milestone) => ProjectItemType.Milestone,
        (ProjectItemType.Project, _) => ProjectItemType.Task,
        (ProjectItemType.Milestone, _) => ProjectItemType.Task,
        _ => ProjectItemType.Subtask
    };

    public void DeleteNode(NodeModel node)
    {
        NodeModel? directChild = Nodes.FirstOrDefault(child => child.Id == node.Id);
        if (directChild is not null)
        {
            Nodes.Remove(directChild);
            return;
        }

        foreach (NodeModel childNode in Nodes.Where(child => !child.IsLeaf))
            childNode.DeleteNode(node);
    }

    public bool IsChildOf(NodeModel parent)
    {
        if (this.ParentItem != null)
        {
            if (this.ParentItem.Id == parent.Id)
                return true;
            else
                return this.ParentItem.IsChildOf(parent);
        }
        else
            return false;
    }

    private int GetCalculatedSize()
    {
        int sum = Effort;

        foreach (NodeModel node in ChildNodes)
            sum += node.GetCalculatedSize();

        return sum;
    }

    private double GetCalculatedWork()
    {
        if (IsProtected)
            return 0;

        double work = 0;

        if (IsLeaf)
            work = Effort * (100 - Progress) * .01;
        else
        {
            foreach (NodeModel node in ChildNodes)
                work += node.GetCalculatedWork();
        }

        return work;
    }

    public int GetCalculatedProgress()
    {
        double work = GetCalculatedWork();
        double size = GetCalculatedSize();
        if (size == 0)
            return 0;

        double prog = 100 - ((work / size) * 100);
        return (int)prog;
    }

    // runs from DueDate setter
    private void updateDueDate()
    {
        if (DueDate.HasValue)
        {
            foreach (NodeModel node in ChildNodes)
            {
                if (node.DueDate.HasValue && node.DueDate.Value <= DueDate)
                    continue;
                else
                    node.DueDate = DueDate;
            }
        }
    }

    public void CheckForNewDueDate()
    {
        if (ParentItem != null && !IsProtected)
        {
            if (ParentItem.DueDate.HasValue)
            {
                if (!DueDate.HasValue || DueDate.Value > ParentItem.DueDate)
                    this.DueDate = ParentItem.DueDate;
            }
        }
    }

    public void Notify()
    {
        OnPropertyChanged(nameof(DotColor));
        NotifyParents();
    }

    public void NotifyParents()
    {
        if (this.ParentItem != null)
            this.ParentItem.Notify();
    }

    public void Finish(int oldProgress, int newProgress)
    {
        if (oldProgress == 100 && newProgress < 100)
            this.Finished = null;

        else if (oldProgress != 100 && newProgress == 100)
            this.Finished = DateTime.Now;

        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(Finished));
        OnPropertyChanged(nameof(FinishedString));

    }

    public void UpdateProgress() => Progress = GetCalculatedProgress();  // run after node deletion

    public void UpdateParentProgress()
    {
        NodeModel? parent = this.ParentItem;
        if (parent != null)
        {
            int oldProgress = parent.Progress;
            parent.Progress = parent.GetCalculatedProgress();
            parent.Finish(oldProgress, parent.Progress);
            parent = null;
        }
    }

    public void ExpandParents()
    {
        if (this.ParentItem != null)
        {
            this.ParentItem.IsExpanded = true;
            this.ParentItem.ExpandParents();
        }
    }

    public void SortNodes(bool bydate = false)
    {
        if (this.IsLeaf)
            return;

        foreach (NodeModel n in Nodes)
            n.SortNodes(bydate);

        if (this.Nodes.Count > 1)
        {
            if (bydate)
                this.Nodes = new ObservableCollection<NodeModel>(this.Nodes.OrderBy(n => n.NodeType).ThenBy(n => n.Created));
            else
                this.Nodes = new ObservableCollection<NodeModel>(this.Nodes.OrderBy(n => n.NodeType).ThenBy(n => n.Text));
        }
    }

    private bool IsVisible(string filter) => 
        Text.ToUpper().Contains(filter) || 
        Description.ToUpper().Contains(filter);

    // filter must ne uppercase! Reterns true if filter is hit on self or childnodes
    public bool Filter(string filter)
    {
        bool isV = false;

        foreach (NodeModel n in Nodes)
        {
            if (n.Filter(filter))
                isV = true;
        }

        if (IsVisible(filter) || isV)
        {
            this.Visibility = Visibility.Visible;
            this.IsFound = IsVisible(filter);
            ExpandParents();
            return true;
        }
        else
        {
            this.Visibility = Visibility.Collapsed;
            this.IsFound = false;
            return false;
        }
    }

    // clear search
    public void Visualize()
    {
        foreach (NodeModel n in Nodes)
            n.Visualize();

        this.Visibility = Visibility.Visible;
        this.IsFound = false;
    }

    public void ResetSave()
    {
        foreach (NodeModel n in Nodes)
            n.ResetSave();

        this.IsChanged = false;
    }

    public override string ToString() => NodeType.ToString() + ": " + Text + ", " + Created.ToString() + " | (" + Nodes.Count.ToString() + ")" + " | " + IsChanged.ToString();


    #region Helpers

    private bool SetTrackedProperty<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        if (constructed)
            IsChanged = true;

        return SetProperty(ref field, value);
    }

    #endregion
}
