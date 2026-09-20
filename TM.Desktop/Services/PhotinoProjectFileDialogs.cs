using Photino.NET;

namespace TM.Desktop.Services;

public sealed class PhotinoProjectFileDialogs : IProjectFileDialogs
{
    private PhotinoWindow? window;
    public void Attach(PhotinoWindow desktopWindow) => window = desktopWindow;

    public async Task<string?> OpenAsync(CancellationToken cancellationToken = default)
    {
        PhotinoWindow current = window ?? throw new InvalidOperationException("Native dialogs are not available.");
        string[] paths = await current.ShowOpenFileAsync("Open TM document", filters: [("TM documents", ["xml"])])
            .WaitAsync(cancellationToken);
        return paths.FirstOrDefault();
    }

    public async Task<string?> SaveAsync(string? currentPath, CancellationToken cancellationToken = default)
    {
        PhotinoWindow current = window ?? throw new InvalidOperationException("Native dialogs are not available.");
        string? path = await current.ShowSaveFileAsync("Save TM document", currentPath ?? "projects.xml", [("TM documents", ["xml"])])
            .WaitAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }
}
