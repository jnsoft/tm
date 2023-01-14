using TM.Entities;
using Task = TM.Entities.Task;

namespace TM.Test;

[TestClass]

public class UnitTests
{
    [TestMethod]
    public void TestProtectedItemToAndFromXml()
    {
        // Arrange
        ProtectedItem x = new ProtectedItem("test");
        x.Password = "test";

        // Act
        XmlDocument doc = x.ToXml();
        ProtectedItem x2 = new ProtectedItem(doc.DocumentElement);


        // Assert
        Assert.AreEqual(x.Changed.Value, x2.Changed.Value);
        Assert.AreEqual(x.GetHashCode(), x2.GetHashCode());
        Assert.AreEqual(x.ToXml().InnerXml, x2.ToXml().InnerXml);
    }

    [TestMethod]
    public void TestSubtaskToAndFromXml()
    {
        // Arrange
        List<Project> Projects = MainWindowModelTests.getSampleProjects();
        Subtask x = Projects.First().Milestones.First().Tasks.First().SubTasks.First();

        // Act
        XmlDocument doc = x.ToXml();
        Subtask x2 = new Subtask(doc.DocumentElement);

        // Assert
        Assert.AreEqual(x.GetHashCode(), x2.GetHashCode());
        Assert.AreEqual(x.ToXml().InnerXml, x2.ToXml().InnerXml);
    }

    [TestMethod]
    public void TestTaskToAndFromXml()
    {
        // Arrange
        List<Project> Projects = MainWindowModelTests.getSampleProjects();
        Task t = Projects.First().Milestones.First().Tasks.First();

        // Act
        XmlDocument doc = t.ToXml();
        Task t2 = new Task(doc.DocumentElement);

        string xml_org = t.ToXml().InnerXml;
        string xml_new = t2.ToXml().InnerXml;

        // Assert
        Assert.AreEqual(t.GetHashCode(), t2.GetHashCode());
        Assert.AreEqual(xml_org, xml_new);
    }

    [TestMethod]
    public void TestMilestoneToAndFromXml()
    {
        // Arrange
        List<Project> Projects = MainWindowModelTests.getSampleProjects();
        Milestone x = Projects.First().Milestones.First();

        // Act
        XmlDocument doc = x.ToXml();
        Milestone x2 = new Milestone(doc.DocumentElement);


        // Assert
        Assert.AreEqual(x.GetHashCode(), x2.GetHashCode());
        Assert.AreEqual(x.ToXml().InnerXml, x2.ToXml().InnerXml);
    }

    [TestMethod]
    public void TestProjectToAndFromXml()
    {
        // Arrange
        List<Project> Projects = MainWindowModelTests.getSampleProjects();
        Project x = Projects.First();

        // Act
        XmlDocument doc = x.ToXml();
        Project x2 = new Project(doc.DocumentElement);


        // Assert
        Assert.AreEqual(x.GetHashCode(), x2.GetHashCode());
        Assert.AreEqual(x.ToXml().InnerXml, x2.ToXml().InnerXml);
    }
}