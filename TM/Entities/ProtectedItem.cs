namespace TM.Entities;

public class ProtectedItem : ICloneable
{
    #region props
    public Guid UUID { get; set; }
    public List<ProtectedItem> Items { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Url { get; set; }

    public string Login { get; set; }

    public string Password { get; set; }

    public DateTime Created { get; set; }
    public DateTime? Changed { get; set; }

    // gui helpers
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }

    // calc props
    public bool IsLeaf => Items.Count == 0;
    public string CreatedXml => Created.ToIsoDate(true);
    public string ChangedXml => Changed.HasValue ? Changed.Value.ToIsoDate(true) : "";
    public override string ToString() => Name;

    #endregion

    #region constructors
    public ProtectedItem(string name)
    {
        UUID = Guid.NewGuid();
        Created = DateTime.Now.Trim(TimeSpan.TicksPerMinute);
        Changed = Created;
        Name = name;
        Description = "";
        Url = "";
        Login = "";
        Password = "";
        Items = new List<ProtectedItem>();
    }

    public ProtectedItem(XmlNode item)
    {
        Guid id = Guid.NewGuid();
        string u = item.GetAttribute("uuid");
        if (!string.IsNullOrWhiteSpace(u))
            Guid.TryParse(u, out id);
        UUID = id;

        Items = new List<ProtectedItem>();
        this.Name = XMLhelper.GetInnerTextFromNode(item.ChildNodes, "name", false);
        this.Login = XMLhelper.GetInnerTextFromNode(item.ChildNodes, "login", false);
        this.Password = XMLhelper.GetInnerTextFromNode(item.ChildNodes, "pass", false);
        this.Url = XMLhelper.GetInnerTextFromNode(item.ChildNodes, "url", false);
        this.Description = XMLhelper.GetInnerTextFromNode(item.ChildNodes, "description", false);
        DateTime? created = ProjectItem.fromDate(XMLhelper.GetInnerTextFromNode(item.ChildNodes, "created", false));
        this.Created = created.HasValue ? created.Value : DateTime.Now;
        this.Changed = ProjectItem.fromDate(XMLhelper.GetInnerTextFromNode(item.ChildNodes, "changed", false));

        XmlNode items = item.ChildNodes.FindNodeByName("items", false);
        if (items != null && items.HasChildNodes)
            foreach (XmlNode n in items.ChildNodes.FindAllNodesByName("item", false, false))
                Items.Add(new ProtectedItem(n));
    }

    public ProtectedItem(NodeModel node)
    {
        UUID = Guid.Parse(node.Id);
        Name = node.Text;
        Login = node.Login;
        Password = node.Password;
        Url = node.Url;
        Description = node.Description;
        Created = node.Created;
        Changed = node.IsChanged || !node.Changed.HasValue ? DateTime.Now : node.Changed;
        IsExpanded = node.IsExpanded;
        IsSelected = node.IsSelected;

        Items = new List<ProtectedItem>();

        foreach (NodeModel n in node.Nodes)
        {
            if (n.Parent == node.Id && n.NodeType == ProjectItemType.Subtask)
                Items.Add(new ProtectedItem(n));
        }

    }

    #endregion

    public List<ProtectedItem> AllProtectedItems()
    {
        List<ProtectedItem> items = new List<ProtectedItem>();

        items.AddRange(Items);

        foreach (ProtectedItem p in Items)
            items.AddRange(p.AllProtectedItems());

        return items;
    }

    public XmlDocument ToXml()
    {
        XmlDocument doc = new XmlDocument();
        XmlElement item = doc.CreateElement("item");

        XmlAttribute uuid = doc.CreateAttribute("uuid");
        uuid.InnerText = UUID.ToString();
        item.Attributes.Append(uuid);

        XmlElement name = doc.CreateElement("name");
        name.InnerText = this.Name;
        item.AppendChild(name);

        XmlElement login = doc.CreateElement("login");
        login.InnerText = this.Login;
        item.AppendChild(login);

        XmlElement pass = doc.CreateElement("pass");
        pass.InnerText = this.Password;
        item.AppendChild(pass);

        XmlElement url = doc.CreateElement("url");
        url.InnerText = this.Url;
        item.AppendChild(url);

        if (!string.IsNullOrWhiteSpace(Description))
        {
            XmlElement description = doc.CreateElement("description");
            description.InnerText = Description;
            item.AppendChild(description);
        }

        XmlElement created = doc.CreateElement("created");
        created.InnerText = CreatedXml;
        item.AppendChild(created);

        if (Changed.HasValue)
        {
            XmlElement changed = doc.CreateElement("changed");
            changed.InnerText = ChangedXml;
            item.AppendChild(changed);
        }

        if (!IsLeaf)
        {
            XmlElement items = doc.CreateElement("items");
            for (int i = 0; i < Items.Count; i++)
            {
                XmlNode child = doc.ImportNode(Items[i].ToXml().DocumentElement, true);
                items.AppendChild(child);
            }
            item.AppendChild(items);
        }

        doc.AppendChild(item);

        return doc;
    }

    #region IClonable

    public object Clone() => Copy();

    public ProtectedItem Copy()
    {
        ProtectedItem that = new ProtectedItem(this.Name);
        that.UUID = this.UUID;
        that.Name = this.Name;
        that.Description = this.Description;
        that.Url = this.Url;
        that.Login = this.Login;
        that.Password = this.Password;
        that.Created = this.Created;
        that.Changed = this.Changed;
        that.IsExpanded = this.IsExpanded;
        that.IsSelected = this.IsSelected;
        that.Items = new List<ProtectedItem>();
        foreach (ProtectedItem item in Items)
            that.Items.Add(item.Copy());

        return that;
    }

    #endregion

    public override bool Equals(object o)
    {
        ProtectedItem x = o as ProtectedItem;

        if (x == null)
            return false;

        return GetHashCode() == x.GetHashCode();
    }

    public override int GetHashCode()
    {
        int h = HashCode.Combine(UUID, Name, Description, Url, Login, Password, Created, Changed);

        foreach (ProtectedItem i in Items)
            h = HashCode.Combine(h, i.GetHashCode());

        return h;
    }

}
