namespace TM.Services;

/// <summary>Dialog-free persistence. Callers serialize edits with load/save operations.</summary>
public sealed class ProjectStore(ProjectCryptoService crypto)
{
    private const long MaximumDocumentCharacters = 32 * 1024 * 1024;

    public ProjectDocument Create(SecureString password)
    {
        ProjectDocument document = new();
        try
        {
            crypto.InitializeNew(document, password);
            return document;
        }
        catch
        {
            Close(document);
            throw;
        }
    }

    public async Task<ProjectDocumentSession> OpenAsync(
        string path, SecureString password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(path);
        if (new FileInfo(fullPath).Length > MaximumDocumentCharacters * 4)
            throw new InvalidDataException("Project file is too large.");

        await using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        XmlReaderSettings settings = new()
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumDocumentCharacters
        };
        using XmlReader reader = XmlReader.Create(stream, settings);
        System.Xml.Linq.XDocument input = await System.Xml.Linq.XDocument.LoadAsync(
            reader, System.Xml.Linq.LoadOptions.None, cancellationToken);
        XmlDocument xml = new() { XmlResolver = null };
        using XmlReader parsed = input.CreateReader();
        xml.Load(parsed);
        if (xml.DocumentElement?.Name is not "project_store")
            throw new InvalidDataException("Not a TM project document.");

        ProjectDocument document = new();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            crypto.LoadEncryptedDocument(document, xml, password);
            cancellationToken.ThrowIfCancellationRequested();
            document.RefreshTodos();
            return new ProjectDocumentSession(document, fullPath);
        }
        catch
        {
            Close(document);
            throw;
        }
    }

    public async Task SaveAsync(ProjectDocumentSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!session.Model.IsFileLoaded || session.Model.IsLocked)
            throw new InvalidOperationException("Unlock a loaded document before saving.");

        string fullPath = Path.GetFullPath(session.FilePath);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("A document directory is required.", nameof(session));
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        XmlDocument xml = crypto.GetAsEncryptedXml(session.Model);
        try
        {
            await File.WriteAllTextAsync(temporaryPath, xml.OuterXml, Encoding.UTF8, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite: true);
            foreach (NodeModel node in session.Model.Nodes)
                node.ResetSave();
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public void Close(ProjectDocument document)
    {
        crypto.ClearAll(document);
        document.Security.CaCertificate?.Dispose();
        document.Security.CaCertificate = null;
        document.Nodes.Clear();
        document.RefreshTodos();
        document.IsLocked = true;
        document.IsFileLoaded = false;
    }
}
