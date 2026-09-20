using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace TM.UITest;

[TestClass]
public sealed class PhotinoWorkspaceTests
{
    [TestMethod]
    [Timeout(60000)]
    public async Task NewCollection_AddAndEditProject_UpdatesNativeWebviewAsync()
    {
        string executable = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../TM.Desktop/bin/Debug/net10.0-windows/win-x64/TM.Desktop.exe"));
        using Process process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false })
            ?? throw new InvalidOperationException("Could not start Photino.");
        try
        {
            using Application application = Application.Attach(process.Id);
            using UIA3Automation automation = new();
            Window window = application.GetMainWindow(automation, TimeSpan.FromSeconds(20));
            (await FindAsync(window, "Document password", ControlType.Edit)).AsTextBox().Text = "native-test-password";
            (await FindAsync(window, "New collection", ControlType.Button)).AsButton().Invoke();
            (await FindAsync(window, "New item name", ControlType.Edit)).AsTextBox().Text = "Native project";
            (await FindAsync(window, "Add item", ControlType.Button)).AsButton().Invoke();
            await FindAsync(window, "Project: Native project", ControlType.Button);
            (await FindAsync(window, "Name", ControlType.Edit)).AsTextBox().Text = "Native renamed project";
            (await FindAsync(window, "Apply edits", ControlType.Button)).AsButton().Invoke();
            await FindAsync(window, "Project: Native renamed project", ControlType.Button);
            // Unsaved-close behavior is a separate acceptance concern; do not save synthetic data.
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    private static async Task<AutomationElement> FindAsync(Window window, string name, ControlType controlType)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(12));
        while (!timeout.IsCancellationRequested)
        {
            AutomationElement? element = window.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(controlType)));
            if (element is not null) return element;
            await Task.Delay(100);
        }
        Assert.Fail($"Native webview did not expose {controlType} '{name}'.");
        throw new InvalidOperationException("Unreachable.");
    }
}
