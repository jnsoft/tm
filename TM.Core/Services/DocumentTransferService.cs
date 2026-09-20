using System.Security;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using TM.Models;

namespace TM.Services;

/// <summary>Legacy-compatible encrypted project transfer without dialogs or retained transfer keys.</summary>
public sealed class DocumentTransferService(ProjectCryptoService crypto)
{
    public const long MaximumTransferBytes = 48L * 1024 * 1024;
    public const long MaximumXmlCharacters = 32L * 1024 * 1024;
    public const int TransferKeyLength = 32;

    public async Task<byte[]> ExportAsync(ProjectDocument document, SecureString documentPassword, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(documentPassword);
        cancellationToken.ThrowIfCancellationRequested();
        if (!crypto.VerifyMasterPassword(document, documentPassword))
            throw new CryptographicException("The document password is incorrect.");

        string destination = Path.GetFullPath(destinationPath);
        if (File.Exists(destination)) throw new IOException("Choose a new output file.");
        byte[]? key = null;
        byte[]? payload = null;
        string staging = Path.Combine(Path.GetDirectoryName(destination)!, $".tm-transfer-{Guid.NewGuid():N}");
        string temporary = Path.Combine(staging, "payload");
        bool created = false;
        try
        {
            XmlDocument xml = crypto.GetUnencryptedXml(document, documentPassword);
            string content = xml.InnerXml;
            if (content.Length > MaximumXmlCharacters) throw new InvalidDataException("The document exceeds the transfer limit.");
            key = SecurityHelper.GetRandomKey(TransferKeyLength);
            payload = SecurityHelper.GCMEncrypt(content.ToByte(), key);
            string encoded = payload.ToBase64();
            if (encoded.Length > MaximumTransferBytes) throw new InvalidDataException("The document exceeds the transfer limit.");

            Directory.CreateDirectory(staging);
            created = true;
            await File.WriteAllTextAsync(temporary, encoded, Encoding.UTF8, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
            byte[] result = key;
            key = null;
            return result;
        }
        finally
        {
            ProjectCryptoService.ClearArray(ref key);
            ProjectCryptoService.ClearArray(ref payload);
            if (created)
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                Directory.Delete(staging);
            }
        }
    }

    public async Task<ProjectDocument> ImportAsync(string sourcePath, SecureString newDocumentPassword, ReadOnlyMemory<byte> transferKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newDocumentPassword);
        cancellationToken.ThrowIfCancellationRequested();
        if (transferKey.Length != TransferKeyLength) throw new CryptographicException("The transfer key is invalid.");
        string source = Path.GetFullPath(sourcePath);
        if (new FileInfo(source).Length > MaximumTransferBytes) throw new InvalidDataException("The selected transfer file exceeds the supported limit.");

        byte[]? encrypted = null;
        byte[]? plain = null;
        byte[]? key = null;
        try
        {
            string encoded = await File.ReadAllTextAsync(source, cancellationToken);
            if (encoded.Length > MaximumTransferBytes) throw new InvalidDataException("The selected transfer file exceeds the supported limit.");
            encrypted = encoded.FromBase64();
            key = transferKey.ToArray();
            plain = SecurityHelper.GCMDecrypt(encrypted, key);
            if (plain.Length > MaximumXmlCharacters * sizeof(char)) throw new InvalidDataException("The transfer payload exceeds the supported limit.");

            XmlDocument xml = LoadTransferXml(plain.ToStringFromByte(), cancellationToken);
            ProjectDocument document = new();
            try
            {
                crypto.InitializeNew(document, newDocumentPassword);
                document.LoadUnencrypted(xml);
                crypto.EncryptProtectedItemsAfterLoadingUnencryptedProjects(document);
                document.RefreshTodos();
                return document;
            }
            catch
            {
                crypto.ClearAll(document);
                throw;
            }
        }
        finally
        {
            ProjectCryptoService.ClearArray(ref encrypted);
            ProjectCryptoService.ClearArray(ref plain);
            ProjectCryptoService.ClearArray(ref key);
        }
    }

    private static XmlDocument LoadTransferXml(string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumXmlCharacters
        };
        using StringReader input = new(value);
        using XmlReader reader = XmlReader.Create(input, settings);
        XDocument parsed = XDocument.Load(reader, LoadOptions.None);
        XmlDocument xml = new() { XmlResolver = null };
        using XmlReader safeReader = parsed.CreateReader();
        xml.Load(safeReader);
        if (xml.DocumentElement?.Name is not "project_store" || xml.DocumentElement.SelectSingleNode("projects") is not XmlElement)
            throw new InvalidDataException("Not a TM transfer document.");
        return xml;
    }
}
