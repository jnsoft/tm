using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TM.Desktop.Services;
using TM.Services;

namespace TM.Desktop.Pages;

public sealed class FileToolsModel(FileToolsService tools) : PageModel
{
    [BindProperty] public FileUtilityOperation Operation { get; set; } = FileUtilityOperation.Sha256;
    public string StatusMessage { get; private set; } = "Ready.";

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        StatusMessage = ModelState.IsValid && Enum.IsDefined(Operation)
            ? await tools.ExecuteAsync(Operation, cancellationToken)
            : "Choose a supported file operation.";
        ModelState.Clear();
        return Request.Headers.ContainsKey("HX-Request") ? Partial("_FileTools", StatusMessage) : Page();
    }
}
