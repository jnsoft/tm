using System.Security.Cryptography;

namespace TM.Services;

/// <summary>Uses Windows DPAPI to protect session-only key material for the current user.</summary>
public sealed class WindowsInMemoryKeyProtector : IInMemoryKeyProtector
{
    public byte[] Protect(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> entropy)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows DPAPI is unavailable on this platform.");

        return ProtectedData.Protect(plaintext.ToArray(), entropy.ToArray(), DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> entropy)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows DPAPI is unavailable on this platform.");

        return ProtectedData.Unprotect(ciphertext.ToArray(), entropy.ToArray(), DataProtectionScope.CurrentUser);
    }
}
