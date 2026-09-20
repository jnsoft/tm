using TM.Services;

namespace TM.Desktop.Services;

public sealed class FileToolsService(FileUtilityService files, IFileToolDialogs dialogs) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<string> ExecuteAsync(FileUtilityOperation operation, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation)) return "Choose a supported file operation.";
        await gate.WaitAsync(cancellationToken);
        try
        {
            string? input = await dialogs.SelectInputAsync(cancellationToken);
            if (input is null) return "Operation canceled. No output was created.";
            cancellationToken.ThrowIfCancellationRequested();
            string suffix = operation switch
            {
                FileUtilityOperation.Base64Encode => ".b64",
                FileUtilityOperation.Base64Decode => ".decoded",
                _ => "." + operation.ToString().ToLowerInvariant()
            };
            string? output = await dialogs.SelectOutputAsync(Path.GetFileName(input) + suffix, cancellationToken);
            if (output is null) return "Operation canceled. No output was created.";
            await files.ExecuteAsync(operation, input, output, cancellationToken);
            return "Output file created. The source file was not changed.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException
            or System.Security.Cryptography.CryptographicException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return "File operation failed. Check the input and permissions, and choose a new output file. Existing files are not overwritten.";
        }
        finally { gate.Release(); }
    }

    public void Dispose() => gate.Dispose();
}
