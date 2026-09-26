using System.Security.Cryptography;

namespace TM.Services;

/// <summary>
/// Protects session-only keys with a process-local random key when an OS key-protection service is unavailable.
/// The key is never persisted, so protected values cannot survive an application restart.
/// </summary>
public sealed class EphemeralInMemoryKeyProtector : IInMemoryKeyProtector, IDisposable
{
    private const int KeyLength = 32;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private byte[]? key = RandomNumberGenerator.GetBytes(KeyLength);

    public byte[] Protect(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> entropy)
    {
        byte[] activeKey = key ?? throw new ObjectDisposedException(nameof(EphemeralInMemoryKeyProtector));
        byte[] result = new byte[checked(NonceLength + TagLength + plaintext.Length)];
        Span<byte> nonce = result.AsSpan(0, NonceLength);
        Span<byte> tag = result.AsSpan(NonceLength, TagLength);
        Span<byte> ciphertext = result.AsSpan(NonceLength + TagLength);
        RandomNumberGenerator.Fill(nonce);
        using AesGcm cipher = new(activeKey, TagLength);
        cipher.Encrypt(nonce, plaintext, ciphertext, tag, entropy);
        return result;
    }

    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> entropy)
    {
        if (ciphertext.Length < NonceLength + TagLength)
            throw new CryptographicException("The protected key is invalid.");

        byte[] activeKey = key ?? throw new ObjectDisposedException(nameof(EphemeralInMemoryKeyProtector));
        ReadOnlySpan<byte> nonce = ciphertext[..NonceLength];
        ReadOnlySpan<byte> tag = ciphertext.Slice(NonceLength, TagLength);
        ReadOnlySpan<byte> encrypted = ciphertext[(NonceLength + TagLength)..];
        byte[] plaintext = new byte[encrypted.Length];
        using AesGcm cipher = new(activeKey, TagLength);
        cipher.Decrypt(nonce, encrypted, tag, plaintext, entropy);
        return plaintext;
    }

    public void Dispose()
    {
        if (key is null)
            return;

        CryptographicOperations.ZeroMemory(key);
        key = null;
    }
}
