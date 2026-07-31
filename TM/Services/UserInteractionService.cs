using System;
using System.Collections.Generic;
using System.Text;
using TM.Helpers;

namespace TM.Services;

public sealed class UserInteractionService
{
    public bool TryGetOpenFilePath(
        string title,
        out string path,
        string fileTypes = "",
        string fileTypeEndingFilter = "") =>
        FileHelper.GetFileName(out path, title, fileTypes, fileTypeEndingFilter);

    public bool TryGetSaveFilePath(
        string title,
        out string path,
        string fileTypes = "",
        string fileTypeEndingFilter = "") =>
        FileHelper.SetFileName(out path, title, fileTypes, fileTypeEndingFilter);

    public bool TryGetPassword(string title, string prompt, out SecureString password) =>
        WpfDialogHelper.GetPassword(title, prompt, out password);

    public bool TryGetText(string title, string prompt, out string input) =>
        WpfDialogHelper.GetText(title, prompt, out input);

    public bool Confirm(
        string message,
        string title,
        MessageBoxButton buttons = MessageBoxButton.YesNo,
        MessageBoxImage image = MessageBoxImage.Question) =>
        MessageBox.Show(message, title, buttons, image) switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.OK => true,
            _ => false
        };

    public void ShowInfo(string message, string title = "Info") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title = "Warning") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title = "Error") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowError(Exception ex, string title = "Error") => ShowError(ex.Message, title);
}