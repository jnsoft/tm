namespace TM.Desktop.Services;

/// <summary>Native clipboard boundary. Tokens identify writes, not their plaintext.</summary>
public interface INativeClipboard
{
    Task<uint> WriteAsync(string text, CancellationToken cancellationToken = default);
    /// <returns>False only when the clipboard is busy and cleanup should be retried.</returns>
    Task<bool> ClearIfOwnedAsync(uint sequence, CancellationToken cancellationToken = default);
}
