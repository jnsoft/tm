using System;
using System.Collections.Generic;
using System.Text;
using TM.Entities;
using TM.Services;
using Task = TM.Entities.Task;

namespace TM.Test;

internal static class TestDataBuilder
{
    public static List<Project> CreateSampleProjects()
    {
        Project project = new("Top node 1");

        Milestone milestone1 = new("Milestone 1", DateTime.Today.AddDays(10));
        Milestone milestone2 = new("Milestone 2", DateTime.Today.AddDays(20));

        Task task1 = new("Task1", DateTime.Today.AddDays(5))
        {
            Priority = Priority.Medium
        };

        Task task2 = new("Task2", DateTime.Today.AddDays(1))
        {
            Priority = Priority.High,
            Progress = 25
        };

        Task task3 = new("Task3")
        {
            Progress = 100
        };

        Subtask subtask1 = new("Subtask1", DateTime.Today.AddDays(2))
        {
            Priority = Priority.Critical,
            Progress = 50
        };

        Subtask subtask2 = new("Subtask2")
        {
            Progress = 100
        };

        ProtectedItem protected1 = new("Protected1")
        {
            Login = "user1",
            Password = "password1",
            Url = "https://example.com/1"
        };

        ProtectedItem protected2 = new("Protected2")
        {
            Login = "user2",
            Password = "password2",
            Url = "https://example.com/2"
        };

        ProtectedItem protected3 = new("Protected3") { Password = "password3" };
        ProtectedItem protected4 = new("Protected4") { Password = "password4" };
        ProtectedItem protected5 = new("Protected5") { Password = "password5" };
        ProtectedItem protected6 = new("Protected6") { Password = "password6" };

        protected5.Items.Add(protected6);
        protected3.Items.Add(protected4);
        protected3.Items.Add(protected5);

        task2.ProtectedItems.Add(protected1);
        subtask1.ProtectedItems.Add(protected2);
        task1.ProtectedItems.Add(protected3);
        task1.SubTasks.Add(subtask1);
        task1.SubTasks.Add(subtask2);

        milestone1.Tasks.Add(task1);
        milestone2.Tasks.Add(task2);
        milestone2.Tasks.Add(task3);

        project.Milestones.Add(milestone1);
        project.Milestones.Add(milestone2);

        return [project];
    }

    public static ProjectDocument CreateLoadedEncryptedDocument(
        ProjectCryptoService crypto,
        string password = "secret")
    {
        ProjectDocument document = new();
        crypto.InitializeNew(document, password.ToCharArray().ToSecureStringAndClear());
        document.LoadProjects(CreateSampleProjects());
        crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(document);
        document.RefreshTodos();
        return document;
    }

    public static NodeModel GetFirstProtectedNode(ProjectDocument document) =>
        document.Nodes
            .SelectMany(node => node.AllChildNodesFlat)
            .First(node => node.IsProtected);
}
