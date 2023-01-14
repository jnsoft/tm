using TM.Entities;

namespace TM.Models;

public class NodeModel : INotifyPropertyChanged
{
    #region props

    private bool constructed = false;
    public string Id { get; set; }
    public string Parent { get; set; }
    public NodeModel ParentItem { get; set; }
    public ProjectItemType NodeType { get; set; }

    private string text;
    public string Text
    {
        get => text;
        set
        {
            if (text != value && constructed)
                IsChanged = true;

            text = value;
            OnPropertyChanged("Text");
        }
    }

    private string login;
    public string Login
    {
        get => login;
        set
        {
            if (login != value && constructed)
                IsChanged = true;

            login = value;
            OnPropertyChanged("Login");
        }
    }

    private string password;
    public string Password
    {
        get => password;
        set
        {
            if (password != value && constructed)
                IsChanged = true;

            password = value;
            OnPropertyChanged("Password");
        }
    }

    private string url;
    public string Url
    {
        get => url;
        set
        {
            if (url != value && constructed)
                IsChanged = true;

            url = value;
            OnPropertyChanged("Url");
        }
    }

    private string description;
    public string Description
    {
        get => description;
        set
        {
            if (description != value && constructed)
                IsChanged = true;

            description = value;
            OnPropertyChanged("Description");
        }
    }

    private Priority priority;
    public Priority Priority
    {
        get => priority;
        set
        {
            if (priority != value && constructed)
                IsChanged = true;

            priority = value;
            Notify();
        }
    }

    private Difficulty difficulty;
    public Difficulty Difficulty
    {
        get => difficulty;
        set
        {
            if (difficulty != value && constructed)
                IsChanged = true;
            difficulty = value;
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
            if (dueDate != value && constructed)
                IsChanged = true;
            dueDate = value;
            updateDueDate();
            OnPropertyChanged("DueDate");
            Notify();
        }
    }
    public DateTime? Finished { get; set; }

    private int progress;
    public int Progress
    {
        get => progress;
        set
        {
            if (progress != value && constructed)
                IsChanged = true;

            int oldProgress = progress;
            progress = value;
            Finish(oldProgress, progress);
            UpdateParentProgress();
            Notify();
        }
    }

    private bool isExpanded;
    public bool IsExpanded
    {
        get => isExpanded;
        set { isExpanded = value; OnPropertyChanged("IsExpanded"); }
    }

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set { isSelected = value; OnPropertyChanged("IsSelected"); }
    }

    private Visibility visibility;
    public Visibility Visibility
    {
        get => visibility;
        set { visibility = value; OnPropertyChanged("Visibility"); }
    }

    private bool isFound;
    public bool IsFound
    {
        get => isFound;
        set { isFound = value; OnPropertyChanged("IsFound"); }
    }

    private bool isChanged;
    public bool IsChanged
    {
        get => isChanged;
        set
        {
            isChanged = value;
            if (value)
            {
                OnPropertyChanged("IsChanged");
                OnPropertyChanged("ChangedString");
            }
        }
    }

    private static object selectedItem = null;
    public static object SelectedItem
    {
        get => selectedItem;
        private set
        {
            if (selectedItem != value)
                selectedItem = value;
        }
    }

    public ObservableCollection<NodeModel> Nodes { get; private set; }

    #endregion

    #region calc props

    private IEnumerable<NodeModel> ChildNodes
    {
        get
        {
            if (IsLeaf)
                return new List<NodeModel>();
            else
                return this.Nodes.Where(n => n.Parent == Id).ToList();
        }
    }

    public List<NodeModel> AllChildNodesFlat
    {
        get
        {
            List<NodeModel> ns = new List<NodeModel>();
            ns.Add(this);

            if (!IsLeaf)
            {
                foreach (NodeModel node in this.Nodes)
                    ns.AddRange(node.AllChildNodesFlat);
            }
            return ns;
        }
    }

    public bool IsLeaf => Nodes == null || Nodes.Count == 0;

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

    public string ItemPrefix
    {
        get
        {
            if (NodeType == ProjectItemType.Project)
                return "P";
            else if (NodeType == ProjectItemType.Milestone)
                return "M";
            else if (NodeType == ProjectItemType.Task)
                return "T";
            else if (NodeType == ProjectItemType.Subtask)
                return "S";
            else if (NodeType == ProjectItemType.Protected)
                return "!";
            else
                return "";
        }
    }

    private int Effort
    {
        get
        {
            if (IsProtected)
                return 0;

            if (IsLeaf)
            {
                if (Difficulty == Difficulty.Direct)
                    return 1;
                else if (Difficulty == Difficulty.Easy)
                    return 2;
                else if (Difficulty == Difficulty.Medium)
                    return 10;
                else if (Difficulty == Difficulty.Hard)
                    return 20;
                else if (Difficulty == Difficulty.Project)
                    return 100;
                else
                    return 0;
            }
            return 0;
        }
    }

    public bool IsFinished => Finished.HasValue;

    public string FinishedString => IsFinished ? Finished.Value.ToIsoDate(false) : "";

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

    public DateTime DateEnd
    {
        get
        {
            if (ParentItem != null && ParentItem.DueDate.HasValue)
                return ParentItem.DueDate.Value;
            else
                return DateTime.Now.AddYears(10);
        }
    }

    #endregion

    #region constructors

    //public NodeModel(string text, params NodeModel[] nodes)
    //{
    //    Text = text;
    //    Nodes = new ObservableCollection<NodeModel>(nodes);
    //}

    public NodeModel() { }

    public NodeModel(Project p)
    {
        Id = p.UUID.ToString();
        Text = p.Name ?? "";
        Login = "";
        Password = "";
        Url = "";
        Description = p.Description ?? "";
        Parent = "";
        NodeType = ProjectItemType.Project;
        Priority = p.Priority;
        Difficulty = Difficulty.Project;
        Created = p.Created;
        Changed = p.Changed;
        dueDate = p.DueDate;
        Progress = p.Progress;
        IsExpanded = p.IsExpanded;
        IsSelected = p.IsSelected;
        Visibility = Visibility.Visible;

        List<NodeModel> ns = new List<NodeModel>();

        foreach (Milestone m in p.Milestones.OrderBy(x => x.Name))
            ns.Add(new NodeModel(m, this));

        foreach (Entities.Task t in p.Tasks.OrderBy(x => x.Name))
            ns.Add(new NodeModel(t, this));

        foreach (ProtectedItem item in p.ProtectedItems.OrderBy(x => x.Name))
            ns.Add(new NodeModel(item, this));

        Nodes = new ObservableCollection<NodeModel>(ns);

        constructed = true;
    }

    public NodeModel(Milestone m, NodeModel parent)
    {
        Id = m.UUID.ToString();
        Text = m.Name ?? "";
        Description = m.Description ?? "";
        Parent = parent.Id;
        ParentItem = parent;
        NodeType = ProjectItemType.Milestone;
        Priority = m.Priority;
        Difficulty = m.Difficulty;
        Created = m.Created;
        Changed = m.Changed;
        dueDate = m.DueDate;
        Progress = m.Progress;
        IsExpanded = m.IsExpanded;
        IsSelected = m.IsSelected;
        Visibility = Visibility.Visible;
        List<NodeModel> ts = new List<NodeModel>();

        foreach (Entities.Task t in m.Tasks.OrderBy(x => x.Name))
            ts.Add(new NodeModel(t, this));

        foreach (ProtectedItem item in m.ProtectedItems.OrderBy(x => x.Name))
            ts.Add(new NodeModel(item, this));

        Nodes = new ObservableCollection<NodeModel>(ts);

        constructed = true;
    }

    public NodeModel(Entities.Task t, NodeModel parent)
    {
        Id = t.UUID.ToString();
        Text = t.Name ?? "";
        Description = t.Description ?? "";
        Parent = parent.Id;
        ParentItem = parent;
        NodeType = ProjectItemType.Task;
        Priority = t.Priority;
        Difficulty = t.Difficulty;
        Created = t.Created;
        Changed = t.Changed;
        dueDate = t.DueDate;
        Progress = t.Progress;
        IsExpanded = t.IsExpanded;
        IsSelected = t.IsSelected;
        Visibility = Visibility.Visible;
        List<NodeModel> ss = new List<NodeModel>();

        foreach (Subtask s in t.SubTasks.OrderBy(x => x.Name))
            ss.Add(new NodeModel(s, this));

        foreach (ProtectedItem item in t.ProtectedItems.OrderBy(x => x.Name))
            ss.Add(new NodeModel(item, this));

        Nodes = new ObservableCollection<NodeModel>(ss);

        constructed = true;
    }

    public NodeModel(Subtask s, NodeModel parent)
    {
        Id = s.UUID.ToString();
        Text = s.Name ?? "";
        Description = s.Description ?? "";
        Parent = parent.Id;
        ParentItem = parent;
        NodeType = ProjectItemType.Subtask;
        Priority = s.Priority;
        Difficulty = s.Difficulty;
        Created = s.Created;
        Changed = s.Changed;
        dueDate = s.DueDate;
        Progress = s.Progress;
        IsExpanded = s.IsExpanded;
        IsSelected = s.IsSelected;
        Visibility = Visibility.Visible;
        List<NodeModel> ss = new List<NodeModel>();

        foreach (Subtask st in s.SubTasks.OrderBy(x => x.Name))
            ss.Add(new NodeModel(st, this));

        foreach (ProtectedItem item in s.ProtectedItems.OrderBy(x => x.Name))
            ss.Add(new NodeModel(item, this));

        Nodes = new ObservableCollection<NodeModel>(ss);

        constructed = true;
    }

    public NodeModel(ProtectedItem i, NodeModel parent)
    {
        Id = i.UUID.ToString();
        Text = i.Name ?? "";
        Login = i.Login ?? "";
        Password = i.Password ?? "";
        Url = i.Url ?? "";
        Description = i.Description ?? "";
        Parent = parent.Id;
        ParentItem = parent;
        NodeType = ProjectItemType.Protected;
        Created = i.Created;
        Changed = i.Changed;
        IsExpanded = i.IsExpanded;
        IsSelected = i.IsSelected;
        Visibility = Visibility.Visible;
        List<NodeModel> ss = new List<NodeModel>();

        foreach (ProtectedItem p in i.Items.OrderBy(x => x.Name))
            ss.Add(new NodeModel(p, this));

        Nodes = new ObservableCollection<NodeModel>(ss);

        constructed = true;
    }

    #endregion

    public NodeModel DeepCopy(bool convertTypes = false, NodeModel parent = null)
    {
        NodeModel n = new NodeModel();

        if (convertTypes)
        {
            if (parent != null)
                n.NodeType = GetNewTypeFromParentType(parent.NodeType, NodeType);
            else
                n.NodeType = ProjectItemType.Project;
        }
        else
            n.NodeType = NodeType;

        n.Id = Id;
        n.Text = Text;
        n.Login = Login;
        n.Password = Password;
        n.Url = Url;
        n.Description = Description;
        n.Parent = parent == null ? Parent : parent.Id;
        n.ParentItem = parent == null ? ParentItem : parent;
        n.Priority = Priority;
        n.Difficulty = Difficulty;
        n.Created = Created;
        n.Changed = Changed;
        n.dueDate = DueDate;
        n.Progress = Progress;
        n.IsExpanded = IsExpanded;
        n.IsSelected = IsSelected;
        n.Visibility = Visibility;

        List<NodeModel> ns = new List<NodeModel>();

        foreach (NodeModel node in Nodes)
            ns.Add(node.DeepCopy(convertTypes, n));

        n.Nodes = new ObservableCollection<NodeModel>(ns);
        n.constructed = true;
        return n;
    }

    private static ProjectItemType GetNewTypeFromParentType(ProjectItemType parent, ProjectItemType node)
    {
        if (node == ProjectItemType.Protected)
            return ProjectItemType.Protected;

        if (parent == ProjectItemType.Project)
        {
            if (node == ProjectItemType.Project || node == ProjectItemType.Milestone)
                return ProjectItemType.Milestone;
            else
                return ProjectItemType.Task;
        }
        else if (parent == ProjectItemType.Milestone)
            return ProjectItemType.Task;
        else
            return ProjectItemType.Subtask;
    }

    public void DeleteNode(NodeModel node)
    {
        foreach (NodeModel n in Nodes)
        {
            if (n.Id == node.Id)
            {
                Nodes.Remove(node);
                break;
            }

            else if (!n.IsLeaf)
                n.DeleteNode(node);
        }
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
        OnPropertyChanged("DotColor");
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

        OnPropertyChanged("IsFinished");
        OnPropertyChanged("Finished");
        OnPropertyChanged("FinishedString");

    }

    public void UpdateProgress() => Progress = GetCalculatedProgress();  // run after node deletion

    public void UpdateParentProgress()
    {
        NodeModel parent = this.ParentItem;
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

    private bool IsVisible(string filter) => Text.ToUpper().Contains(filter) || this.Description.ToUpper().Contains(filter);

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

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        if (PropertyChanged != null)
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }
}
