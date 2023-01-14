using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TM.Entities
{
    public abstract class ProjectItem
    {
        public Guid UUID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Priority Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public int Progress { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Changed { get; set; }
        public DateTime? Finished { get; set; }

        public List<ProtectedItem> ProtectedItems { get; set; }

        // gui helpers
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        //calc props

        public bool HasProtected => ProtectedItems != null && ProtectedItems.Count > 0;

        public string DueDateXml => DueDate.HasValue ? DueDate.Value.ToIsoDate(false) : "";
        public string CreatedXml => Created.ToIsoDate(true);
        public string ChangedXml => Changed.HasValue ? Changed.Value.ToIsoDate(true) : "";
        public string FinishedXml => Finished.HasValue ? Finished.Value.ToIsoDate(true) : "";

        // Methods
        protected void SetCommonElements(XmlNode node)
        {
            Guid id = Guid.NewGuid();
            string u = node.GetAttribute("uuid");
            if (!string.IsNullOrWhiteSpace(u))
                Guid.TryParse(u, out id);
            UUID = id;

            ProtectedItems = new List<ProtectedItem>();
            Name = XMLhelper.GetInnerTextFromNode(node.ChildNodes, "Name", false);
            Description = XMLhelper.GetInnerTextFromNode(node.ChildNodes, "Description", false);
            Priority = XMLhelper.GetInnerTextFromNode(node.ChildNodes, "Priority", false).ToEnumSafe<Priority>();
            // Difficulty
            Created = fromDate(XMLhelper.GetInnerTextFromNode(node.ChildNodes, "created", false));
            string d = XMLhelper.GetInnerTextFromNode(node.ChildNodes, "due_date", false);
            if (!string.IsNullOrWhiteSpace(d))
                DueDate = fromDate(d);
            Changed = fromDate(XMLhelper.GetInnerTextFromNode(node.ChildNodes, "changed", false));
            d = XMLhelper.GetInnerTextFromNode(node.ChildNodes, "finished", false);
            if (!string.IsNullOrWhiteSpace(d))
                Finished = fromDate(d);

            int prog = 0;
            if (Int32.TryParse(XMLhelper.GetInnerTextFromNode(node.ChildNodes, "progress", false), out prog))
                Progress = Math.Min(100, prog);

            XmlNode items = node.ChildNodes.FindNodeByName("items", false, false);
            if (items != null && items.HasChildNodes)
                foreach (XmlNode n in items.ChildNodes.FindAllNodesByName("item", false, false))
                    ProtectedItems.Add(new ProtectedItem(n));
        }

        public static DateTime fromDate(string s)
        {
            try
            {
                return s.FromIsoDate(true);
            }
            catch (Exception)
            {

                try
                {
                    return s.FromWC3DateTime(true);
                }
                catch (Exception)
                {

                    throw;
                }
            }
        }

        public override string ToString() => Name;

        public override bool Equals(object o)
        {
            ProjectItem x = o as ProjectItem;

            if (x == null)
                return false;

            return GetHashCode() == x.GetHashCode();
        }

        public override int GetHashCode()
        {
            int h = HashCode.Combine(UUID, Name, Description, Priority, DueDate, Progress);
            h = HashCode.Combine(h, Created, Changed, Finished);

            foreach (ProtectedItem i in ProtectedItems)
                h = HashCode.Combine(h, i.GetHashCode());

            return h;
        }
    }
}
