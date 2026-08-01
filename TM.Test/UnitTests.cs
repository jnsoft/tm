using TM.Entities;
using Task = TM.Entities.Task;

namespace TM.Test;

[TestClass]

public class UnitTests
{
    [TestMethod]
    public void TestProtectedItemToAndFromXml()
    {
        ProtectedItem item = new("test")
        {
            Password = "test"
        };

        XmlDocument doc = item.ToXml();
        ProtectedItem roundTripped = new(doc.DocumentElement);

        Assert.AreEqual(item.Changed.Value, roundTripped.Changed.Value);
        Assert.AreEqual(item.GetHashCode(), roundTripped.GetHashCode());
        Assert.AreEqual(item.ToXml().InnerXml, roundTripped.ToXml().InnerXml);
    }

    [TestMethod]
    public void TestSubtaskToAndFromXml()
    {
        List<Project> projects = TestDataBuilder.CreateSampleProjects();
        Subtask item = projects.First().Milestones.First().Tasks.First().SubTasks.First();

        XmlDocument doc = item.ToXml();
        Subtask roundTripped = new(doc.DocumentElement);

        Assert.AreEqual(item.GetHashCode(), roundTripped.GetHashCode());
        Assert.AreEqual(item.ToXml().InnerXml, roundTripped.ToXml().InnerXml);
    }

    [TestMethod]
    public void TestTaskToAndFromXml()
    {
        List<Project> projects = TestDataBuilder.CreateSampleProjects();
        Task item = projects.First().Milestones.First().Tasks.First();

        XmlDocument doc = item.ToXml();
        Task roundTripped = new(doc.DocumentElement);

        Assert.AreEqual(item.GetHashCode(), roundTripped.GetHashCode());
        Assert.AreEqual(item.ToXml().InnerXml, roundTripped.ToXml().InnerXml);
    }

    [TestMethod]
    public void TestMilestoneToAndFromXml()
    {
        List<Project> projects = TestDataBuilder.CreateSampleProjects();
        Milestone item = projects.First().Milestones.First();

        XmlDocument doc = item.ToXml();
        Milestone roundTripped = new(doc.DocumentElement);

        Assert.AreEqual(item.GetHashCode(), roundTripped.GetHashCode());
        Assert.AreEqual(item.ToXml().InnerXml, roundTripped.ToXml().InnerXml);
    }

    [TestMethod]
    public void TestProjectToAndFromXml()
    {
        List<Project> projects = TestDataBuilder.CreateSampleProjects();
        Project item = projects.First();

        XmlDocument doc = item.ToXml();
        Project roundTripped = new(doc.DocumentElement);

        Assert.AreEqual(item.GetHashCode(), roundTripped.GetHashCode());
        Assert.AreEqual(item.ToXml().InnerXml, roundTripped.ToXml().InnerXml);
    }
}