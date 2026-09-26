namespace TM.Desktop.Services;

/// <summary>
/// Safe fallback for platforms without an ownership-aware clipboard implementation.
/// It refuses secrets rather than clearing a clipboard value that another application may own.
/// </summary>
public sealed class UnsupportedNativeClipboard(string platformName) : INativeClipboard
{
    private readonly string platform = string.IsNullOrWhiteSpace(platformName)
        ? throw new ArgumentException("A platform name is required.", nameof(platformName))
        : platformName;

    public Task<uint> WriteAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new PlatformNotSupportedException(
            $"Timed secret clipboard copy is not yet available on {platform}. The password remains visible only in TM.");
    }

    public Task<bool> ClearIfOwnedAsync(uint sequence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
