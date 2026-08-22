using System;
using System.Collections.Generic;
using System.Text;
using TM.Entities;
using TM.Services;

namespace TM.Test;

[TestClass]
public class MainWindowViewModelTests
{
    [TestMethod]
    public void AddProjectCommand_AddsAndSelectsProject()
    {
        MainWindowViewModel viewModel = CreateLoadedViewModel(out _);

        NodeModel? selectedFromEvent = null;
        viewModel.SelectNodeRequested += (_, node) => selectedFromEvent = node;

        int before = viewModel.Nodes.Count;

        viewModel.AddProjectCommand.Execute(null);

        Assert.AreEqual(before + 1, viewModel.Nodes.Count);
        Assert.IsNotNull(viewModel.SelectedNode);
        Assert.AreEqual(ProjectItemType.Project, viewModel.SelectedNode.NodeType);
        Assert.AreSame(viewModel.SelectedNode, selectedFromEvent);
    }

    [TestMethod]
    public void SaveCommand_CanExecute_TracksLoadedLockedAndPasswordEditorState()
    {
        MainWindowViewModel viewModel = CreateLoadedViewModel(out _);

        Assert.IsTrue(viewModel.SaveCommand.CanExecute(null));

        NodeModel protectedNode = TestDataBuilder.GetFirstProtectedNode(viewModel.Document);
        viewModel.SelectedNode = protectedNode;

        viewModel.BeginPasswordEdit();
        Assert.IsFalse(viewModel.SaveCommand.CanExecute(null));

        viewModel.CancelPasswordEdit();
        Assert.IsTrue(viewModel.SaveCommand.CanExecute(null));

        viewModel.Document.IsLocked = true;
        Assert.IsFalse(viewModel.SaveCommand.CanExecute(null));
    }

    [TestMethod]
    public void CommitPasswordEdit_EncryptsPasswordAndClearsEditorState()
    {
        string pass1 = new("updated-password");
        string pass2 = new("updated-password");
        MainWindowViewModel viewModel = CreateLoadedViewModel(out ProjectCryptoService crypto);
        NodeModel protectedNode = TestDataBuilder.GetFirstProtectedNode(viewModel.Document);

        viewModel.SelectedNode = protectedNode;
        viewModel.BeginPasswordEdit();
        viewModel.EditablePassword = pass1;

        viewModel.CommitPasswordEdit();

        Assert.IsFalse(viewModel.IsPasswordEditorActive);
        Assert.AreEqual(string.Empty, viewModel.EditablePassword);
        Assert.AreNotEqual(pass2, protectedNode.Password);
        Assert.AreEqual(pass2, crypto.DecryptSecret(viewModel.Document, protectedNode.Password));
    }

    [TestMethod]
    public void CanDrop_ReturnsFalseForInvalidTargets_AndTrueForValidMove()
    {
        MainWindowViewModel viewModel = CreateLoadedViewModel(out _);

        NodeModel project = viewModel.Nodes[0];
        NodeModel milestone = project.Nodes.First(node => node.NodeType == ProjectItemType.Milestone);
        NodeModel protectedNode = project.AllChildNodesFlat.First(node => node.IsProtected);

        Assert.IsFalse(viewModel.CanDrop(project, protectedNode));
        Assert.IsFalse(viewModel.CanDrop(project, milestone));
        Assert.IsTrue(viewModel.CanDrop(milestone, project));
    }

    [TestMethod]
    public void SelectTodo_FocusesAndSelectsMatchingNode()
    {
        MainWindowViewModel viewModel = CreateLoadedViewModel(out _);
        NodeModel todoNode = viewModel.Nodes
            .SelectMany(node => node.AllChildNodesFlat)
            .First(node => node.IsLeaf);

        todoNode.DueDate = DateTime.Today.AddDays(1);
        viewModel.RefreshTodos();

        ListItemModel todo = viewModel.Todos.Single(item => item.Id == todoNode.Id);

        viewModel.SelectTodo(todo);

        Assert.IsNotNull(viewModel.SelectedNode);
        Assert.AreEqual(todo.Id, viewModel.SelectedNode.Id);
        Assert.IsTrue(viewModel.SelectedNode.IsSelected);
    }

    [TestMethod]
    public void GeneratePasswordCommand_UsesConfiguredLength()
    {
        MainWindowViewModel viewModel = CreateViewModel(out _);
        viewModel.GeneratedPasswordLength = 24;
        viewModel.UseComplexGeneratedPassword = true;

        viewModel.GeneratePasswordCommand.Execute(null);

        Assert.AreEqual(24, viewModel.EditablePassword.Length);
        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.EditablePassword));
    }

    private static MainWindowViewModel CreateViewModel(out ProjectCryptoService crypto)
    {
        crypto = new ProjectCryptoService();
        UserInteractionService interactions = new();
        ShellService shell = new();
        ProjectDocumentService projectDocumentService = new(interactions, crypto);
        SecurityToolsService securityToolsService = new(interactions, shell, crypto);

        return new MainWindowViewModel(
            projectDocumentService,
            securityToolsService,
            interactions,
            shell,
            crypto);
    }

    private static MainWindowViewModel CreateLoadedViewModel(out ProjectCryptoService crypto)
    {
        MainWindowViewModel viewModel = CreateViewModel(out crypto);
        crypto.InitializeNew(viewModel.Document, "secret".ToSecureString());
        viewModel.Document.LoadProjects(TestDataBuilder.CreateSampleProjects());
        crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(viewModel.Document);
        viewModel.Document.RefreshTodos();
        return viewModel;
    }
}
