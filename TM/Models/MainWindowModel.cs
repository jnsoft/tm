using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using TM.Entities;

namespace TM.Models;

public class MainWindowModel : INotifyPropertyChanged
{
    public const int PBKDF2_ITERATIONS = 1000000;
    public const int SALT_LEN = 32;

    private byte[] MasterKey = null;
    public byte[] Salt = null;
    private byte[] Entropy = null;

    public string PublicKey = null;
    private byte[] PrivateKey = null;
    private byte[] PrivateKeyEntropy = null;

    public X509Certificate2 CA_Certificate = null;

    private ObservableCollection<NodeModel> nodes;

    public ObservableCollection<NodeModel> Nodes
    {
        get { return this.nodes; }
        set
        {
            this.nodes = value;
            this.OnPropertyChanged();
        }
    }

    public ObservableCollection<ListItemModel> Todos
    {
        get
        {
            List<ListItemModel> listItems = new List<ListItemModel>();
            NodeModel[] nodes = Nodes.ToArray();
            foreach (NodeModel node in nodes)
                listItems.AddRange(node.AllChildNodesFlat.Where(x => x.IsLeaf).Select(x => new ListItemModel(x)));

            listItems = listItems.Where(x => x.DueDate.HasValue && x.Progress != 100).ToList();
            return new ObservableCollection<ListItemModel>(listItems.OrderBy(x => x.DueDate.Value).ThenByDescending(x => x.Priority));
        }
    }

#if DEBUG
    private byte[] DEBUG_KEY => ProtectedData.Unprotect(MasterKey, Entropy, DataProtectionScope.CurrentUser);

#endif

    public bool IsEmpty => Nodes.Count == 0;

    private bool isFileLoaded = false;

    public bool IsFileLoaded
    {
        get => isFileLoaded;
        set
        {
            isFileLoaded = value;
            OnPropertyChanged("IsFileLoaded");
        }
    }

    public bool IsDiffieHellmanEnabled => PrivateKey != null && !string.IsNullOrWhiteSpace(PublicKey);

    public bool IsPKIenabled => CA_Certificate != null;

    public bool IsLocked = true;

    // TODO move TreeViewModel content here

    public MainWindowModel()
    {
        nodes = new ObservableCollection<NodeModel>();
        IsLocked = false;
        isFileLoaded = false;
    }

    public MainWindowModel(SecureString Password) : this() => SetMasterKey(Password);

    public MainWindowModel(XmlDocument doc, SecureString Password)
    {
        byte[] salt = XMLhelper.GetInnerTextFromNode(doc.DocumentElement.ChildNodes, "salt", false).FromBase64();
        SetMasterKey(Password, salt);

        byte[] pepper = XMLhelper.GetInnerTextFromNode(doc.DocumentElement.ChildNodes, "pepper", false).FromBase64();
        byte[] innersalt = XMLhelper.GetInnerTextFromNode(doc.DocumentElement.ChildNodes, "innersalt", false).FromBase64();
        XMLhelper.DecryptSimplified(doc, DeriveKey("projects", innersalt).ToBase64().ToSecureString(), pepper);

        List<XmlNode> projects = doc.ChildNodes.FindAllNodesByName("project", true, true);
        setNodesFromProjects(Project.FromXml(projects));

        XmlNode key_store = XMLhelper.FindNodeByName(doc.DocumentElement.ChildNodes, "key_store", false);
        if (key_store != null)
            LoadKeyStore(key_store);

        XmlNode cert_store = XMLhelper.FindNodeByName(doc.DocumentElement.ChildNodes, "cert_store", false);
        if (cert_store != null)
            LoadCertStore(cert_store);

        IsLocked = false;
        IsFileLoaded = true;
        //OnPropertyChanged("IsLocked");
    }

    public void ClearAll()
    {
        ClearMasterKey();
        ClearPrivateKey();
    }

    public void LoadProjects(List<Project> projects)
    {
        setNodesFromProjects(projects);

        IsLocked = false;
        IsFileLoaded = true;
        //OnPropertyChanged("IsFileLoaded");
        //OnPropertyChanged("IsLocked");
    }


    #region Working with nodes

    public NodeModel GetNodeById(string id)
    {
        foreach (NodeModel node in Nodes) // for each project
        {
            foreach (NodeModel n in node.AllChildNodesFlat) // inclusive parent node
            {
                if (n.Id == id)
                    return n;
            }
        }
        return null;
    }

    public void DeleteNode(NodeModel node)
    {
        foreach (NodeModel n in Nodes)
        {
            if (n.Id == node.Id)
            {
                nodes.Remove(node);
                break;
            }

            else if (!n.IsLeaf)
                n.DeleteNode(node);
        }
        OnPropertyChanged("Nodes");
    }

    public void AddNode(NodeModel node)
    {
        nodes.Add(node);
        OnPropertyChanged("Nodes");
    }

    public void FilterNodes(string filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filter = filter.ToUpper(); // filter must be uppercase!
            foreach (NodeModel n in Nodes)
                n.Filter(filter);
        }
        else
        {
            foreach (NodeModel n in Nodes)
                n.Visualize();
        }
    }

    public void SortNodes(bool bydate = false)
    {
        if (this.Nodes.Count < 1)
            return;

        foreach (NodeModel n in Nodes)
            n.SortNodes(bydate);

        if (bydate)
            this.Nodes = new ObservableCollection<NodeModel>(this.Nodes.OrderBy(n => n.NodeType).ThenBy(n => n.Created));
        else
            this.Nodes = new ObservableCollection<NodeModel>(this.Nodes.OrderBy(n => n.NodeType).ThenBy(n => n.Text));

        OnPropertyChanged("Nodes");
    }

    public void ExpandNodes() => expandNodes(nodes);

    private void expandNodes(ObservableCollection<NodeModel> nodes)
    {
        foreach (NodeModel node in nodes)
        {
            node.IsExpanded = true;
            expandNodes(node.Nodes);
        }
    }

    public void CollapseNodes() => collapseNodes(nodes);

    private void collapseNodes(ObservableCollection<NodeModel> nodes)
    {
        foreach (NodeModel node in nodes)
        {
            collapseNodes(node.Nodes);
            node.IsExpanded = false;
        }
    }

    // expand all parent nodes
    internal void FocusNode(NodeModel n)
    {
        collapseNodes(Nodes);
        n.ExpandParents();
        n.IsSelected = true;
    }

    #endregion

    #region Working with MasterKey

    public void SetMasterKey(SecureString Password)
    {
        byte[] salt = SecurityHelper.GetRandomKey(SALT_LEN);
        byte[] k = SecurityHelper.GetKeyFromPassword(Password, salt, SALT_LEN, PBKDF2_ITERATIONS);
        SetMasterKey(salt, k);

        Password.Dispose();
    }

    public void SetMasterKey(SecureString Password, byte[] salt)
    {
        SetMasterKey(salt, SecurityHelper.GetKeyFromPassword(Password, salt, SALT_LEN, PBKDF2_ITERATIONS));
        Password.Dispose();
    }

    public void SetMasterKey(byte[] salt, byte[] key)
    {
        Entropy = SecurityHelper.GetRandomKey(SALT_LEN);
        Salt = salt;
        MasterKey = ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser);
        ClearArr(ref key);
    }

    public void ClearMasterKey()
    {
        if (MasterKey != null)
        {
            ClearArr(ref MasterKey);
            MasterKey = null;
        }
    }

    public bool ChangeMasterPassword(SecureString OldPassword, SecureString NewPassword)
    {
        byte[] newSalt = SecurityHelper.GetRandomKey(SALT_LEN);
        byte[] newKey = SecurityHelper.GetKeyFromPassword(NewPassword, newSalt, 32, PBKDF2_ITERATIONS);
        byte[] oldKey = SecurityHelper.GetKeyFromPassword(OldPassword, Salt, 32, PBKDF2_ITERATIONS);

        try
        {
            List<Project> projects = GetProjects();

            if (projects.Count == 0)
                return false;

            if (projects.Select(p => p.AllProtectedItems().Count()).Sum() == 0)
                return false;

            foreach (Project p in projects)
            {
                foreach (ProtectedItem i in p.AllProtectedItems())
                    i.Password = ReencryptSecret(i.Password, oldKey, newKey);
            }

            SetMasterKey(newSalt, newKey);
            setNodesFromProjects(projects);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            OldPassword.Dispose();
            NewPassword.Dispose();
            ClearArr(ref oldKey);
        }

    }

    public byte[] DeriveKey(string context, byte[] salt, int length = 32)
    {
        byte[] key = ProtectedData.Unprotect(MasterKey, Entropy, DataProtectionScope.CurrentUser);
        byte[] derived_key = SecurityHelper.DeriveSessionKey_HKDF(key, context.ToByte(), length, salt);
        ClearArr(ref key);
        return derived_key;
    }

    public byte[] DeriveKey(byte[] masterkey, string context, byte[] salt) => SecurityHelper.DeriveSessionKey_HKDF(masterkey, context.ToByte(), 32, salt);

    public void Lock()
    {
        ClearAll();
        IsLocked = true;
    }

    #endregion

    #region Protected items

    public string EncryptSecret(string plain)
    {
        byte[] salt = SecurityHelper.GetRandomKey(SALT_LEN);
        byte[] key = DeriveKey("protected item", salt);
        byte[] encrypted = SecurityHelper.GCMEncrypt(plain.ToByte(), key);
        ClearArr(ref key);
        byte[] result = new byte[salt.Length + encrypted.Length];
        int outputOffset = 0;
        ArrayHelper.Append(result, salt, ref outputOffset);
        ArrayHelper.Append(result, encrypted, ref outputOffset);
        return result.ToBase64();
    }

    public string DecryptSecret(string encrypted)
    {
        byte[] input = encrypted.FromBase64();

        int pos = 0;
        byte[] salt = ArrayHelper.Extract(input, SALT_LEN, ref pos);
        int enc_len = input.Length - salt.Length;
        byte[] ciphertext = ArrayHelper.Extract(input, enc_len, ref pos);

        byte[] key = DeriveKey("protected item", salt);
        string plain = SecurityHelper.GCMDecrypt(ciphertext, key).ToStringFromByte();

        ClearArr(ref key);
        return plain;
    }

    public string DecryptSecret(string encrypted, ref byte[] key)
    {
        byte[] input = encrypted.FromBase64();

        int pos = 0;
        byte[] salt = ArrayHelper.Extract(input, SALT_LEN, ref pos);
        int enc_len = input.Length - salt.Length;
        byte[] ciphertext = ArrayHelper.Extract(input, enc_len, ref pos);

        byte[] key2 = DeriveKey(key, "protected item", salt);

        string plain = SecurityHelper.GCMDecrypt(ciphertext, key2).ToStringFromByte();

        ClearArr(ref key2);
        return plain;
    }

    public string DecryptSecret(string encrypted, ref SecureString pass)
    {
        byte[] input = encrypted.FromBase64();

        int pos = 0;
        byte[] salt = ArrayHelper.Extract(input, SALT_LEN, ref pos);
        int enc_len = input.Length - salt.Length;
        byte[] ciphertext = ArrayHelper.Extract(input, enc_len, ref pos);


        byte[] key = DeriveKey("protected item", salt);

        string plain = SecurityHelper.GCMDecrypt(ciphertext, key).ToStringFromByte();

        ClearArr(ref key);
        return plain;
    }

    public string ReencryptSecret(string secret, byte[] oldkey, byte[] newkey)
    {
        byte[] input = secret.FromBase64();

        int pos = 0;
        byte[] salt = ArrayHelper.Extract(input, SALT_LEN, ref pos);
        int enc_len = input.Length - salt.Length;
        byte[] ciphertext = ArrayHelper.Extract(input, enc_len, ref pos);
        byte[] key = DeriveKey(oldkey, "protected item", salt);
        string plain = SecurityHelper.GCMDecrypt(ciphertext, key).ToStringFromByte();
        ClearArr(ref key);

        salt = SecurityHelper.GetRandomKey(SALT_LEN);
        key = DeriveKey(newkey, "protected item", salt);
        byte[] encrypted = SecurityHelper.GCMEncrypt(plain.ToByte(), key);
        ClearArr(ref key);

        byte[] result = new byte[salt.Length + encrypted.Length];
        int outputOffset = 0;
        ArrayHelper.Append(result, salt, ref outputOffset);
        ArrayHelper.Append(result, encrypted, ref outputOffset);
        return result.ToBase64();
    }

    public void EncryptProtectedItemsAfterLoadingUnencryptedProjects()
    {
        List<Project> ps = GetProjects();

        foreach (Project p in ps)
        {
            foreach (ProtectedItem i in p.AllProtectedItems())
                i.Password = EncryptSecret(i.Password);
        }

        setNodesFromProjects(ps);
    }

    #endregion

    #region DH

    //private void EnsurePKIpair()
    //{
    //    if (PrivateKey == null)
    //        GenerateNewPKIpair();
    //}

    public void GenerateNewPKIpair()
    {
        byte[] key = SecurityHelper.GeneratePKIPair(out byte[] pub);
        SetPrivateKey(ref key);
        PublicKey = pub.ToBase64();
    }

    public void ClearPrivateKey()
    {
        if (PrivateKey != null)
        {
            ClearArr(ref PrivateKey);
            PrivateKey = null;
        }
    }

    public void SetPrivateKey(ref byte[] key)
    {
        PrivateKeyEntropy = SecurityHelper.GetRandomKey(SALT_LEN);
        PrivateKey = ProtectedData.Protect(key, PrivateKeyEntropy, DataProtectionScope.CurrentUser);
        ClearArr(ref key);
    }

    public byte[] GetUnprotectedPrivateKey() => ProtectedData.Unprotect(PrivateKey, PrivateKeyEntropy, DataProtectionScope.CurrentUser);

    #endregion

    #region PKI

    public void EnsureCAcert()
    {
        if (CA_Certificate == null)
            CreateOrReplaceCAcert();
    }

    private void CreateOrReplaceCAcert() => CA_Certificate = X509Helper.CreateCACert($"CA TM: {Environment.UserName}@{Environment.MachineName}", null);

    public byte[] SignFile(string fname)
    {
        byte[] dataToSign = File.ReadAllBytes(fname);

        if (CA_Certificate.IsForDigitalSignature())
            return CMSHelper.Sign(dataToSign, CA_Certificate, X509IncludeOption.EndCertOnly);

        else if (CA_Certificate.IsCA())
        {
            X509Certificate2 signingCert = X509Helper.CreateAndSignCertificate("TM.signing", CA_Certificate);
            return CMSHelper.Sign(dataToSign, signingCert, X509IncludeOption.EndCertOnly);
        }

        else
            throw new Exception("No valid certificate for signing found");
    }

    #endregion

    public List<Project> GetProjects()
    {
        List<Project> result = new List<Project>();

        foreach (NodeModel node in Nodes)
        {
            if (node.NodeType == ProjectItemType.Project)
                result.Add(new Project(node));
        }

        return result;
    }

    #region ToXml

    public XmlDocument GetKeyStoreXML()
    {
        if (!IsDiffieHellmanEnabled)
            throw new Exception("GetKeyStoreXML: No keys found");

        XmlDocument doc = new XmlDocument();

        XmlElement key_store = doc.CreateElement("key_store");

        byte[] keystoreSalt = SecureRandom.GetRandomBytes(32);
        XmlElement keystore_salt = doc.CreateElement("keystore_salt");
        keystore_salt.InnerText = keystoreSalt.ToBase64();
        key_store.AppendChild(keystore_salt);

        XmlElement public_key = doc.CreateElement("public_key");
        public_key.InnerText = PublicKey;
        key_store.AppendChild(public_key);

        XmlElement private_key = doc.CreateElement("private_key");

        byte[] key = DeriveKey("key_store", keystoreSalt);
        private_key.InnerText = SecurityHelper.GCMEncrypt(GetUnprotectedPrivateKey(), key, "key_store_ad".ToByte()).ToBase64();
        key_store.AppendChild(private_key);

        doc.AppendChild(key_store);

        return doc;
    }

    public XmlDocument GetCertStoreXML()
    {
        if (!IsPKIenabled)
            throw new Exception("GetPfxXML: No certificate found");

        XmlDocument doc = new XmlDocument();

        XmlElement cert_store = doc.CreateElement("cert_store");

        XmlElement cert_id = doc.CreateElement("cert_id");
        cert_id.InnerText = CA_Certificate.GetSerialNumberString();
        cert_store.AppendChild(cert_id);

        XmlElement expire_date = doc.CreateElement("expire_date");
        expire_date.InnerText = CA_Certificate.GetExpirationDateString();
        cert_store.AppendChild(expire_date);

        byte[] certstoreSalt = SecureRandom.GetRandomBytes(32);
        XmlElement certstore_salt = doc.CreateElement("certstore_salt");
        certstore_salt.InnerText = certstoreSalt.ToBase64();
        cert_store.AppendChild(certstore_salt);

        byte[] pfx = X509Helper.X509ToPfx(CA_Certificate, CA_Certificate.GetSerialNumberString().ToSecureString());
        byte[] key = DeriveKey("CDATA", certstoreSalt);
        byte[] enc = SecurityHelper.GCMEncrypt(pfx, key, cert_id.InnerText.ToByte());
        ClearArr(ref key);
        ClearArr(ref pfx);

        XMLhelper.AddBinaryToXmlNode(cert_store, enc, "CDATA");

        doc.AppendChild(cert_store);

        return doc;
    }

    public XmlDocument GetAsEncryptedXML()
    {
        if (MasterKey == null)
            throw new ArgumentNullException("No key set");

        XmlDocument doc = new XmlDocument();
        XmlElement project_store = doc.CreateElement("project_store");

        XmlElement created = doc.CreateElement("created");
        created.InnerText = DateTimeHelper.GetDateTimeNowString();
        project_store.AppendChild(created);

        XmlElement salt = doc.CreateElement("salt");
        salt.InnerText = Salt.ToBase64();
        project_store.AppendChild(salt);

        byte[] Innersalt = SecureRandom.GetRandomBytes(32);
        XmlElement innersalt = doc.CreateElement("innersalt");
        innersalt.InnerText = Innersalt.ToBase64();
        project_store.AppendChild(innersalt);

        byte[] Pepper = SecureRandom.GetRandomBytes(32);
        XmlElement pepper = doc.CreateElement("pepper");
        pepper.InnerText = Pepper.ToBase64();
        project_store.AppendChild(pepper);

        if (IsDiffieHellmanEnabled)
            project_store.AppendChild(doc.ImportNode(GetKeyStoreXML().DocumentElement, true));

        if (IsPKIenabled)
            project_store.AppendChild(doc.ImportNode(GetCertStoreXML().DocumentElement, true));

        List<Project> ps = GetProjects();
        XmlElement projects = doc.CreateElement("projects");
        for (int i = 0; i < ps.Count; i++)
        {
            XmlNode project = doc.ImportNode(ps[i].ToXml().DocumentElement, true);
            projects.AppendChild(project);
        }

        project_store.AppendChild(projects);

        doc.AppendChild(project_store);

        doc.EncryptSimplified("projects", DeriveKey("projects", Innersalt).ToBase64().ToSecureString(), Pepper);

        return doc;
    }

    public XmlDocument GetUnencryptedXML(SecureString Password)
    {
        List<Project> plainprojects = GetProjects();
        byte[] key = SecurityHelper.GetKeyFromPassword(Password, Salt, SALT_LEN, PBKDF2_ITERATIONS); // enforce user to know the password

        foreach (Project p in plainprojects)
        {
            foreach (ProtectedItem i in p.AllProtectedItems())
                i.Password = DecryptSecret(i.Password, ref key);
        }

        ClearArr(ref key);

        XmlDocument doc = new XmlDocument();
        XmlElement project_store = doc.CreateElement("project_store");

        XmlElement created = doc.CreateElement("created");
        created.InnerText = DateTimeHelper.GetDateTimeNowString();
        project_store.AppendChild(created);

        XmlElement projects = doc.CreateElement("projects");
        for (int i = 0; i < plainprojects.Count; i++)
        {
            XmlNode project = doc.ImportNode(plainprojects[i].ToXml().DocumentElement, true);
            projects.AppendChild(project);
        }

        project_store.AppendChild(projects);

        doc.AppendChild(project_store);

        return doc;
    }

    #endregion

    #region FromXml

    public void LoadKeyStore(XmlNode key_store)
    {
        PublicKey = XMLhelper.GetInnerTextFromChild(key_store, "public_key");
        byte[] keystore_salt = XMLhelper.GetInnerTextFromChild(key_store, "keystore_salt").FromBase64();
        byte[] encrypted_key = XMLhelper.GetInnerTextFromChild(key_store, "private_key").FromBase64();
        byte[] key = DeriveKey("key_store", keystore_salt);
        byte[] private_key = SecurityHelper.GCMDecrypt(encrypted_key, key, "key_store_ad".ToByte());
        ClearArr(ref key);
        SetPrivateKey(ref private_key);
    }

    public void LoadCertStore(XmlNode cert_store)
    {
        string cert_id = XMLhelper.GetInnerTextFromChild(cert_store, "cert_id");
        byte[] certstore_salt = XMLhelper.GetInnerTextFromChild(cert_store, "certstore_salt").FromBase64();
        byte[] enc = XMLhelper.ReadBinaryFromXmlNode(cert_store.GetNode("CDATA", false));

        byte[] key = DeriveKey("CDATA", certstore_salt);
        byte[] pfx = SecurityHelper.GCMDecrypt(enc, key, cert_id.ToByte());
        ClearArr(ref key);

        CA_Certificate = X509Helper.X509FromPfx(pfx, XMLhelper.GetInnerTextFromChild(cert_store, "cert_id").ToSecureString());
    }

    public void LoadUnencrypted(XmlDocument unencryptedXml)
    {
        List<XmlNode> projects = unencryptedXml.ChildNodes.FindAllNodesByName("project", true, true);
        setNodesFromProjects(Project.FromXml(projects));
    }


    #endregion

    // public void ClearFilterFlag() => OnPropertyChanged("IsFiltered"); // needed?

    public void FireTodos() => OnPropertyChanged("Todos");

    #region INotify

    public event PropertyChangedEventHandler PropertyChanged = delegate { };
    public void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        // Raise the PropertyChanged event, passing the name of the property whose value has changed.
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Old implementaion:

    //public event PropertyChangedEventHandler PropertyChanged;

    //protected void OnPropertyChanged(string propertyName)
    //{
    //    if (PropertyChanged != null)
    //        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    //}

    #endregion

    #region Private helpers

    private void setNodesFromProjects(List<Project> projects)
    {
        List<NodeModel> ps = new List<NodeModel>();
        foreach (Project p in projects.OrderBy(x => x.Name))
            ps.Add(new NodeModel(p));

        Nodes = new ObservableCollection<NodeModel>(ps);
    }

    // private void Refresh() => throw new NotImplementedException();



    #endregion

    #region Public Static Helpers

    public static void ClearArr(ref byte[] arr)
    {
        Array.Clear(arr, 0, arr.Length);
        arr = null;
    }

    #endregion



}
