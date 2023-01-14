using System.IO;


namespace TM.Helpers;

public static class FileHelper
{
    private const int PBKDF2_ITERATIONS = 110000;
    public static bool GetFileName(out string path, string title, string fileTypes = "", string fileTypeEndingFilter = "")
    {
        var dlg = new Microsoft.Win32.OpenFileDialog();
        dlg.Title = title;


        if (!string.IsNullOrWhiteSpace(fileTypes) && !string.IsNullOrWhiteSpace(fileTypeEndingFilter))
        {
            dlg.Filter = $"{fileTypes} (.{fileTypeEndingFilter})|*.{fileTypeEndingFilter}|All Files (*.*)|*.*";
            dlg.FilterIndex = 1;
        }

        dlg.InitialDirectory = Directory.GetCurrentDirectory();

        path = null;

        bool? result = dlg.ShowDialog();

        if (result.HasValue && result.Value == true)
        {
            path = dlg.FileName;
            return true;
        }
        else
            return false;

    }

    public static bool SetFileName(out string path, string title, string fileTypes = "", string fileTypeEndingFilter = "")
    {
        Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
        dlg.Title = title;

        if (!string.IsNullOrWhiteSpace(fileTypes) && !string.IsNullOrWhiteSpace(fileTypeEndingFilter))
        {
            dlg.Filter = $"{fileTypes} (.{fileTypeEndingFilter})|*.{fileTypeEndingFilter}|All Files (*.*)|*.*";
            dlg.FilterIndex = 1;
        }

        dlg.InitialDirectory = Directory.GetCurrentDirectory();

        path = null;

        bool? result = dlg.ShowDialog();

        if (result.HasValue && result.Value == true)
        {
            path = dlg.FileName;
            return true;
        }
        else
            return false;

    }

    public static bool EncryptFile(string path)
    {
        SecurityHelper.EncryptFile_Account(path);
        string newFilename = path + ".enc";
        if (!File.Exists(newFilename))
            File.Move(path, newFilename);
        return true;

    }

    public static bool DecryptFile(string path)
    {

        SecurityHelper.DecryptFile_Account(path);

        string extension = Path.GetExtension(path);
        string newFilename = path;

        if (extension == ".enc")
            newFilename = path.Substring(0, path.Length - extension.Length);

        if (!File.Exists(newFilename))
            File.Move(path, newFilename);

        return true;
    }

    public static bool EncryptFile(string path, SecureString Password)
    {
        string newFileName = path + ".aes";
        if (SecurityHelper.AesEncryptFile(path, newFileName, Password))
        {
            SecurityHelper.WipeFile(path);
            return true;
        }
        else
            return false;
    }

    public static bool DecryptFile(string path, SecureString Password)
    {
        string extension = Path.GetExtension(path);
        string outPath = path;

        if (extension == ".aes")
            outPath = path.Substring(0, path.Length - extension.Length);

        return SecurityHelper.AesDecryptFile(path, outPath, Password);
    }

    public static bool EncryptFile(string path, ref byte[] key, byte[] salt)
    {
        string newFileName = path + ".aes";
        if (SecurityHelper.AesEncryptFile(path, newFileName, ref key, salt))
        {
            SecurityHelper.WipeFile(path);
            return true;
        }
        else
            return false;
    }

    public static bool DecryptFile(string path, ref byte[] key)
    {
        string extension = Path.GetExtension(path);
        string outPath = path;

        if (extension == ".aes")
            outPath = path.Substring(0, path.Length - extension.Length);

        return SecurityHelper.AesDecryptFile(path, outPath, ref key, 32);
    }

    public static byte[] ReadSaltFromFile(string path) => SecurityHelper.AesGetSaltToDecryptFile(path, 32);

    private static bool AppendToFile(string path, byte[] data)
    {
        FileInfo fi = new FileInfo(path);
        FileStream fs = fi.Open(FileMode.Append);
        fs.Write(data, 0, data.Length);
        fs.SetLength(fi.Length + data.Length);
        fs.Close();
        return true;
    }

    private static byte[] RemoveEndFromFile(string path, int NoOfBytesToDelete)
    {
        byte[] data = new byte[NoOfBytesToDelete];

        FileInfo fi = new FileInfo(path);
        FileStream fs = fi.Open(FileMode.Open);

        fs.Seek(0 - NoOfBytesToDelete, SeekOrigin.End);
        fs.Read(data, 0, NoOfBytesToDelete);

        long bytesToDelete = NoOfBytesToDelete;
        fs.SetLength(Math.Max(0, fi.Length - bytesToDelete));

        fs.Close();

        return data;
    }

}
