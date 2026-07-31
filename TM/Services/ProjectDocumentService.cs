
namespace TM.Services;

public sealed class ProjectDocumentService(UserInteractionService interactions)
{
    private const string DefaultFileName = "projects.xml";
    private const string FileOpenError = "Failed to open file";

    public static string DefaultPath => Path.Combine(Directory.GetCurrentDirectory(), DefaultFileName);

    public ProjectDocumentSession CreateEmptySession() => new(new MainWindowModel(), DefaultPath);

    public ProjectDocumentSession? CreateNewSession(bool hasExistingContent)
    {
        bool okToClear = !hasExistingContent ||
            interactions.Confirm(
                "Click OK to clear the project tree",
                "Clear project tree",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

        if (!okToClear)
            return null;

        if (!interactions.TryGetPassword("New collection", "Set password:", out SecureString password))
            return null;

        MainWindowModel model = new(password)
        {
            IsFileLoaded = true
        };

        return new ProjectDocumentSession(model, BuildNewProjectFilePath());
    }

    public ProjectDocumentSession? OpenSession()
    {
        if (!interactions.TryGetOpenFilePath("Open project file", out string path, "Xml Files", "xml"))
            return null;

        try
        {
            if (!interactions.TryGetPassword("Password", "Enter password", out SecureString password))
                return null;

            XmlDocument doc = XMLhelper.XmlFromFile(path);
            MainWindowModel model = new(doc, password);

            interactions.ShowInfo($"{path} loaded", "Load file");
            return new ProjectDocumentSession(model, path);
        }
        catch (CryptographicException)
        {
            interactions.ShowWarning("Decryption failed", FileOpenError);
            return null;
        }
        catch
        {
            interactions.ShowWarning("Could not parse XML", FileOpenError);
            return null;
        }
    }

    public void SaveSession(ProjectDocumentSession session)
    {
        XmlElement root = session.Model.GetAsEncryptedXML().DocumentElement
            ?? throw new InvalidOperationException("Encrypted XML has no root element.");

        XMLhelper.XmlToFile(root, session.FilePath);

        foreach (NodeModel node in session.Model.Nodes)
            node.ResetSave();

        interactions.ShowInfo($"Project tree saved to {session.FilePath}", "Save file");
    }

    public ProjectDocumentSession? SaveSessionAs(ProjectDocumentSession session)
    {
        if (!interactions.TryGetSaveFilePath("Save project file", out string path, "Xml Files", "xml"))
            return null;

        ProjectDocumentSession newSession = session with { FilePath = path };
        SaveSession(newSession);
        return newSession;
    }

    public void SaveUnencryptedSession(MainWindowModel model)
    {
        if (!interactions.TryGetSaveFilePath("Save unencrypted project file", out string path, "Save Files", "sav"))
            return;

        if (!interactions.TryGetPassword("Password", "Enter password:", out SecureString pass))
            return;

        try
        {
            XmlDocument doc = model.GetUnencryptedXML(pass);
            byte[] key = SecurityHelper.GetRandomKey(32);
            string fileContent = SecurityHelper.GCMEncrypt(doc.InnerXml.ToByte(), key).ToBase64();
            File.WriteAllText(path, fileContent);

            string keyText = key.ToBase64();
            interactions.ShowInfo(
                $"Unencrypted projects file saved to {path}.\n\nTo unlock file, use key:\n{keyText}\n\n(Press Ctrl+C to copy message box contents)",
                "Save file");

            SecurityHelper.ZeroString(keyText);
            ArrayHelper.ClearArray(ref key);
        }
        catch
        {
            interactions.ShowWarning("Could not save unencrypted", "Save unencrypted");
        }
    }

    public ProjectDocumentSession? LoadUnencryptedSession()
    {
        if (!interactions.TryGetOpenFilePath("Open unencrypted file", out string filePath, "Save Files", "sav"))
            return null;

        if (!interactions.TryGetPassword("Set password", "Set new password:", out SecureString password))
            return null;

        if (!interactions.TryGetPassword("Unlock file", "Enter unlock key:", out SecureString unlockKey))
            return null;

        try
        {
            using (unlockKey)
            {
                byte[] byteKey = unlockKey.ToInsecureString().FromBase64();
                string fileStringContent = File.ReadAllText(filePath);
                byte[] fileContent = SecurityHelper.GCMDecrypt(fileStringContent.FromBase64(), byteKey);

                XmlDocument doc = new();
                doc.LoadXml(fileContent.ToStringFromByte());

                MainWindowModel model = new(password);
                model.LoadUnencrypted(doc);
                model.EncryptProtectedItemsAfterLoadingUnencryptedProjects();
                model.IsFileLoaded = true;

                MainWindowModel.ClearArr(ref byteKey);

                return new ProjectDocumentSession(model, BuildNewProjectFilePath());
            }
        }
        catch (Exception ex)
        {
            interactions.ShowError($"Could not load {filePath}. Please check file. {ex.Message}", "Load unencrypted file");
            return null;
        }
    }

    private static string BuildNewProjectFilePath()
    {
        string defaultPath = DefaultPath;
        return File.Exists(defaultPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), $"{DateTime.Now.Ticks}.xml")
            : defaultPath;
    }
}
