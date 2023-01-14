namespace TM.Entities;

public class Task : ProjectItem, ICloneable
{
    public Difficulty Difficulty { get; set; }
    public List<Subtask> SubTasks { get; set; }
    public bool IsLeaf => SubTasks.Count == 0;
    public Task(string name, DateTime? duedate = null)
    {
        UUID = Guid.NewGuid();
        Created = DateTime.Now.Trim(TimeSpan.TicksPerMinute);
        Changed = Created;
        Name = name;
        Description = "";
        Priority = Priority.None;
        DueDate = duedate;
        Difficulty = Difficulty.Direct;
        SubTasks = new List<Subtask>();
        ProtectedItems = new List<ProtectedItem>();
    }

    public Task(XmlNode task)
    {
        SetCommonElements(task);

        SubTasks = new List<Subtask>();

        this.Difficulty = XMLhelper.GetInnerTextFromNode(task.ChildNodes, "Difficulty", false).ToEnumSafe<Difficulty>();

        XmlNode subtasks = task.ChildNodes.FindNodeByName("subtasks", false);
        if (subtasks != null && subtasks.HasChildNodes)
            foreach (XmlNode n in subtasks.ChildNodes.FindAllNodesByName("subtask", false, false))
                SubTasks.Add(new Subtask(n));

    }

    public Task(NodeModel node)
    {
        UUID = Guid.Parse(node.Id);
        Name = node.Text;
        Description = node.Description;
        Priority = node.Priority;
        Difficulty = node.Difficulty;
        DueDate = node.DueDate;
        Created = node.Created;
        Changed = node.IsChanged || !node.Changed.HasValue ? DateTime.Now : node.Changed;
        Progress = node.Progress;
        Finished = node.Finished;
        IsExpanded = node.IsExpanded;
        IsSelected = node.IsSelected;

        SubTasks = new List<Subtask>();
        ProtectedItems = new List<ProtectedItem>();

        foreach (NodeModel n in node.Nodes)
        {
            if (n.Parent == node.Id && n.NodeType == ProjectItemType.Subtask)
                SubTasks.Add(new Subtask(n));
            else if (n.NodeType == ProjectItemType.Protected)
                ProtectedItems.Add(new ProtectedItem(n));
        }

    }

    public List<ProtectedItem> AllProtectedItems()
    {
        List<ProtectedItem> items = new List<ProtectedItem>();

        items.AddRange(ProtectedItems);

        foreach (Subtask t in SubTasks)
            items.AddRange(t.AllProtectedItems());

        foreach (ProtectedItem p in ProtectedItems)
            items.AddRange(p.AllProtectedItems());

        return items;
    }

    public XmlDocument ToXml()
    {

        XmlDocument doc = new XmlDocument();
        XmlElement task = doc.CreateElement("task");

        XmlAttribute uuid = doc.CreateAttribute("uuid");
        uuid.InnerText = UUID.ToString();
        task.Attributes.Append(uuid);

        XmlElement name = doc.CreateElement("name");
        name.InnerText = this.Name;
        task.AppendChild(name);

        XmlElement priority = doc.CreateElement("priority");
        priority.InnerText = this.Priority.ToString();
        task.AppendChild(priority);

        XmlElement difficulty = doc.CreateElement("difficulty");
        difficulty.InnerText = this.Difficulty.ToString();
        task.AppendChild(difficulty);

        if (!string.IsNullOrWhiteSpace(Description))
        {
            XmlElement description = doc.CreateElement("description");
            description.InnerText = this.Description;
            task.AppendChild(description);
        }

        if (DueDate.HasValue)
        {
            XmlElement due_date = doc.CreateElement("due_date");
            due_date.InnerText = this.DueDateXml;
            task.AppendChild(due_date);
        }

        XmlElement progress = doc.CreateElement("progress");
        progress.InnerText = this.Progress.ToString();
        task.AppendChild(progress);

        XmlElement created = doc.CreateElement("created");
        created.InnerText = this.CreatedXml;
        task.AppendChild(created);

        if (Changed.HasValue)
        {
            XmlElement changed = doc.CreateElement("changed");
            changed.InnerText = this.ChangedXml;
            task.AppendChild(changed);
        }

        if (Finished.HasValue)
        {
            XmlElement finished = doc.CreateElement("finished");
            finished.InnerText = this.FinishedXml;
            task.AppendChild(finished);
        }

        if (SubTasks.Count > 0)
        {
            XmlElement subtasks = doc.CreateElement("subtasks");
            for (int i = 0; i < SubTasks.Count; i++)
            {
                XmlNode subtask = doc.ImportNode(SubTasks[i].ToXml().DocumentElement, true);
                subtasks.AppendChild(subtask);
            }
            task.AppendChild(subtasks);
        }

        if (HasProtected)
        {
            XmlElement items = doc.CreateElement("items");
            for (int i = 0; i < ProtectedItems.Count; i++)
            {
                XmlNode pi = doc.ImportNode(ProtectedItems[i].ToXml().DocumentElement, true);
                items.AppendChild(pi);
            }
            task.AppendChild(items);
        }

        doc.AppendChild(task);

        return doc;
    }

    public object Clone() => Copy();

    public Task Copy()
    {
        Task that = new Task(this.Name);

        that.UUID = UUID;
        that.Name = Name;
        that.Description = Description;
        that.Priority = Priority;
        that.DueDate = DueDate;
        that.Progress = Progress;
        that.Created = Created;
        that.Changed = Changed;
        that.Finished = Finished;
        that.IsExpanded = IsExpanded;
        that.IsSelected = IsSelected;
        that.ProtectedItems = new List<ProtectedItem>();
        foreach (ProtectedItem item in ProtectedItems)
            that.ProtectedItems.Add(item.Copy());

        that.Difficulty = Difficulty;

        that.SubTasks = new List<Subtask>();
        foreach (Subtask item in SubTasks)
            that.SubTasks.Add(item.Copy());

        return that;
    }

    public override bool Equals(object o)
    {
        Task x = o as Task;

        if (x == null)
            return false;

        return GetHashCode() == x.GetHashCode();
    }

    public override int GetHashCode()
    {
        int h = HashCode.Combine(base.GetHashCode(), Difficulty);

        foreach (Subtask item in SubTasks)
            h = HashCode.Combine(h, item.GetHashCode());

        return h;
    }
}
