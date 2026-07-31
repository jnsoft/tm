
using System.Reflection.Metadata;
using TM.Entities;

namespace TM.Services;

public sealed class ProjectDocumentService(UserInteractionService interactions, ProjectCryptoService crypto)
{
    private const string DefaultFileName = "projects.xml";
    private const string FileOpenError = "Failed to open file";

    public static string DefaultPath => Path.Combine(Directory.GetCurrentDirectory(), DefaultFileName);

    public ProjectDocumentSession CreateEmptySession() => new(new ProjectDocument(), DefaultPath);

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

        ProjectDocument document = new();
        crypto.InitializeNew(document, password);

        return new ProjectDocumentSession(document, BuildNewProjectFilePath());
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
            ProjectDocument document = new();
            LoadEncryptedDocument(document, doc, password);

            interactions.ShowInfo($"{path} loaded", "Load file");
            return new ProjectDocumentSession(document, path);
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
        XmlElement root = crypto.GetAsEncryptedXml(session.Model).DocumentElement
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

    public void SaveUnencryptedSession(ProjectDocument document)
    {
        if (!interactions.TryGetSaveFilePath("Save unencrypted project file", out string path, "Save Files", "sav"))
            return;

        if (!interactions.TryGetPassword("Password", "Enter password:", out SecureString pass))
            return;

        try
        {
            XmlDocument doc = crypto.GetUnencryptedXml(document, pass);
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

                ProjectDocument document = new();
                crypto.SetMasterKey(document, password);
                document.LoadUnencrypted(doc);
                crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(document);
                document.IsFileLoaded = true;
                document.IsLocked = false;

                ProjectCryptoService.ClearArray(ref byteKey);

                return new ProjectDocumentSession(document, BuildNewProjectFilePath());
            }
        }
        catch (Exception ex)
        {
            interactions.ShowError($"Could not load {filePath}. Please check file. {ex.Message}", "Load unencrypted file");
            return null;
        }
    }

    private void LoadEncryptedDocument(ProjectDocument document, XmlDocument doc, SecureString password)
    {
        XmlElement root = doc.DocumentElement
            ?? throw new ArgumentException("XML document has no root element", nameof(doc));

        byte[] salt = XMLhelper.GetInnerTextFromNode(root.ChildNodes, "salt", false).FromBase64();
        crypto.SetMasterKey(document, password, salt);

        byte[] pepper = XMLhelper.GetInnerTextFromNode(root.ChildNodes, "pepper", false).FromBase64();
        byte[] innerSalt = XMLhelper.GetInnerTextFromNode(root.ChildNodes, "innersalt", false).FromBase64();

        XMLhelper.DecryptSimplified(
            doc,
            crypto.DeriveKey(document, "projects", innerSalt).ToBase64().ToSecureString(),
            pepper);

        List<XmlNode> projects = root.ChildNodes.FindAllNodesByName("project", true, true);
        document.LoadProjects(Project.FromXml(projects));

        XmlNode keyStore = XMLhelper.FindNodeByName(root.ChildNodes, "key_store", false);
        if (keyStore is not null)
            crypto.LoadKeyStore(document, keyStore);

        XmlNode certStore = XMLhelper.FindNodeByName(root.ChildNodes, "cert_store", false);
        if (certStore is not null)
            crypto.LoadCertStore(document, certStore);

        document.IsLocked = false;
        document.IsFileLoaded = true;
    }

    private static string BuildNewProjectFilePath()
    {
        string defaultPath = DefaultPath;
        return File.Exists(defaultPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), $"{DateTime.Now.Ticks}.xml")
            : defaultPath;
    }
}
