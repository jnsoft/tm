namespace TM.Desktop.Services;

public interface IFileToolDialogs
{
    Task<string?> SelectInputAsync(CancellationToken cancellationToken = default);
    Task<string?> SelectOutputAsync(string suggestedName, CancellationToken cancellationToken = default);
}
