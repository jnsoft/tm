using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TM.Desktop.Services;
using TM.Services;
using System.ComponentModel.DataAnnotations;

namespace TM.Desktop.Pages;

public sealed class FileToolsModel(FileToolsService tools) : PageModel
{
    [BindProperty] public FileUtilityOperation Operation { get; set; } = FileUtilityOperation.Sha256;
    [BindProperty] public PasswordFileOperation PasswordOperation { get; set; }
    [BindProperty, StringLength(4096)] public string FilePassword { get; set; } = "";
    [BindProperty, StringLength(4096)] public string ConfirmFilePassword { get; set; } = "";
    public string StatusMessage { get; private set; } = "Ready.";

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        StatusMessage = ModelState.IsValid && Enum.IsDefined(Operation)
            ? await tools.ExecuteAsync(Operation, cancellationToken)
            : "Choose a supported file operation.";
        ModelState.Clear();
        return Request.Headers.ContainsKey("HX-Request") ? Partial("_FileTools", StatusMessage) : Page();
    }

    public async Task<IActionResult> OnPostPasswordAsync(CancellationToken cancellationToken)
    {
        try
        {
            StatusMessage = ModelState.IsValid && Enum.IsDefined(PasswordOperation)
                ? await tools.ExecutePasswordAsync(PasswordOperation, FilePassword ?? "", ConfirmFilePassword ?? "", cancellationToken)
                : "Check the file operation and password lengths.";
        }
        finally
        {
            FilePassword = "";
            ConfirmFilePassword = "";
            ModelState.Clear();
        }
        return Request.Headers.ContainsKey("HX-Request") ? Partial("_FileTools", StatusMessage) : Page();
    }
}
