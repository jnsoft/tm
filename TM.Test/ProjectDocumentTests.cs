using TM.Entities;
using Task = TM.Entities.Task;
using TM.Services;
using System.Security.Cryptography;

namespace TM.Test;

[TestClass]
public class ProjectDocumentTests
{
    [TestMethod]
    public void Constructor_InitializesEmptyUnlockedDocument()
    {
        ProjectDocument document = new();

        Assert.IsFalse(document.IsLocked);
        Assert.IsFalse(document.IsFileLoaded);
        Assert.IsTrue(document.IsEmpty);
        Assert.AreEqual(0, document.Nodes.Count);
        Assert.AreEqual(0, document.Todos.Count);
    }

    [TestMethod]
    public void LoadProjects_SetsFlagsAndSortsRootNodesByName()
    {
        ProjectDocument document = new();
        List<Project> projects =
        [
            new Project("Zeta"),
            new Project("Alpha")
        ];

        document.LoadProjects(projects);

        Assert.IsTrue(document.IsFileLoaded);
        Assert.IsFalse(document.IsLocked);
        Assert.IsFalse(document.IsEmpty);
        Assert.AreEqual("Alpha", document.Nodes[0].Text);
        Assert.AreEqual("Zeta", document.Nodes[1].Text);
    }

    [TestMethod]
    public void GetProjects_ReturnsEquivalentProjectsAfterLoadProjects()
    {
        ProjectDocument document = new();
        List<Project> projects = TestDataBuilder.CreateSampleProjects();

        document.LoadProjects(projects);
        List<Project> loadedProjects = document.GetProjects();

        Assert.AreEqual(projects[0].GetHashCode(), loadedProjects[0].GetHashCode());
        CollectionAssert.AreEqual(projects, loadedProjects);
    }

    [TestMethod]
    public void GetNodeById_ReturnsNestedNode()
    {
        ProjectDocument document = new();
        document.LoadProjects(TestDataBuilder.CreateSampleProjects());

        NodeModel expected = document.Nodes[0]
            .AllChildNodesFlat
            .First(node => node.Text == "Subtask1");

        NodeModel? actual = document.GetNodeById(expected.Id);

        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.Id, actual.Id);
        Assert.AreEqual("Subtask1", actual.Text);
    }

    [TestMethod]
    public void AddNode_AddsRootNodeAndUpdatesEmptyState()
    {
        ProjectDocument document = new();
        NodeModel node = new(new Project("New Project"));

        document.AddNode(node);

        Assert.IsFalse(document.IsEmpty);
        Assert.AreEqual(1, document.Nodes.Count);
        Assert.AreEqual(node.Id, document.Nodes[0].Id);
    }

    [TestMethod]
    public void DeleteNode_RemovesNestedNode()
    {
        ProjectDocument document = new();
        document.LoadProjects(TestDataBuilder.CreateSampleProjects());

        NodeModel node = document.Nodes[0]
            .AllChildNodesFlat
            .First(item => item.Text == "Subtask1");

        document.DeleteNode(node);

        Assert.IsNull(document.GetNodeById(node.Id));
    }

    [TestMethod]
    public void RefreshTodos_IncludesOnlyIncompleteLeafItemsWithDueDate_OrderedByDateThenPriority()
    {
        ProjectDocument document = new();

        Project project = new("Project");
        Milestone milestone = new("Milestone");

        Task later = new("Later", DateTime.Today.AddDays(2))
        {
            Priority = Priority.Low,
            Progress = 10
        };

        Task soonerLow = new("SoonerLow", DateTime.Today.AddDays(1))
        {
            Priority = Priority.Low,
            Progress = 10
        };

        Task soonerHigh = new("SoonerHigh", DateTime.Today.AddDays(1))
        {
            Priority = Priority.High,
            Progress = 10
        };

        Task done = new("Done", DateTime.Today)
        {
            Progress = 100
        };

        Task withoutDate = new("WithoutDate")
        {
            Progress = 10
        };

        milestone.Tasks.AddRange([later, soonerLow, soonerHigh, done, withoutDate]);
        project.Milestones.Add(milestone);

        document.LoadProjects([project]);
        document.RefreshTodos();

        string[] titles = [.. document.Todos.Select(todo => todo.Title)];

        CollectionAssert.AreEqual(
            new[] { "SoonerHigh", "SoonerLow", "Later" },
            titles);
    }

    [TestMethod]
    public void LoadUnencrypted_LoadsProjectsFromUnencryptedXml()
    {
        string pass1 = new string("secret");
        string pass2 = new string("secret");
        string pass3 = new string("secret2");
        ProjectCryptoService crypto = new();
        ProjectDocument source = new();
        crypto.InitializeNew(source, pass1.ToSecureString());
        source.LoadProjects(TestDataBuilder.CreateSampleProjects());
        crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(source);

        XmlDocument unencrypted = crypto.GetUnencryptedXml(source, pass2.ToSecureString());
        ProjectDocument loaded = new();
        crypto.InitializeNew(loaded, pass3.ToSecureString());
        loaded.LoadUnencrypted(unencrypted);
        crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(loaded);

        Assert.HasCount(source.Nodes.Count, loaded.Nodes);
    }
}
