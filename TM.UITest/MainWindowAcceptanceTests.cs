using FlaUI.Core.WindowsAPI;
using FlaUI.Core;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Text;
using System.Xml;
using TM.Models;
using TM.Services;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Definitions;

namespace TM.UITest;

[TestClass]
public class MainWindowAcceptanceTests
{
    private const string Password = "acceptance-test-password";
    private const string fileName = "acc-test-projects.xml";

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

            AddProject(mainWindow, automation, "Acceptance Project");
            AddTask(mainWindow, automation, "Acceptance Task 1", DateTime.Today.AddDays(7));
            AddTask(mainWindow, automation, "Acceptance Task 2", DateTime.Today.AddDays(14));

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
            application?.Dispose();

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

        tree.RightClick();

        FindMenuItem(automation.GetDesktop(), "Add Project").Click();

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

        projectText.RightClick();

        FindMenuItem(automation.GetDesktop(), "Add Task").Click();

        SetName(mainWindow, taskName);
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
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);

        Window saveConfirmation = WaitForWindow(application, automation, "Save file");

        FindMenuItem(saveConfirmation, "OK").Click();
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

    private static string GetApplicationExecutablePath()
    {
        string executablePath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                @"..\..\..\..\TM\bin\Debug\net10.0-windows\win-x64\TM.exe"));

        Assert.IsTrue(
            File.Exists(executablePath),
            $"Build the TM project before running UI tests. Missing: {executablePath}");

        return executablePath;
    }
}
