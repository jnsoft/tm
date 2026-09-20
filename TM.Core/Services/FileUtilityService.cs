using System.Security.Cryptography;

namespace TM.Services;

public enum FileUtilityOperation { Sha256, Sha384, Sha512, Md5, Sha1, Base64Encode, Base64Decode }

/// <summary>Streaming, dialog-free tools. Never overwrites a destination.</summary>
public sealed class FileUtilityService
{
    public async Task ExecuteAsync(FileUtilityOperation operation, string sourcePath, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        cancellationToken.ThrowIfCancellationRequested();
        string source = Path.GetFullPath(sourcePath);
        string destination = Path.GetFullPath(destinationPath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) || File.Exists(destination))
            throw new IOException("Choose a new output file.");
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".tm-tool-{Guid.NewGuid():N}.tmp");
        bool created = false;
        try
        {
            await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                created = true;
                if (operation is FileUtilityOperation.Base64Encode)
                {
                    using ToBase64Transform transform = new();
                    await using CryptoStream encoder = new(output, transform, CryptoStreamMode.Write, leaveOpen: true);
                    byte[] buffer = new byte[65536];
                    try
                    {
                        int read;
                        while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
                            await encoder.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                    finally { CryptographicOperations.ZeroMemory(buffer); }
                    await encoder.FlushFinalBlockAsync(cancellationToken);
                }
                else if (operation is FileUtilityOperation.Base64Decode)
                    await DecodeAsync(input, output, cancellationToken);
                else
                {
                    using HashAlgorithm hash = operation switch
                    {
                        FileUtilityOperation.Md5 => MD5.Create(),
                        FileUtilityOperation.Sha1 => SHA1.Create(),
                        FileUtilityOperation.Sha256 => SHA256.Create(),
                        FileUtilityOperation.Sha384 => SHA384.Create(),
                        FileUtilityOperation.Sha512 => SHA512.Create(),
                        _ => throw new ArgumentOutOfRangeException(nameof(operation))
                    };
                    byte[] digest = await hash.ComputeHashAsync(input, cancellationToken);
                    string hex = Convert.ToHexString(digest);
                    string grouped = string.Join(" ", Enumerable.Range(0, hex.Length / 8).Select(i => hex.Substring(i * 8, 8))) + " ";
                    await output.WriteAsync(Encoding.UTF8.GetBytes(grouped), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (created && File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task DecodeAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[65536];
        byte[] decoded = new byte[49152];
        char[] quartet = new char[4];
        int count = 0, outputCount = 0, prefix = 0;
        bool start = true, padded = false;
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
            {
                for (int i = 0; i < read; i++)
                {
                    byte value = buffer[i];
                    if (start)
                    {
                        start = false;
                        if (value == 0xEF) { prefix = 1; continue; }
                    }
                    if (prefix is 1 or 2)
                    {
                        if (value != (prefix == 1 ? 0xBB : 0xBF)) throw new FormatException("Invalid Base64 input.");
                        prefix++;
                        continue;
                    }
                    if (value is 9 or 10 or 13 or 32) continue;
                    if (padded || value > 127) throw new FormatException("Invalid Base64 input.");
                    quartet[count++] = (char)value;
                    if (count != 4) continue;
                    if (!Convert.TryFromBase64Chars(quartet, decoded.AsSpan(outputCount), out int written))
                        throw new FormatException("Invalid Base64 input.");
                    outputCount += written;
                    padded = quartet[3] == '=';
                    count = 0;
                    if (outputCount > decoded.Length - 3)
                    {
                        await output.WriteAsync(decoded.AsMemory(0, outputCount), cancellationToken);
                        outputCount = 0;
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (count != 0 || prefix is 1 or 2) throw new FormatException("Incomplete Base64 input.");
            await output.WriteAsync(decoded.AsMemory(0, outputCount), cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(decoded);
            Array.Clear(quartet);
        }
    }
}
