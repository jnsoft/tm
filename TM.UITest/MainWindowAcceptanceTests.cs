using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Text;
//using System.Windows.Automation;
using System.Xml;
using TM.Models;
using TM.Services;

namespace TM.UITest;

[TestClass]
public class MainWindowAcceptanceTests
{
    private const string Password = "acceptance-test-password";
    private const string fileName = "projects.xml";
    private const string exeRelativePath = @"..\..\..\..\..\TM\bin\Debug\net10.0-windows\win-x64\TM.exe";
    private const int SHORT_TIMEOUT = 100;
    private const int MEDIUM_TIMEOUT = 250;
    private const int LONG_TIMEOUT = 500;

    private static string applicationExecutablePath() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,exeRelativePath));
        
    

    [TestMethod]
    [Timeout(60000)]
    public void NewProject_AddTasks_SaveAndReload_RoundTripsEncryptedDocument()
    {
        string workingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"TM.UITest.{Guid.NewGuid():N}");

        Directory.CreateDirectory(workingDirectory);
        Application? application = null;

        try
        {
            string executablePath = GetApplicationExecutablePath();
            string expectedFilePath = Path.Combine(workingDirectory, fileName);

            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("Could not start TM.exe.");

            application = Application.Attach(process);

            using UIA3Automation automation = new();
            Window mainWindow = application.GetMainWindow(automation);

            Assert.IsNotNull(mainWindow);

            CreateNewEncryptedCollection(mainWindow, application, automation);
            System.Threading.Thread.Sleep(MEDIUM_TIMEOUT);
            AddProject(mainWindow, automation, "Acceptance Project");
            System.Threading.Thread.Sleep(SHORT_TIMEOUT);
            AddTask(mainWindow, automation, "Acceptance Task 1", DateTime.Today.AddDays(7));
            System.Threading.Thread.Sleep(SHORT_TIMEOUT);
            AddTask(mainWindow, automation, "Acceptance Task 2", DateTime.Today.AddDays(14));
            System.Threading.Thread.Sleep(SHORT_TIMEOUT);
            Save(mainWindow, application, automation);
            mainWindow.Close();

            Assert.IsTrue(
                WaitUntil(() => File.Exists(expectedFilePath)),
                $"Expected saved file was not created: {expectedFilePath}");

            ProjectDocument loadedDocument = LoadEncryptedDocument(expectedFilePath, Password);

            Assert.IsTrue(loadedDocument.IsFileLoaded);
            Assert.HasCount(1, loadedDocument.Nodes);

            NodeModel project = loadedDocument.Nodes.Single();
            Assert.AreEqual("Acceptance Project", project.Text);

            List<NodeModel> tasks = project.AllChildNodesFlat
                .Where(node => node.Text.StartsWith("Acceptance Task", StringComparison.Ordinal))
                .ToList();

            Assert.HasCount(2, tasks);
            Assert.IsTrue(tasks.Any(task => task.Text == "Acceptance Task 1"));
            Assert.IsTrue(tasks.Any(task => task.Text == "Acceptance Task 2"));
            Assert.IsTrue(tasks.All(task => task.DueDate.HasValue));

        }
        finally
        {
            if (application != null)
            {
                // 1. Fetch the native OS process via FlaUI's process ID property
                var nativeProcess = Process.GetProcessById(application.ProcessId);

                application.Close();

                // 2. Disconnect FlaUI 
                application.Dispose();

                // 3. Wait for the operating system to completely terminate it
                // (Timeout of 2000ms ensures it won't hang if the app freezes)
                nativeProcess?.WaitForExit(2000);
            }

            if (Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static void CreateNewEncryptedCollection(
    Window mainWindow,
    Application application,
    UIA3Automation automation)
    {
        FindMenuItem(mainWindow, "File").Click();
        FindMenuItem(mainWindow, "New").Click();

        Window passwordDialog = WaitForWindow(application, automation, "New collection");

        passwordDialog.Focus();
        Keyboard.Type(Password);
        Keyboard.Press(VirtualKeyShort.RETURN);
    }

    private static void AddProject(
        Window mainWindow,
        UIA3Automation automation,
        string projectName)
    {
        AutomationElement tree = mainWindow.FindFirstDescendant(
            cf => cf.ByAutomationId("ProjectTree"));
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        tree.RightClick();
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        FindMenuItem(automation.GetDesktop(), "Add Project").Click();
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        SetName(mainWindow, projectName);
    }

    private static void AddTask(
        Window mainWindow,
        UIA3Automation automation,
        string taskName,
        DateTime dueDate)
    {
        AutomationElement projectText = mainWindow.FindFirstDescendant(
            cf => cf.ByText("Acceptance Project"));
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        projectText.RightClick();
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        FindMenuItem(automation.GetDesktop(), "Add Task").Click();
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        SetName(mainWindow, taskName);
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        SetDueDate(mainWindow, dueDate);
    }

    private static void SetName(Window mainWindow, string name)
    {
        TextBox nameEditor = mainWindow.FindFirstDescendant(
            cf => cf.ByAutomationId("txtName"))
            .AsTextBox();

        nameEditor.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(name);
        Keyboard.Press(VirtualKeyShort.TAB);
    }

    private static void SetDueDate(Window mainWindow, DateTime dueDate)
    {
        AutomationElement datePicker = mainWindow.FindFirstDescendant(
            cf => cf.ByAutomationId("dpDueDate"));

        TextBox dateEditor = datePicker.FindFirstDescendant(
            cf => cf.ByControlType(ControlType.Edit))
            .AsTextBox();

        dateEditor.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(dueDate.ToShortDateString());
        Keyboard.Press(VirtualKeyShort.TAB);
    }

    private static void Save(
        Window mainWindow,
        Application application,
        UIA3Automation automation)
    {
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        Window saveConfirmation = WaitForModalWindow(application, automation, "Save file");
        System.Threading.Thread.Sleep(SHORT_TIMEOUT);
        FindButton(saveConfirmation, "OK").Click();
    }

    private static ProjectDocument LoadEncryptedDocument(
        string filePath,
        string password)
    {
        XmlDocument encryptedXml = new();
        encryptedXml.Load(filePath);

        ProjectDocument document = new();
        ProjectCryptoService crypto = new();

        using SecureString securePassword = CreateSecureString(password);
        crypto.LoadEncryptedDocument(document, encryptedXml, securePassword);

        return document;
    }

    private static SecureString CreateSecureString(string value)
    {
        SecureString secureValue = new();

        foreach (char character in value)
            secureValue.AppendChar(character);

        secureValue.MakeReadOnly();
        return secureValue;
    }

    private static Window WaitForWindow(
        Application application,
        UIA3Automation automation,
        string title)
    {
        Window? window = null;

        bool found = WaitUntil(() =>
        {
            window = application
                .GetAllTopLevelWindows(automation)
                .FirstOrDefault(candidate => candidate.Title == title);

            return window is not null;
        });

        Assert.IsTrue(found, $"Window '{title}' was not shown.");
        return window!;
    }

    private static Window WaitForModalWindow(
        Application application,
        UIA3Automation automation,
        string title)
    {
        Window? window = null;

        bool found = WaitUntil(() =>
        {
            // 1. Get your application's primary window first
            var mainWindow = application.GetMainWindow(automation);
            if (mainWindow == null) return false;

            // 2. Search for the popup box directly inside that main window
            var messageBoxElement = mainWindow.FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(candidate => candidate.Name != null && candidate.Name.Contains(title));

            if (messageBoxElement != null)
            {
                window = messageBoxElement.AsWindow();
                return true;
            }

            return false;
        });

        Assert.IsTrue(found, $"Window '{title}' was not shown.");
        return window!;
    }

    private static Window WaitForGlobalWindow(
        Application application,
        UIA3Automation automation,
        string title)
    {
        Window? window = null;

        bool found = WaitUntil(() =>
        {

            // Search absolutely all immediate elements on the desktop
            var desktopChildren = automation.GetDesktop().FindAllChildren();

            var openTitles = desktopChildren.Select(c => c.Name).ToList();

            var targetElement = desktopChildren.FirstOrDefault(c =>
                c.Name != null && c.Name.Contains(title));

            window = targetElement?.AsWindow();
            return window is not null;

            

            // Query by .Name explicitly
            window = automation.GetDesktop()
                .FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(candidate => candidate.Name != null && candidate.Name.Contains(title))?
                .AsWindow();

            return window is not null;

            // Search the global desktop instead of the application process
            window = automation.GetDesktop()
                .FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(candidate => candidate.AsWindow().Title == title)?
                .AsWindow();

            return window is not null;
        });

        Assert.IsTrue(found, $"Window '{title}' was not shown.");
        return window!;
    }


    

    private static AutomationElement FindDesktopElement(
        UIA3Automation automation,
        string text)
    {
        AutomationElement? element = null;

        bool found = WaitUntil(() =>
        {
            element = automation.GetDesktop()
                .FindFirstDescendant(cf => cf.ByText(text));

            return element is not null;
        });

        Assert.IsTrue(found, $"UI element '{text}' was not found.");
        return element!;
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMilliseconds = 10000)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if (condition())
                return true;

            Thread.Sleep(100);
        }

        return false;
    }

    private static MenuItem FindMenuItem(AutomationElement root, string name)
    {
        AutomationElement? element = root.FindFirstDescendant(
            cf => cf.ByControlType(ControlType.MenuItem).And(cf.ByName(name)));

        Assert.IsNotNull(element, $"Menu item '{name}' was not found.");

        MenuItem? menuItem = element.AsMenuItem();
        Assert.IsNotNull(menuItem, $"Element '{name}' is not a menu item.");

        return menuItem;
    }

    private static Button FindButton(AutomationElement root, string name)
    {
        // Use ControlType.Button instead of MenuItem
        AutomationElement? element = root.FindFirstDescendant(
            cf => cf.ByControlType(ControlType.Button).And(cf.ByName(name)));

        Assert.IsNotNull(element, $"Button '{name}' was not found.");

        // Cast to FlaUI Button
        Button? button = element.AsButton();
        Assert.IsNotNull(button, $"Element '{name}' is not a button.");

        return button;
    }

    private static string GetApplicationExecutablePath()
    {
        
        Assert.IsTrue(
            File.Exists(applicationExecutablePath()),
            $"Build the TM project before running UI tests. Missing: {applicationExecutablePath()}");

        return applicationExecutablePath();
    }
}

