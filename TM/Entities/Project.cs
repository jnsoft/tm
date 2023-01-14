namespace TM.Entities;

public class Project : ProjectItem, ICloneable
{
    public List<Milestone> Milestones { get; set; }
    public List<Task> Tasks { get; set; }

    public bool IsLeaf => Milestones.Count + Tasks.Count + ProtectedItems.Count == 0;

    public Project(string name)
    {
        UUID = Guid.NewGuid();
        Created = DateTime.Now.Trim(TimeSpan.TicksPerMinute);
        Changed = Created;
        Name = name;
        Description = "";
        Priority = Priority.None;
        Tasks = new List<Task>();
        Milestones = new List<Milestone>();
        ProtectedItems = new List<ProtectedItem>();
    }


    // from file
    public Project(XmlNode project)
    {
        SetCommonElements(project);

        Tasks = new List<Task>();
        Milestones = new List<Milestone>();

        XmlNode milestones = project.ChildNodes.FindNodeByName("milestones", false);
        XmlNode tasks = project.ChildNodes.FindNodeByName("tasks", false, false);

        if (milestones != null && milestones.HasChildNodes)
            foreach (XmlNode n in milestones.ChildNodes.FindAllNodesByName("milestone", false, false))
                Milestones.Add(new Milestone(n));

        if (tasks != null && tasks.HasChildNodes)
            foreach (XmlNode n in tasks.ChildNodes.FindAllNodesByName("task", false, false))
                Tasks.Add(new Task(n));

    }

    // from model
    public Project(NodeModel node)
    {
        UUID = Guid.Parse(node.Id);
        Name = node.Text;
        Description = node.Description;
        Priority = node.Priority;
        DueDate = node.DueDate;
        Created = node.Created;
        Changed = node.IsChanged || !node.Changed.HasValue ? DateTime.Now : node.Changed;
        Progress = node.Progress;
        Finished = node.Finished;
        IsExpanded = node.IsExpanded;
        IsSelected = node.IsSelected;

        Tasks = new List<Task>();
        Milestones = new List<Milestone>();
        ProtectedItems = new List<ProtectedItem>();

        foreach (NodeModel n in node.Nodes)
        {
            if (n.Parent == node.Id)
            {
                if (n.NodeType == ProjectItemType.Task)
                    Tasks.Add(new Task(n));
                else if (n.NodeType == ProjectItemType.Milestone)
                    Milestones.Add(new Milestone(n));
                else if (n.NodeType == ProjectItemType.Protected)
                    ProtectedItems.Add(new ProtectedItem(n));
            }
        }

    }

    public List<ProtectedItem> AllProtectedItems()
    {
        List<ProtectedItem> items = new List<ProtectedItem>();

        items.AddRange(ProtectedItems);

        foreach (Milestone m in Milestones)
            items.AddRange(m.AllProtectedItems());

        foreach (Task t in Tasks)
            items.AddRange(t.AllProtectedItems());

        foreach (ProtectedItem p in ProtectedItems)
            items.AddRange(p.AllProtectedItems());

        return items;
    }

    public XmlDocument ToXml()
    {
        XmlDocument doc = new XmlDocument();
        XmlElement project = doc.CreateElement("project");

        XmlAttribute uuid = doc.CreateAttribute("uuid");
        uuid.InnerText = UUID.ToString();
        project.Attributes.Append(uuid);

        XmlElement name = doc.CreateElement("name");
        name.InnerText = this.Name;
        project.AppendChild(name);

        XmlElement priority = doc.CreateElement("priority");
        priority.InnerText = this.Priority.ToString();
        project.AppendChild(priority);

        if (!string.IsNullOrWhiteSpace(Description))
        {
            XmlElement description = doc.CreateElement("description");
            description.InnerText = this.Description;
            project.AppendChild(description);
        }

        if (DueDate.HasValue)
        {
            XmlElement due_date = doc.CreateElement("due_date");
            due_date.InnerText = this.DueDateXml;
            project.AppendChild(due_date);
        }

        XmlElement progress = doc.CreateElement("progress");
        progress.InnerText = this.Progress.ToString();
        project.AppendChild(progress);

        XmlElement created = doc.CreateElement("created");
        created.InnerText = this.CreatedXml;
        project.AppendChild(created);

        if (Changed.HasValue)
        {
            XmlElement changed = doc.CreateElement("changed");
            changed.InnerText = this.ChangedXml;
            project.AppendChild(changed);
        }

        if (Finished.HasValue)
        {
            XmlElement finished = doc.CreateElement("finished");
            finished.InnerText = this.FinishedXml;
            project.AppendChild(finished);
        }


        if (Tasks.Count > 0)
        {
            XmlElement tasks = doc.CreateElement("tasks");
            for (int i = 0; i < Tasks.Count; i++)
            {
                XmlNode task = doc.ImportNode(Tasks[i].ToXml().DocumentElement, true);
                tasks.AppendChild(task);
            }
            project.AppendChild(tasks);
        }

        if (Milestones.Count > 0)
        {
            XmlElement milestones = doc.CreateElement("milestones");
            for (int i = 0; i < Milestones.Count; i++)
            {
                XmlNode task = doc.ImportNode(Milestones[i].ToXml().DocumentElement, true);
                milestones.AppendChild(task);
            }
            project.AppendChild(milestones);
        }

        if (HasProtected)
        {
            XmlElement items = doc.CreateElement("items");
            for (int i = 0; i < ProtectedItems.Count; i++)
            {
                XmlNode pi = doc.ImportNode(ProtectedItems[i].ToXml().DocumentElement, true);
                items.AppendChild(pi);
            }
            project.AppendChild(items);
        }

        doc.AppendChild(project);

        return doc;
    }

    public static List<Project> FromXml(List<XmlNode> projects)
    {
        List<Project> Projects = new List<Project>();

        foreach (XmlNode p in projects)
            Projects.Add(new Project(p));

        return Projects;
    }

    public object Clone() => Copy();

    public Project Copy()
    {
        Project that = new Project(this.Name);

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

        that.Tasks = new List<Task>();
        foreach (Task item in Tasks)
            that.Tasks.Add(item.Copy());

        that.Milestones = new List<Milestone>();
        foreach (Milestone item in Milestones)
            that.Milestones.Add(item.Copy());

        return that;
    }

    public override bool Equals(object o)
    {
        Project p = o as Project;

        if (p == null)
            return false;

        return this.GetHashCode() == o.GetHashCode();
    }

    public override int GetHashCode()
    {
        int h = base.GetHashCode();

        foreach (Milestone item in Milestones)
            h = HashCode.Combine(h, item.GetHashCode());

        foreach (Task item in Tasks)
            h = HashCode.Combine(h, item.GetHashCode());

        foreach (ProtectedItem pi in ProtectedItems)
            h = HashCode.Combine(h, pi.GetHashCode());

        return h;
    }
}
