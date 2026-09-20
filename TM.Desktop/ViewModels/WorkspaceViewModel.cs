using System.ComponentModel.DataAnnotations;
using TM.Entities;

namespace TM.Desktop.ViewModels;

public enum WorkspaceAction { Refresh, New, Open, Save, SaveAs, Lock, Unlock, Select, Add, Edit, Delete, Filter, Reveal }

public sealed class WorkspaceCommand
{
    public WorkspaceAction Action { get; set; }
    public long Revision { get; set; }
    public string? NodeId { get; set; }
    public ProjectItemType NodeType { get; set; }
    [StringLength(256)] public string Name { get; set; } = "";
    [StringLength(32000)] public string Description { get; set; } = "";
    [StringLength(4096)] public string Password { get; set; } = "";
    [StringLength(4096)] public string Secret { get; set; } = "";
    public bool ReplaceSecret { get; set; }
    [StringLength(2048)] public string Login { get; set; } = "";
    [StringLength(2048)] public string Url { get; set; } = "";
    [StringLength(256)] public string Filter { get; set; } = "";
    public bool ConfirmDiscard { get; set; }
    public bool ConfirmDelete { get; set; }
    public Priority Priority { get; set; }
    public Difficulty Difficulty { get; set; }
    public DateTime? DueDate { get; set; }
    [Range(0, 100)] public int Progress { get; set; }
}

public sealed record TreeItemViewModel(string Id, string Text, ProjectItemType Type, bool Selected,
    IReadOnlyList<TreeItemViewModel> Children);
public sealed record TreeBranchViewModel(TreeItemViewModel Node, long Revision);
public sealed record EditorViewModel(string Id, string Name, ProjectItemType Type, string Description,
    string Login, string Url, Priority Priority, Difficulty Difficulty, DateTime? DueDate, int Progress, bool IsLeaf);
public sealed record TodoViewModel(string Id, string Title, DateTime? DueDate);
public sealed record WorkspaceViewModel(long Revision, bool IsLoaded, bool IsLocked, bool IsDirty,
    string FileName, string Filter, IReadOnlyList<TreeItemViewModel> Tree,
    IReadOnlyList<TodoViewModel> Todos, EditorViewModel? Editor, string? RevealedPassword = null, string? Message = null);
