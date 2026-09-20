using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TM.Desktop.ViewModels;

namespace TM.Desktop.Pages;

public sealed class IndexModel(ShellViewModel viewModel) : PageModel
{
    public ShellViewModel ViewModel => viewModel;
    public void OnGet() { }
    public IActionResult OnPostCheck() => Partial("_Status", viewModel);
}
