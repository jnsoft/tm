using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace TM.UITest;

[TestClass]
public sealed class PhotinoShellTests
{
    [TestMethod]
    [Timeout(60000)]
    public async Task DesktopShell_RendersAuthenticatedPageAndClosesAsync()
    {
        string executable = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../TM.Desktop/bin/Debug/net10.0-windows/win-x64/TM.Desktop.exe"));
        string workingDirectory = Path.Combine(Path.GetTempPath(), $"TM.PhotinoSmoke.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        using Process process = Process.Start(new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Could not start Photino desktop.");
        try
        {
            using Application application = Application.Attach(process.Id);
            using UIA3Automation automation = new();
            Window window = application.GetMainWindow(automation, TimeSpan.FromSeconds(20));
            Assert.IsNotNull(window);
            Assert.IsTrue(window.Title.Contains("Photino", StringComparison.Ordinal));
            AutomationElement? check = null;
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
            while (check is null && !timeout.IsCancellationRequested)
            {
                check = window.FindFirstDescendant(cf => cf.ByName("Check local session"));
                if (check is null)
                    await Task.Delay(100);
            }
            Assert.IsNotNull(check, "The authenticated Razor page must render in the native webview.");
            check.AsButton().Invoke();
            window.Close();
            using CancellationTokenSource exitTimeout = new(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(exitTimeout.Token);
            Assert.AreEqual(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}
