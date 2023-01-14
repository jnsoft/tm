namespace TM.Helpers;

public static class HashHelper
{
    const int BUFFER_SIZE = 1024;
    public const int SALT_LEN = 32;

    #region Hashes
    public static string MD5File(string path)
    {
        using (MD5 md5 = MD5.Create())
        {
            FileInfo f = new FileInfo(path);
            using (FileStream fileStream = f.Open(FileMode.Open))
            {
                try
                {
                    fileStream.Position = 0;
                    byte[] hashValue = md5.ComputeHash(fileStream);
                    string hash = hashValue.PrettyPrintHash();

                    string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".md5";
                    int i = 1;
                    while (File.Exists(newFilename))
                        newFilename += "." + (i++).ToString();

                    File.WriteAllText(newFilename, hash, Encoding.UTF8);
                    return newFilename;
                }
                catch (IOException e)
                {
                    //Console.WriteLine($"I/O Exception: {e.Message}");
                }
                catch (UnauthorizedAccessException e)
                {
                    //Console.WriteLine($"Access Exception: {e.Message}");
                }
                return null;
            }
        }

    }

    public static string Sha1File(string path)
    {
        using (SHA1 sha = SHA1.Create())
        {
            FileInfo f = new FileInfo(path);
            using (FileStream fileStream = f.Open(FileMode.Open))
            {

                fileStream.Position = 0;
                byte[] hashValue = sha.ComputeHash(fileStream);
                string hash = hashValue.PrettyPrintHash();

                string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".sha1";
                int i = 1;
                while (File.Exists(newFilename))
                    newFilename += "." + (i++).ToString();

                File.WriteAllText(newFilename, hash, Encoding.UTF8);
                return newFilename;
            }
        }

    }

    public static string Sha256File(string path)
    {
        using (SHA256 sha = SHA256.Create())
        {
            FileInfo f = new FileInfo(path);
            using (FileStream fileStream = f.Open(FileMode.Open))
            {
                fileStream.Position = 0;
                byte[] hashValue = sha.ComputeHash(fileStream);
                string hash = hashValue.PrettyPrintHash();

                string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".sha" + "256";
                int i = 1;
                while (File.Exists(newFilename))
                    newFilename += "." + (i++).ToString();

                File.WriteAllText(newFilename, hash, Encoding.UTF8);
                return newFilename;
            }
        }
    }

    public static string Sha384File(string path)
    {
        using (SHA384 sha = SHA384.Create())
        {
            FileInfo f = new FileInfo(path);
            using (FileStream fileStream = f.Open(FileMode.Open))
            {
                fileStream.Position = 0;
                byte[] hashValue = sha.ComputeHash(fileStream);
                string hash = hashValue.PrettyPrintHash();

                string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".sha" + "384";
                int i = 1;
                while (File.Exists(newFilename))
                    newFilename += "." + (i++).ToString();

                File.WriteAllText(newFilename, hash, Encoding.UTF8);
                return newFilename;
            }
        }
    }

    public static string Sha512File(string path)
    {
        using (SHA512 sha = SHA512.Create())
        {
            FileInfo f = new FileInfo(path);
            using (FileStream fileStream = f.Open(FileMode.Open))
            {
                fileStream.Position = 0;
                byte[] hashValue = sha.ComputeHash(fileStream);
                string hash = hashValue.PrettyPrintHash();

                string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".sha" + "512";
                int i = 1;
                while (File.Exists(newFilename))
                    newFilename += "." + (i++).ToString();

                File.WriteAllText(newFilename, hash, Encoding.UTF8);
                return newFilename;
            }
        }
    }

    #endregion

    public static string MD5SignFile(byte[] key, string path)
    {
        using (HMACMD5 hmac = new HMACMD5(key))
        {
            using (FileStream inStream = new FileStream(path, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] mac = hmac.ComputeHash(inStream);
                string signature = mac.PrettyPrintHash();

                string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".HMAC_" + "MD5";
                int i = 1;
                while (File.Exists(newFilename))
                    newFilename += "." + (i++).ToString();

                File.WriteAllText(newFilename, signature, Encoding.UTF8);
                return newFilename;
            }
        }
    }

    public static string Sha1SignFile(byte[] key, string path)
    {
        using (HMACSHA1 hmac = new HMACSHA1(key))
        {
            using (FileStream inStream = new FileStream(path, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] mac = hmac.ComputeHash(inStream);
                string signature = mac.PrettyPrintHash();

                string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".HMAC_" + "SHA1";
                int i = 1;
                while (File.Exists(newFilename))
                    newFilename += "." + (i++).ToString();

                File.WriteAllText(newFilename, signature, Encoding.UTF8);
                return newFilename;
            }
        }
    }

    public static string Sha256SignFile(byte[] key, string path)
    {
        using (HMACSHA256 hmac = new HMACSHA256(key))
        {
            using (FileStream inStream = new FileStream(path, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] mac = hmac.ComputeHash(inStream);
                string signature = mac.PrettyPrintHash();

                string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".HMAC_" + "256";
                int i = 1;
                while (File.Exists(newFilename))
                    newFilename += "." + (i++).ToString();

                File.WriteAllText(newFilename, signature, Encoding.UTF8);
                return newFilename;
            }
        }
    }

    public static string Sha384SignFile(byte[] key, string path)
    {
        using (HMACSHA384 hmac = new HMACSHA384(key))
        {
            using (FileStream inStream = new FileStream(path, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] mac = hmac.ComputeHash(inStream);
                string signature = mac.PrettyPrintHash();

                string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".HMAC_" + "384";
                int i = 1;
                while (File.Exists(newFilename))
                    newFilename += "." + (i++).ToString();

                File.WriteAllText(newFilename, signature, Encoding.UTF8);
                return newFilename;
            }
        }
    }

    public static string Sha512SignFile(byte[] key, string path)
    {
        using (HMACSHA512 hmac = new HMACSHA512(key))
        {
            using (FileStream inStream = new FileStream(path, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] mac = hmac.ComputeHash(inStream);
                string signature = mac.PrettyPrintHash();

                string newFilename = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".HMAC_" + "512";
                int i = 1;
                while (File.Exists(newFilename))
                    newFilename += "." + (i++).ToString();

                File.WriteAllText(newFilename, signature, Encoding.UTF8);
                return newFilename;
            }
        }
    }

    public static bool MD5VerifyFile(byte[] key, string sourceFile, byte[] Stored_HMAC)
    {
        using (HMACMD5 hmac = new HMACMD5(key))
        {
            using (FileStream inStream = new FileStream(sourceFile, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] computedHash = hmac.ComputeHash(inStream);

                if (computedHash.Length != Stored_HMAC.Length)
                    return false;

                for (int i = 0; i < Stored_HMAC.Length; i++)
                {
                    if (computedHash[i] != Stored_HMAC[i])
                        return false;
                }
            }
        }
        return true;
    }

    public static bool Sha1VerifyFile(byte[] key, string sourceFile, byte[] Stored_HMAC)
    {
        using (HMACSHA1 hmac = new HMACSHA1(key))
        {
            using (FileStream inStream = new FileStream(sourceFile, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] computedHash = hmac.ComputeHash(inStream);

                if (computedHash.Length != Stored_HMAC.Length)
                    return false;

                for (int i = 0; i < Stored_HMAC.Length; i++)
                {
                    if (computedHash[i] != Stored_HMAC[i])
                        return false;
                }
            }
        }
        return true;
    }

    public static bool Sha256VerifyFile(byte[] key, string sourceFile, byte[] Stored_HMAC)
    {
        using (HMACSHA256 hmac = new HMACSHA256(key))
        {
            using (FileStream inStream = new FileStream(sourceFile, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] computedHash = hmac.ComputeHash(inStream);

                if (computedHash.Length != Stored_HMAC.Length)
                    return false;

                for (int i = 0; i < Stored_HMAC.Length; i++)
                {
                    if (computedHash[i] != Stored_HMAC[i])
                        return false;
                }
            }
        }
        return true;
    }

    public static bool Sha384VerifyFile(byte[] key, string sourceFile, byte[] Stored_HMAC)
    {
        using (HMACSHA384 hmac = new HMACSHA384(key))
        {
            using (FileStream inStream = new FileStream(sourceFile, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] computedHash = hmac.ComputeHash(inStream);

                if (computedHash.Length != Stored_HMAC.Length)
                    return false;

                for (int i = 0; i < Stored_HMAC.Length; i++)
                {
                    if (computedHash[i] != Stored_HMAC[i])
                        return false;
                }
            }
        }
        return true;
    }

    public static bool Sha512VerifyFile(byte[] key, string sourceFile, byte[] Stored_HMAC)
    {
        using (HMACSHA512 hmac = new HMACSHA512(key))
        {
            using (FileStream inStream = new FileStream(sourceFile, FileMode.Open))
            {
                inStream.Position = 0;
                byte[] computedHash = hmac.ComputeHash(inStream);

                if (computedHash.Length != Stored_HMAC.Length)
                    return false;

                for (int i = 0; i < Stored_HMAC.Length; i++)
                {
                    if (computedHash[i] != Stored_HMAC[i])
                        return false;
                }
            }
        }
        return true;
    }

    #region Helpers

    private static string PrettyPrintHash(this byte[] arr)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < arr.Length; i++)
        {
            sb.Append($"{arr[i]:X2}");
            if ((i % 4) == 3)
                sb.Append(" ");
        }
        return sb.ToString();
    }

    public static byte[] FromPrettyPrint(this string s) => jnUtil.Hex.FromHexString(s.Replace(" ", ""));

    public static void CopyFile(string sourceFile, string destFile)
    {
        using (FileStream inStream = new FileStream(sourceFile, FileMode.Open))
        {
            using (FileStream outStream = new FileStream(destFile, FileMode.Create))
            {
                inStream.Position = 0;
                int bytesRead;
                byte[] buffer = new byte[BUFFER_SIZE];
                do
                {
                    bytesRead = inStream.Read(buffer, 0, 1024);
                    outStream.Write(buffer, 0, bytesRead);
                } while (bytesRead > 0);
            }
        }
        return;
    }

    public enum HASHTypes
    {
        MD5,
        SHA1,
        SHA256,
        SHA384,
        SHA512
    }

    #endregion
}
