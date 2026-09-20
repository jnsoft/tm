namespace TM.Services;

/// <summary>Retains the legacy jnUtil Windows EFS implementation.</summary>
public sealed class WindowsAccountFileProtection : IAccountFileProtection
{
    public bool IsEncrypted(string path) => (File.GetAttributes(path) & FileAttributes.Encrypted) != 0;

    public void SetEncrypted(string path, bool encrypted)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows EFS is required.");
        if (encrypted) SecurityHelper.EncryptFile_Account(path);
        else SecurityHelper.DecryptFile_Account(path);
    }
}
