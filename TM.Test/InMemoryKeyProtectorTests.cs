using System.Security.Cryptography;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class InMemoryKeyProtectorTests
{
    [TestMethod]
    public void EphemeralProtector_RoundTripsOnlyWithTheOriginalEntropy()
    {
        using EphemeralInMemoryKeyProtector protector = new();
        byte[] plaintext = RandomNumberGenerator.GetBytes(32);
        byte[] entropy = RandomNumberGenerator.GetBytes(32);
        byte[] wrongEntropy = RandomNumberGenerator.GetBytes(32);
        byte[]? protectedValue = null;
        byte[]? recovered = null;
        try
        {
            protectedValue = protector.Protect(plaintext, entropy);
            recovered = protector.Unprotect(protectedValue, entropy);

            CollectionAssert.AreEqual(plaintext, recovered);
            Assert.Throws<CryptographicException>(() => protector.Unprotect(protectedValue, wrongEntropy));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(entropy);
            CryptographicOperations.ZeroMemory(wrongEntropy);
            if (protectedValue is not null) CryptographicOperations.ZeroMemory(protectedValue);
            if (recovered is not null) CryptographicOperations.ZeroMemory(recovered);
        }
    }

    [TestMethod]
    public void EphemeralProtector_CannotBeUnprotectedByAnotherProcessSession()
    {
        using EphemeralInMemoryKeyProtector exporter = new();
        using EphemeralInMemoryKeyProtector importer = new();
        byte[] plaintext = RandomNumberGenerator.GetBytes(32);
        byte[] entropy = RandomNumberGenerator.GetBytes(32);
        byte[]? protectedValue = null;
        try
        {
            protectedValue = exporter.Protect(plaintext, entropy);
            Assert.Throws<CryptographicException>(() => importer.Unprotect(protectedValue, entropy));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(entropy);
            if (protectedValue is not null) CryptographicOperations.ZeroMemory(protectedValue);
        }
    }
}
