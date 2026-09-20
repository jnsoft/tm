namespace TM.Desktop.Services;

public sealed class ExpiringClipboardService(INativeClipboard clipboard, TimeProvider clock) : BackgroundService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(15);
    private readonly SemaphoreSlim gate = new(1, 1);
    private uint? sequence;
    private DateTimeOffset expires;

    public async Task CopyAsync(string text, CancellationToken cancellationToken = default)
    {
        if (text.Contains('\0')) throw new InvalidOperationException("Passwords containing a null character cannot be copied.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            sequence = await clipboard.WriteAsync(text, cancellationToken);
            expires = clock.GetUtcNow() + Lifetime;
        }
        finally { gate.Release(); }
    }

    public async Task ClearOwnedAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            expires = DateTimeOffset.MinValue;
            await ClearCoreAsync(cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task ExpireAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (clock.GetUtcNow() >= expires) await ClearCoreAsync(cancellationToken);
        }
        finally { gate.Release(); }
    }

    private async Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        if (sequence is { } owned && await clipboard.ClearIfOwnedAsync(owned, cancellationToken))
            sequence = null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ExpireAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await ClearOwnedAsync(cancellationToken);
    }
}
