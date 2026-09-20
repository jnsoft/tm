using System.Security.Cryptography;

namespace TM.Services;

public enum HmacAlgorithm { Sha256, Sha384, Sha512, Md5, Sha1 }
public enum HmacOperation { Create, Verify }

/// <summary>Caller serializes document changes through completion.</summary>
public sealed class DocumentHmacService(ProjectCryptoService crypto)
{
    public async Task CreateAsync(ProjectDocument document, HmacAlgorithm algorithm, string sourcePath,
        string outputPath, CancellationToken cancellationToken = default)
    {
        Validate(document, algorithm);
        cancellationToken.ThrowIfCancellationRequested();
        string source = Path.GetFullPath(sourcePath), output = Path.GetFullPath(outputPath);
        if (string.Equals(source, output, StringComparison.OrdinalIgnoreCase) || File.Exists(output))
            throw new IOException("Choose a new output file.");
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        byte[] mac = await ComputeAsync(document, algorithm, source, salt, cancellationToken);
        string hex = Convert.ToHexString(mac);
        string text = string.Join(" ", Enumerable.Range(0, hex.Length / 8).Select(i => hex.Substring(i * 8, 8)))
            + " \n" + Convert.ToBase64String(salt);
        string temporary = Path.Combine(Path.GetDirectoryName(output)!, $".tm-hmac-{Guid.NewGuid():N}.tmp");
        bool created = false;
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous))
            {
                created = true;
                await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, output, overwrite: false);
        }
        finally { if (created && File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<bool> VerifyAsync(ProjectDocument document, HmacAlgorithm algorithm, string sourcePath,
        string sidecarPath, CancellationToken cancellationToken = default)
    {
        Validate(document, algorithm);
        byte[] bytes = new byte[4097];
        await using FileStream stream = new(sidecarPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        int count = 0, read;
        while (count < bytes.Length && (read = await stream.ReadAsync(bytes.AsMemory(count), cancellationToken)) != 0)
            count += read;
        if (count > 4096) throw new FormatException("HMAC sidecar is too large.");
        string text = new UTF8Encoding(false, true).GetString(bytes, 0, count).TrimStart('\uFEFF');
        string[] lines = text.TrimEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length != 2) throw new FormatException("Invalid HMAC sidecar.");
        byte[] expected = Convert.FromHexString(lines[0].Replace(" ", "", StringComparison.Ordinal));
        byte[] salt = Convert.FromBase64String(lines[1]);
        int length = algorithm switch
        {
            HmacAlgorithm.Md5 => 16, HmacAlgorithm.Sha1 => 20, HmacAlgorithm.Sha256 => 32,
            HmacAlgorithm.Sha384 => 48, HmacAlgorithm.Sha512 => 64, _ => 0
        };
        if (expected.Length != length || salt.Length != 32) throw new FormatException("Invalid HMAC or salt length.");
        byte[] actual = await ComputeAsync(document, algorithm, sourcePath, salt, cancellationToken);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private async Task<byte[]> ComputeAsync(ProjectDocument document, HmacAlgorithm algorithm, string source,
        byte[] salt, CancellationToken cancellationToken)
    {
        byte[]? key = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            key = crypto.DeriveKey(document, "HMAC", salt, 64);
            using HMAC hmac = algorithm switch
            {
                HmacAlgorithm.Md5 => new HMACMD5(key), HmacAlgorithm.Sha1 => new HMACSHA1(key),
                HmacAlgorithm.Sha256 => new HMACSHA256(key), HmacAlgorithm.Sha384 => new HMACSHA384(key),
                HmacAlgorithm.Sha512 => new HMACSHA512(key), _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
            };
            await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await hmac.ComputeHashAsync(input, cancellationToken);
        }
        finally { ProjectCryptoService.ClearArray(ref key); }
    }

    private static void Validate(ProjectDocument document, HmacAlgorithm algorithm)
    {
        if (!Enum.IsDefined(algorithm)) throw new ArgumentOutOfRangeException(nameof(algorithm));
        if (!document.IsFileLoaded || document.IsLocked || document.Security.ProtectedMasterKey is null)
            throw new InvalidOperationException("Open or unlock a document first.");
    }
}
