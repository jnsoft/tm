using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TM.Test;

[TestClass]
public class ProjectCryptoStateTests
{
    [TestMethod]
    public void PublicKey_Set_WithPrivateKeyEnabled_RaisesStateChangeAndEnablesDiffieHellman()
    {
        ProjectCryptoState state = new()
        {
            ProtectedPrivateKey = [1, 2, 3]
        };

        List<string?> changedProperties = [];
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        state.PublicKey = "public-key";

        Assert.IsTrue(state.IsDiffieHellmanEnabled);
        Assert.IsTrue(changedProperties.Contains(nameof(ProjectCryptoState.PublicKey)));
        Assert.IsTrue(changedProperties.Contains(nameof(ProjectCryptoState.IsDiffieHellmanEnabled)));
    }

    [TestMethod]
    public void PublicKey_Set_ToWhitespace_DoesNotEnableDiffieHellman()
    {
        ProjectCryptoState state = new()
        {
            ProtectedPrivateKey = [1, 2, 3]
        };

        state.PublicKey = "   ";

        Assert.IsFalse(state.IsDiffieHellmanEnabled);
    }

    [TestMethod]
    public void CaCertificate_Set_RaisesStateChangeAndEnablesPki()
    {
        ProjectCryptoState state = new();

        List<string?> changedProperties = [];
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        state.CaCertificate = CreateCertificate();

        Assert.IsTrue(state.IsPkiEnabled);
        Assert.IsTrue(changedProperties.Contains(nameof(ProjectCryptoState.CaCertificate)));
        Assert.IsTrue(changedProperties.Contains(nameof(ProjectCryptoState.IsPkiEnabled)));
    }

    private static X509Certificate2 CreateCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(
            "CN=TM.Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(7));
    }
}
