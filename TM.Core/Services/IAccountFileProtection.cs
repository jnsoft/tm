namespace TM.Services;

/// <summary>Native filesystem encryption boundary, separate from document DPAPI protection.</summary>
public interface IAccountFileProtection
{
    bool IsEncrypted(string path);
    void SetEncrypted(string path, bool encrypted);
}
