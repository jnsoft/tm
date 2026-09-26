namespace TM.Services;

/// <summary>Protects session-only key material while a document is open.</summary>
public interface IInMemoryKeyProtector
{
    byte[] Protect(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> entropy);
    byte[] Unprotect(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> entropy);
}
