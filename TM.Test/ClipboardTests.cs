using TM.Desktop.Services;

namespace TM.Test;

[TestClass]
public sealed class ClipboardTests
{
    [TestMethod]
    public async Task Expiry_RepeatedCopy_UsesLatestDeadlineAsync()
    {
        TestNativeClipboard native = new();
        TestClock clock = new();
        using ExpiringClipboardService service = new(native, clock);
        await service.CopyAsync("first");
        clock.Advance(10);
        await service.CopyAsync("second");
        clock.Advance(5);
        await service.ExpireAsync();
        Assert.AreEqual("second", native.Text);
        clock.Advance(10);
        await service.ExpireAsync();
        Assert.IsNull(native.Text);
    }

    [TestMethod]
    public async Task ExpiryAndClear_PreserveExternalCopiesEvenWithIdenticalTextAsync()
    {
        TestNativeClipboard native = new();
        TestClock clock = new();
        using ExpiringClipboardService service = new(native, clock);
        foreach (string external in new[] { "different", "secret" })
        {
            await service.CopyAsync("secret");
            native.ExternalCopy(external);
            clock.Advance(16);
            await service.ExpireAsync();
            await service.ClearOwnedAsync();
            Assert.AreEqual(external, native.Text);
        }
    }

    [TestMethod]
    public async Task BusyClear_RetriesWithoutWaitingForOriginalExpiryAsync()
    {
        TestNativeClipboard native = new();
        using ExpiringClipboardService service = new(native, new TestClock());
        await service.CopyAsync("secret");
        native.Busy = true;
        await service.ClearOwnedAsync();
        Assert.AreEqual("secret", native.Text);
        native.Busy = false;
        await service.ExpireAsync();
        Assert.IsNull(native.Text);
    }

    [TestMethod]
    public async Task FailedCanceledAndNullCharacterCopy_DoNotLoseExistingExpiryAsync()
    {
        TestNativeClipboard native = new();
        TestClock clock = new();
        using ExpiringClipboardService service = new(native, clock);
        await service.CopyAsync("original");
        native.Busy = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CopyAsync("failed"));
        native.Busy = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CopyAsync("null\0value"));
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.CopyAsync("canceled", canceled.Token));
        Assert.AreEqual("original", native.Text);
        clock.Advance(15);
        await service.ExpireAsync();
        Assert.IsNull(native.Text);
    }

    [TestMethod]
    public async Task Stop_ClearsOwnedButNotExternalClipboardAsync()
    {
        TestNativeClipboard native = new();
        using ExpiringClipboardService service = new(native, TimeProvider.System);
        await service.StartAsync(CancellationToken.None);
        await service.CopyAsync("secret");
        await service.StopAsync(CancellationToken.None);
        Assert.IsNull(native.Text);
        await service.CopyAsync("next");
        native.ExternalCopy("external");
        await service.ClearOwnedAsync();
        Assert.AreEqual("external", native.Text);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(int seconds) => now = now.AddSeconds(seconds);
    }
}

internal sealed class TestNativeClipboard : INativeClipboard
{
    private uint sequence;
    private bool owned;
    public string? Text { get; private set; }
    public bool Busy { get; set; }
    public int Writes { get; private set; }

    public Task<uint> WriteAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Busy) throw new InvalidOperationException("The clipboard is busy. Try copying again.");
        Text = new string(text.AsSpan());
        owned = true;
        Writes++;
        return Task.FromResult(++sequence);
    }

    public Task<bool> ClearIfOwnedAsync(uint expected, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Busy) return Task.FromResult(false);
        if (owned && sequence == expected)
        {
            Text = null;
            owned = false;
            sequence++;
        }
        return Task.FromResult(true);
    }

    public void ExternalCopy(string text)
    {
        Text = text;
        owned = false;
        sequence++;
    }
}
