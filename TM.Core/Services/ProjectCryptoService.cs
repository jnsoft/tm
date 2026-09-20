using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TM.Entities;

namespace TM.Services;

public sealed class ProjectCryptoService
{
    public const int Pbkdf2Iterations = 1000000;
    public const int SaltLength = 32;

    public void InitializeNew(ProjectDocument document, SecureString password)
    {
        SetMasterKey(document, password);
        document.IsLocked = false;
        document.IsFileLoaded = true;
    }

    public void Lock(ProjectDocument document)
    {
        ClearAll(document);
        document.IsLocked = true;
    }

    public void ClearAll(ProjectDocument document)
    {
        ClearMasterKey(document);
        ClearPrivateKey(document);
    }

    public void SetMasterKey(ProjectDocument document, SecureString password)
    {
        byte[] salt = SecurityHelper.GetRandomKey(SaltLength);
        byte[] key = SecurityHelper.GetKeyFromPassword(password, salt, SaltLength, Pbkdf2Iterations);
        SetMasterKey(document, salt, key);
        password.Dispose();
    }

    public void SetMasterKey(ProjectDocument document, SecureString password, byte[] salt)
    {
        SetMasterKey(
            document,
            salt,
            SecurityHelper.GetKeyFromPassword(password, salt, SaltLength, Pbkdf2Iterations));

        password.Dispose();
    }

    public void GenerateKeys(ProjectDocument document)
    {
        byte[]? privateKey = SecurityHelper.GeneratePKIPair(out byte[] publicKey);
        SetPrivateKey(document, ref privateKey);
        document.Security.PublicKey = publicKey.ToBase64();
    }

    public void SetMasterKey(ProjectDocument document, byte[] salt, byte[]? key)
    {
        document.Security.Entropy = SecurityHelper.GetRandomKey(SaltLength);
        document.Security.Salt = salt;

        if (key is null)
            throw new ArgumentNullException(nameof(key));

        document.Security.ProtectedMasterKey =
            ProtectedData.Protect(key, document.Security.Entropy, DataProtectionScope.CurrentUser);

        ClearArray(ref key);
    }

    public byte[] DeriveKey(ProjectDocument document, string context, byte[] salt, int length = 32)
    {
        byte[]? key = ProtectedData.Unprotect(
            document.Security.ProtectedMasterKey ?? throw new InvalidOperationException("Master key is not set"),
            document.Security.Entropy,
            DataProtectionScope.CurrentUser);

        byte[] derivedKey = SecurityHelper.DeriveSessionKey_HKDF(key, context.ToByte(), length, salt);
        ClearArray(ref key);
        return derivedKey;
    }

    public byte[] DeriveKey(byte[] masterKey, string context, byte[] salt) =>
        SecurityHelper.DeriveSessionKey_HKDF(masterKey, context.ToByte(), 32, salt);

    public string EncryptSecret(ProjectDocument document, string plain)
    {
        byte[] salt = SecurityHelper.GetRandomKey(SaltLength);
        byte[]? key = DeriveKey(document, "protected item", salt);
        byte[] encrypted = SecurityHelper.GCMEncrypt(plain.ToByte(), key);
        ClearArray(ref key);

        byte[] result = new byte[salt.Length + encrypted.Length];
        int outputOffset = 0;
        ArrayHelper.Append(result, salt, ref outputOffset);
        ArrayHelper.Append(result, encrypted, ref outputOffset);
        return result.ToBase64();
    }

    public string DecryptSecret(ProjectDocument document, string encrypted)
    {
        byte[] input = encrypted.FromBase64();
        int pos = 0;
        byte[] salt = ArrayHelper.Extract(input, SaltLength, ref pos);
        byte[] ciphertext = ArrayHelper.Extract(input, input.Length - salt.Length, ref pos);

        byte[]? key = DeriveKey(document, "protected item", salt);
        string plain = SecurityHelper.GCMDecrypt(ciphertext, key).ToStringFromByte();
        ClearArray(ref key);
        return plain;
    }

    public bool ChangeMasterPassword(ProjectDocument document, SecureString oldPassword, SecureString newPassword)
    {
        byte[] newSalt = SecurityHelper.GetRandomKey(SaltLength);
        byte[]? newKey = SecurityHelper.GetKeyFromPassword(newPassword, newSalt, 32, Pbkdf2Iterations);
        byte[]? oldKey = SecurityHelper.GetKeyFromPassword(oldPassword, document.Security.Salt, 32, Pbkdf2Iterations);

        try
        {
            List<Project> projects = document.GetProjects();
            if (projects.Count == 0)
                return false;

            if (projects.SelectMany(project => project.AllProtectedItems()).Any() is false)
                return false;

            foreach (Project project in projects)
            {
                foreach (ProtectedItem item in project.AllProtectedItems())
                    item.Password = ReencryptSecret(item.Password, oldKey, newKey);
            }

            SetMasterKey(document, newSalt, newKey);
            document.LoadProjects(projects);
            return true;
        }
        finally
        {
            oldPassword.Dispose();
            newPassword.Dispose();
            ClearArray(ref oldKey);
            ClearArray(ref newKey);
        }
    }

    public void EncryptProtectedItemsAfterLoadingUnencryptedProjects(ProjectDocument document)
    {
        List<Project> projects = document.GetProjects();

        foreach (Project project in projects)
        {
            foreach (ProtectedItem item in project.AllProtectedItems())
                item.Password = EncryptSecret(document, item.Password);
        }

        document.LoadProjects(projects);
    }

    public byte[]? GetUnprotectedPrivateKey(ProjectDocument document)
    {
        return document.Security.ProtectedPrivateKey is null
            ? null
            : ProtectedData.Unprotect(
                document.Security.ProtectedPrivateKey,
                document.Security.PrivateKeyEntropy,
                DataProtectionScope.CurrentUser);
    }

    public void EnsureCaCertificate(ProjectDocument document)
    {
        if (document.Security.CaCertificate is null)
            document.Security.CaCertificate = X509Helper.CreateCACert(
                $"CA TM: {Environment.UserName}@{Environment.MachineName}",
                null);
    }

    public byte[] SignFile(ProjectDocument document, string fileName)
    {
        byte[] dataToSign = File.ReadAllBytes(fileName);
        X509Certificate2 certificate =
            document.Security.CaCertificate ?? throw new InvalidOperationException("No certificate loaded");

        if (certificate.IsForDigitalSignature())
            return CMSHelper.Sign(dataToSign, certificate, X509IncludeOption.EndCertOnly);

        if (certificate.IsCA())
        {
            X509Certificate2 signingCert = X509Helper.CreateAndSignCertificate("TM.signing", certificate);
            return CMSHelper.Sign(dataToSign, signingCert, X509IncludeOption.EndCertOnly);
        }

        throw new InvalidOperationException("No valid certificate for signing found");
    }

    internal string ReencryptSecret(string encrypted, byte[] oldKey, byte[] newKey)
    {
        byte[] input = encrypted.FromBase64();
        int pos = 0;
        byte[] salt = ArrayHelper.Extract(input, SaltLength, ref pos);
        byte[] ciphertext = ArrayHelper.Extract(input, input.Length - salt.Length, ref pos);

        byte[]? key = DeriveKey(oldKey, "protected item", salt);
        string plain = SecurityHelper.GCMDecrypt(ciphertext, key).ToStringFromByte();
        ClearArray(ref key);

        salt = SecurityHelper.GetRandomKey(SaltLength);
        key = DeriveKey(newKey, "protected item", salt);
        byte[] encryptedValue = SecurityHelper.GCMEncrypt(plain.ToByte(), key);
        ClearArray(ref key);

        byte[] result = new byte[salt.Length + encryptedValue.Length];
        int outputOffset = 0;
        ArrayHelper.Append(result, salt, ref outputOffset);
        ArrayHelper.Append(result, encryptedValue, ref outputOffset);
        return result.ToBase64();
    }

    private void ClearMasterKey(ProjectDocument document)
    {
        if (document.Security.ProtectedMasterKey is null)
            return;

        Array.Clear(document.Security.ProtectedMasterKey, 0, document.Security.ProtectedMasterKey.Length);
        document.Security.ProtectedMasterKey = null;
    }

    private void ClearPrivateKey(ProjectDocument document)
    {
        if (document.Security.ProtectedPrivateKey is not null)
            Array.Clear(document.Security.ProtectedPrivateKey, 0, document.Security.ProtectedPrivateKey.Length);

        if (document.Security.PrivateKeyEntropy is not null)
            Array.Clear(document.Security.PrivateKeyEntropy, 0, document.Security.PrivateKeyEntropy.Length);

        document.Security.ProtectedPrivateKey = null;
        document.Security.PrivateKeyEntropy = null;
        document.Security.PublicKey = null;
    }

    public static void ClearArray(ref byte[]? value)
    {
        if (value is not null)
            Array.Clear(value, 0, value.Length);

        value = null;
    }

    public XmlDocument GetAsEncryptedXml(ProjectDocument document)
    {
        if (document.Security.ProtectedMasterKey is null)
            throw new ArgumentNullException(nameof(document), "No key set");

        XmlDocument doc = new();
        XmlElement projectStore = doc.CreateElement("project_store");

        XmlElement created = doc.CreateElement("created");
        created.InnerText = DateTimeHelper.GetDateTimeNowString();
        projectStore.AppendChild(created);

        XmlElement salt = doc.CreateElement("salt");
        salt.InnerText = (document.Security.Salt ?? throw new InvalidOperationException("Salt is not set")).ToBase64();
        projectStore.AppendChild(salt);

        byte[] innerSalt = SecureRandom.GetRandomBytes(SaltLength);
        XmlElement innerSaltNode = doc.CreateElement("innersalt");
        innerSaltNode.InnerText = innerSalt.ToBase64();
        projectStore.AppendChild(innerSaltNode);

        byte[] pepper = SecureRandom.GetRandomBytes(SaltLength);
        XmlElement pepperNode = doc.CreateElement("pepper");
        pepperNode.InnerText = pepper.ToBase64();
        projectStore.AppendChild(pepperNode);

        if (document.IsDiffieHellmanEnabled)
        {
            XmlNode keyStoreNode = doc.ImportNode(
                GetKeyStoreXml(document).DocumentElement ?? throw new InvalidOperationException("Key store XML has no root element."),
                true);
            projectStore.AppendChild(keyStoreNode);
        }

        if (document.IsPkiEnabled)
        {
            XmlNode certStoreNode = doc.ImportNode(
                GetCertStoreXml(document).DocumentElement ?? throw new InvalidOperationException("Cert store XML has no root element."),
                true);
            projectStore.AppendChild(certStoreNode);
        }

        List<Project> projects = document.GetProjects();
        XmlElement projectsNode = doc.CreateElement("projects");

        for (int i = 0; i < projects.Count; i++)
        {
            XmlNode projectNode = doc.ImportNode(
                projects[i].ToXml().DocumentElement ?? throw new InvalidOperationException($"Project XML at index {i} has no root element."),
                true);
            projectsNode.AppendChild(projectNode);
        }

        projectStore.AppendChild(projectsNode);
        doc.AppendChild(projectStore);

        XMLhelper.EncryptSimplified(
            doc,
            "projects",
            DeriveKey(document, "projects", innerSalt).ToBase64().ToSecureString(),
            pepper);

        return doc;
    }

    public XmlDocument GetUnencryptedXml(ProjectDocument document, SecureString password)
    {
        List<Project> plainProjects = document.GetProjects();
        byte[]? key = SecurityHelper.GetKeyFromPassword(
            password,
            document.Security.Salt ?? throw new InvalidOperationException("Salt is not set"),
            SaltLength,
            Pbkdf2Iterations);

        foreach (Project project in plainProjects)
        {
            foreach (ProtectedItem item in project.AllProtectedItems())
                item.Password = DecryptSecret(item.Password, ref key);
        }

        ClearArray(ref key);

        XmlDocument doc = new();
        XmlElement projectStore = doc.CreateElement("project_store");

        XmlElement created = doc.CreateElement("created");
        created.InnerText = DateTimeHelper.GetDateTimeNowString();
        projectStore.AppendChild(created);

        XmlElement projectsNode = doc.CreateElement("projects");
        for (int i = 0; i < plainProjects.Count; i++)
        {
            XmlNode projectNode = doc.ImportNode(
                plainProjects[i].ToXml().DocumentElement ?? throw new InvalidOperationException($"Project XML at index {i} has no root element."),
                true);
            projectsNode.AppendChild(projectNode);
        }

        projectStore.AppendChild(projectsNode);
        doc.AppendChild(projectStore);

        return doc;
    }

    public void LoadKeyStore(ProjectDocument document, XmlNode keyStore)
    {
        document.Security.PublicKey = XMLhelper.GetInnerTextFromChild(keyStore, "public_key");
        byte[] keyStoreSalt = XMLhelper.GetInnerTextFromChild(keyStore, "keystore_salt").FromBase64();
        byte[] encryptedKey = XMLhelper.GetInnerTextFromChild(keyStore, "private_key").FromBase64();

        byte[]? key = DeriveKey(document, "key_store", keyStoreSalt);
        byte[]? privateKey = SecurityHelper.GCMDecrypt(
            encryptedKey,
            key ?? throw new InvalidOperationException("Key is not set"),
            "key_store_ad".ToByte());

        ClearArray(ref key);
        SetPrivateKey(document, ref privateKey);
    }

    public void LoadCertStore(ProjectDocument document, XmlNode certStore)
    {
        string certId = XMLhelper.GetInnerTextFromChild(certStore, "cert_id");
        byte[] certStoreSalt = XMLhelper.GetInnerTextFromChild(certStore, "certstore_salt").FromBase64();
        byte[] encrypted = XMLhelper.ReadBinaryFromXmlNode(certStore.GetNode("CDATA", false));

        byte[]? key = DeriveKey(document, "CDATA", certStoreSalt);
        byte[]? pfx = SecurityHelper.GCMDecrypt(
            encrypted,
            key ?? throw new InvalidOperationException("Key is not set"),
            certId.ToByte());

        ClearArray(ref key);

        document.Security.CaCertificate = X509Helper.X509FromPfx(
            pfx ?? throw new InvalidOperationException("PFX is not set"),
            certId.ToSecureString());

        ClearArray(ref pfx);
    }

    public void LoadEncryptedDocument(ProjectDocument document, XmlDocument doc, SecureString password)
    {
        XmlElement root = doc.DocumentElement
            ?? throw new ArgumentException("XML document has no root element", nameof(doc));

        byte[] salt = XMLhelper.GetInnerTextFromNode(root.ChildNodes, "salt", false).FromBase64();
        SetMasterKey(document, password, salt);

        byte[] pepper = XMLhelper.GetInnerTextFromNode(root.ChildNodes, "pepper", false).FromBase64();
        byte[] innerSalt = XMLhelper.GetInnerTextFromNode(root.ChildNodes, "innersalt", false).FromBase64();

        XMLhelper.DecryptSimplified(
            doc,
            DeriveKey(document, "projects", innerSalt).ToBase64().ToSecureString(),
            pepper);

        List<XmlNode> projects = root.ChildNodes.FindAllNodesByName("project", true, true);
        document.LoadProjects(Project.FromXml(projects));

        XmlNode keyStore = XMLhelper.FindNodeByName(root.ChildNodes, "key_store", false);
        if (keyStore is not null)
            LoadKeyStore(document, keyStore);

        XmlNode certStore = XMLhelper.FindNodeByName(root.ChildNodes, "cert_store", false);
        if (certStore is not null)
            LoadCertStore(document, certStore);

        document.IsLocked = false;
        document.IsFileLoaded = true;
    }

    internal XmlDocument GetKeyStoreXml(ProjectDocument document)
    {
        if (!document.IsDiffieHellmanEnabled)
            throw new InvalidOperationException("No keys found");

        XmlDocument doc = new();
        XmlElement keyStore = doc.CreateElement("key_store");

        byte[] keyStoreSalt = SecureRandom.GetRandomBytes(SaltLength);
        XmlElement keyStoreSaltNode = doc.CreateElement("keystore_salt");
        keyStoreSaltNode.InnerText = keyStoreSalt.ToBase64();
        keyStore.AppendChild(keyStoreSaltNode);

        XmlElement publicKeyNode = doc.CreateElement("public_key");
        publicKeyNode.InnerText = document.Security.PublicKey ?? throw new InvalidOperationException("Public key is not set");
        keyStore.AppendChild(publicKeyNode);

        XmlElement privateKeyNode = doc.CreateElement("private_key");
        byte[] key = DeriveKey(document, "key_store", keyStoreSalt);
        privateKeyNode.InnerText = SecurityHelper.GCMEncrypt(
            GetUnprotectedPrivateKey(document) ?? throw new InvalidOperationException("Private key is not set"),
            key,
            "key_store_ad".ToByte()).ToBase64();
        keyStore.AppendChild(privateKeyNode);

        doc.AppendChild(keyStore);
        return doc;
    }

    internal XmlDocument GetCertStoreXml(ProjectDocument document)
    {
        if (!document.IsPkiEnabled)
            throw new InvalidOperationException("No certificate found");

        X509Certificate2 certificate = document.Security.CaCertificate
            ?? throw new InvalidOperationException("No CA certificate is loaded");

        XmlDocument doc = new();
        XmlElement certStore = doc.CreateElement("cert_store");

        XmlElement certId = doc.CreateElement("cert_id");
        certId.InnerText = certificate.GetSerialNumberString();
        certStore.AppendChild(certId);

        XmlElement expireDate = doc.CreateElement("expire_date");
        expireDate.InnerText = certificate.GetExpirationDateString();
        certStore.AppendChild(expireDate);

        byte[] certStoreSalt = SecureRandom.GetRandomBytes(SaltLength);
        XmlElement certStoreSaltNode = doc.CreateElement("certstore_salt");
        certStoreSaltNode.InnerText = certStoreSalt.ToBase64();
        certStore.AppendChild(certStoreSaltNode);

        byte[]? pfx = X509Helper.X509ToPfx(certificate, certificate.GetSerialNumberString().ToSecureString());
        byte[]? key = DeriveKey(document, "CDATA", certStoreSalt);
        byte[] encrypted = SecurityHelper.GCMEncrypt(
            pfx ?? throw new InvalidOperationException("PFX is not set"),
            key ?? throw new InvalidOperationException("Key is not set"),
            certId.InnerText.ToByte());

        ClearArray(ref key);
        ClearArray(ref pfx);

        XMLhelper.AddBinaryToXmlNode(certStore, encrypted, "CDATA");
        doc.AppendChild(certStore);

        return doc;
    }

    private string DecryptSecret(string encrypted, ref byte[]? key)
    {
        byte[] input = encrypted.FromBase64();

        int pos = 0;
        byte[] salt = ArrayHelper.Extract(input, SaltLength, ref pos);
        int encryptedLength = input.Length - salt.Length;
        byte[] ciphertext = ArrayHelper.Extract(input, encryptedLength, ref pos);

        byte[]? derivedKey = DeriveKey(
            key ?? throw new InvalidOperationException("Key is not set"),
            "protected item",
            salt);

        string plain = SecurityHelper.GCMDecrypt(ciphertext, derivedKey).ToStringFromByte();
        ClearArray(ref derivedKey);
        return plain;
    }

    private void SetPrivateKey(ProjectDocument document, ref byte[]? key)
    {
        if (document.Security.ProtectedPrivateKey is not null)
            Array.Clear(document.Security.ProtectedPrivateKey, 0, document.Security.ProtectedPrivateKey.Length);

        if (document.Security.PrivateKeyEntropy is not null)
            Array.Clear(document.Security.PrivateKeyEntropy, 0, document.Security.PrivateKeyEntropy.Length);

        document.Security.PrivateKeyEntropy = SecurityHelper.GetRandomKey(SaltLength);
        document.Security.ProtectedPrivateKey = ProtectedData.Protect(
            key ?? throw new ArgumentNullException(nameof(key)),
            document.Security.PrivateKeyEntropy,
            DataProtectionScope.CurrentUser);

        ClearArray(ref key);
    }
}
