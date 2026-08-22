using FlaUI.Core;
using FlaUI.UIA3;

namespace TM.UITest
{
    [TestClass]
    public class StarterTest
    {
        [TestMethod]
        public void Application_StartsAndShowsMainWindow()
        {
            string executablePath = Path.GetFullPath(
                @"..\..\..\..\..\TM\bin\Debug\net10.0-windows\win-x64\TM.exe");

            using Application application = Application.Launch(executablePath);
            using UIA3Automation automation = new();

            FlaUI.Core.AutomationElements.Window mainWindow =
                application.GetMainWindow(automation);

            Assert.IsNotNull(mainWindow);
            Assert.IsTrue(mainWindow.Title.Contains("Task manager"));

            mainWindow.Close();
        }
    }
}
