using System.ComponentModel.DataAnnotations;
using TM.Entities;
using TM.Services;

namespace TM.Desktop.ViewModels;

public enum WorkspaceAction { Refresh, New, Open, Save, SaveAs, Lock, Unlock, Select, Add, Edit, Delete, Filter, ExpandTree, CollapseTree, FocusSelected, SortByName, SortByDate, AppendTimestamp, Reveal, GeneratePassword, ChangePassword, CopyPassword, DocumentFile, Hmac, GenerateKeys, PublicKeyFile, GenerateCertificate, SignFile, ImportCertificate, ExportCertificate, ExportTransfer, ImportTransfer }

public sealed class WorkspaceCommand
{
    public WorkspaceAction Action { get; set; }
    public DocumentFileOperation DocumentFileOperation { get; set; }
    public HmacOperation HmacOperation { get; set; }
    public HmacAlgorithm HmacAlgorithm { get; set; } = HmacAlgorithm.Sha256;
    public PublicKeyFileOperation PublicKeyFileOperation { get; set; }
    [StringLength(16384)] public string PeerPublicKey { get; set; } = "";
    public long Revision { get; set; }
    public string? NodeId { get; set; }
    public ProjectItemType NodeType { get; set; }
    [StringLength(256)] public string Name { get; set; } = "";
    [StringLength(32000)] public string Description { get; set; } = "";
    [StringLength(4096)] public string Password { get; set; } = "";
    [StringLength(4096)] public string NewPassword { get; set; } = "";
    [StringLength(4096)] public string ConfirmNewPassword { get; set; } = "";
    [StringLength(4096)] public string CertificatePassword { get; set; } = "";
    [StringLength(4096)] public string ConfirmCertificatePassword { get; set; } = "";
    [StringLength(4096)] public string TransferKey { get; set; } = "";
    [StringLength(4096)] public string ConfirmTransferKey { get; set; } = "";
    public bool ConfirmCertificateReplacement { get; set; }
    public bool ConfirmPasswordChange { get; set; }
    [StringLength(4096)] public string Secret { get; set; } = "";
    public bool ReplaceSecret { get; set; }
    [Range(5, 30)] public int GeneratedPasswordLength { get; set; } = 12;
    public bool UseComplexGeneratedPassword { get; set; }
    public bool ConfirmGeneratePassword { get; set; }
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

public sealed record TreeItemViewModel(string Id, string Text, ProjectItemType Type, bool Selected, bool Expanded,
    IReadOnlyList<TreeItemViewModel> Children);
public sealed record TreeBranchViewModel(TreeItemViewModel Node, long Revision);
public sealed record EditorViewModel(string Id, string Name, ProjectItemType Type, string Description,
    string Login, string Url, Priority Priority, Difficulty Difficulty, DateTime? DueDate, int Progress, bool IsLeaf);
public sealed record TodoViewModel(string Id, string Title, DateTime? DueDate);
public sealed record WorkspaceViewModel(long Revision, bool IsLoaded, bool IsLocked, bool IsDirty,
    string FileName, string Filter, IReadOnlyList<TreeItemViewModel> Tree,
    IReadOnlyList<TodoViewModel> Todos, EditorViewModel? Editor, string? RevealedPassword = null, string? Message = null,
    string? TransferKey = null);
