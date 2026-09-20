using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Xml;
using TM.Desktop.Services;
using TM.Desktop.ViewModels;

namespace TM.Desktop.Pages;

public sealed class IndexModel(ShellViewModel viewModel, WorkspaceService workspace) : PageModel
{
    public ShellViewModel ViewModel => viewModel;
    [BindProperty] public WorkspaceCommand Input { get; set; } = new();
    public WorkspaceViewModel? Workspace { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Workspace = await workspace.SnapshotAsync(cancellationToken);

    public IActionResult OnPostCheck() => Partial("_Status", viewModel);

    public async Task<IActionResult> OnPostWorkspaceAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                throw new ValidationException("Check the submitted field values and lengths.");
            Workspace = await workspace.ExecuteAsync(Input, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ValidationException)
        {
            Workspace = (await workspace.SnapshotAsync(cancellationToken)) with { Message = exception.Message };
        }
        catch (Exception exception) when (exception is CryptographicException or XmlException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Workspace = (await workspace.SnapshotAsync(cancellationToken)) with
            {
                Message = "The document operation failed. Check the password, file and access permissions."
            };
        }
        finally
        {
            Input.Password = "";
            Input.NewPassword = "";
            Input.ConfirmNewPassword = "";
            Input.Secret = "";
            ModelState.Clear();
        }
        return Request.Headers.ContainsKey("HX-Request") ? Partial("_Workspace", Workspace) : Page();
    }
}
