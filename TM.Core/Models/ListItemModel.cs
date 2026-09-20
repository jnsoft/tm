using TM.Entities;

namespace TM.Models;

public class ListItemModel
{
    public string Id { get; set; }
    public string Title { get; set; }
    public int Completion { get; set; }
    public int Progress { get; set; }
    public Priority Priority { get; set; }
    public DateTime? DueDate { get; set; }

    public string Color
    {
        get
        {
            if (Priority == Priority.Critical)
                return "Red";
            else if (Priority == Priority.High)
                return "Orange";
            else if (Priority == Priority.Medium)
                return "Yellow";
            else if (Priority == Priority.Low)
                return "LightGreen";
            else
                return "White";

        }
    }

    public string TextColor
    {
        get
        {
            if (Priority == Priority.Critical)
                return "White";
            else
                return "Black";

        }
    }

    public ListItemModel(NodeModel n)
    {
        Id = n.Id;
        Title = n.Text;
        Completion = n.Progress;
        Priority = n.Priority;
        DueDate = n.DueDate;
        Progress = n.Progress;
    }

    public ListItemModel(ProjectItem x)
    {
        Id = x.UUID.ToString();
        Title = x.Name;
        Completion = x.Progress;
        Priority = x.Priority;
        DueDate = x.DueDate;
        Progress = x.Progress;
    }

    public override string ToString() => this.Title;

}
