using Photino.NET;

namespace TM.Desktop.Services;

public static class DesktopApplication
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        PhotinoProjectFileDialogs dialogs = new();
        await using DesktopWebHost host = await DesktopWebHost.StartAsync(AppContext.BaseDirectory, cancellationToken, dialogs);
        string profile = Path.Combine(Path.GetTempPath(), "TM.Photino", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread ui = new(() =>
        {
            try
            {
                PhotinoWindow window = new PhotinoWindow()
                    .SetLogVerbosity(0)
                    .SetTitle("TM — Photino")
                    .SetUseOsDefaultSize(false)
                    .SetSize(1100, 760)
                    .SetMinSize(720, 480)
                    .SetTemporaryFilesPath(profile)
                    .SetContextMenuEnabled(false)
                    .SetDevToolsEnabled(false)
                    .SetGrantBrowserPermissions(false)
                    .SetFileSystemAccessEnabled(false)
                    .SetJavascriptClipboardAccessEnabled(false)
                    .SetMediaStreamEnabled(false)
                    .SetNotificationsEnabled(false)
                    .SetWebSecurityEnabled(true)
                    .SetIgnoreCertificateErrorsEnabled(false)
                    .Load(host.Security.BootstrapUri);
                dialogs.Attach(window);
                window.WaitForClose();
                completion.TrySetResult();
            }
            catch (Exception exception) { completion.TrySetException(exception); }
        }) { Name = "TM Photino UI", IsBackground = false };
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        try { await completion.Task; }
        finally
        {
            try { Directory.Delete(profile, recursive: true); }
            catch (IOException) { /* Webview processes can release their files after window closure. */ }
            catch (UnauthorizedAccessException) { /* Do not mask host shutdown with cache cleanup failures. */ }
        }
    }
}
