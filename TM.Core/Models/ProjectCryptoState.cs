
using System.Security.Cryptography.X509Certificates;
using TM.Common;

namespace TM.Models;

public sealed class ProjectCryptoState : ObservableObject
{
    private string? publicKey;
    private X509Certificate2? caCertificate;

    internal byte[]? ProtectedMasterKey { get; set; }
    internal byte[]? Salt { get; set; }
    internal byte[]? Entropy { get; set; }
    internal byte[]? ProtectedPrivateKey { get; set; }
    internal byte[]? PrivateKeyEntropy { get; set; }

    public string? PublicKey
    {
        get => publicKey;
        set
        {
            if (!SetProperty(ref publicKey, value))
                return;

            OnPropertyChanged(nameof(IsDiffieHellmanEnabled));
        }
    }

    public X509Certificate2? CaCertificate
    {
        get => caCertificate;
        set
        {
            if (!SetProperty(ref caCertificate, value))
                return;

            OnPropertyChanged(nameof(IsPkiEnabled));
        }
    }

    public bool IsDiffieHellmanEnabled =>
        ProtectedPrivateKey is not null && !string.IsNullOrWhiteSpace(PublicKey);

    public bool IsPkiEnabled => CaCertificate is not null;
}
