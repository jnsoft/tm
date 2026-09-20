namespace TM.Desktop.Services;

public interface IProjectFileDialogs
{
    Task<string?> OpenAsync(CancellationToken cancellationToken = default);
    Task<string?> SaveAsync(string? currentPath, CancellationToken cancellationToken = default);
}
