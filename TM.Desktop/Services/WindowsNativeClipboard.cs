using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace TM.Desktop.Services;

/// <summary>Windows-only clipboard owner with a dedicated, message-pumping STA thread.</summary>
public sealed class WindowsNativeClipboard : INativeClipboard, IAsyncDisposable
{
    private const uint UnicodeText = 13;
    private readonly BlockingCollection<Action<nint>> work = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WindowsNativeClipboard()
    {
        Thread thread = new(Run) { IsBackground = true, Name = "TM Clipboard" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public Task<uint> WriteAsync(string text, CancellationToken cancellationToken = default) =>
        InvokeAsync(window => Write(window, text), cancellationToken);

    public Task<bool> ClearIfOwnedAsync(uint sequence, CancellationToken cancellationToken = default) =>
        InvokeAsync(window =>
        {
            if (!OpenClipboard(window)) return false;
            try
            {
                if (GetClipboardOwner() != window || GetClipboardSequenceNumber() != sequence) return true;
                return EmptyClipboard();
            }
            finally { CloseClipboard(); }
        }, cancellationToken);

    private async Task<T> InvokeAsync<T>(Func<nint, T> action, CancellationToken cancellationToken)
    {
        await ready.Task.WaitAsync(cancellationToken);
        TaskCompletionSource<T> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        work.Add(window =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.TrySetResult(action(window));
            }
            catch (OperationCanceledException) { result.TrySetCanceled(cancellationToken); }
            catch (Exception error) { result.TrySetException(error); }
        }, cancellationToken);
        // Once queued, await completion: never abandon a write that might still copy a secret.
        return await result.Task;
    }

    private void Run()
    {
        nint window = 0;
        try
        {
            window = CreateWindowExW(0, "STATIC", "TM Clipboard", 0, 0, 0, 0, 0, new nint(-3), 0, 0, 0);
            if (window == 0) throw new InvalidOperationException("Native clipboard is unavailable.");
            ready.TrySetResult();
            while (!work.IsCompleted)
            {
                if (work.TryTake(out Action<nint>? action, 50)) action(window);
                while (PeekMessageW(out Message message, 0, 0, 0, 1))
                {
                    TranslateMessage(ref message);
                    DispatchMessageW(ref message);
                }
            }
        }
        catch (Exception error) { ready.TrySetException(error); }
        finally
        {
            if (window != 0) DestroyWindow(window);
            stopped.TrySetResult();
        }
    }

    private static uint Write(nint window, string text)
    {
        if (text.Contains('\0')) throw new InvalidOperationException("Passwords containing a null character cannot be copied.");
        if (!OpenClipboard(window)) throw new InvalidOperationException("The clipboard is busy. Try copying again.");
        try
        {
            if (!EmptyClipboard()) throw new InvalidOperationException("The clipboard could not be updated.");
            // Windows recognizes these hints; third-party clipboard managers may ignore them.
            SetFlag("CanIncludeInClipboardHistory", 0);
            SetFlag("CanUploadToCloudClipboard", 0);
            SetFlag("ExcludeClipboardContentFromMonitorProcessing", 1);
            int bytes = checked((text.Length + 1) * 2);
            nint memory = GlobalAlloc(0x0042, (nuint)bytes); // GMEM_MOVEABLE | GMEM_ZEROINIT
            if (memory == 0) throw new InvalidOperationException("The clipboard could not be updated.");
            bool transferred = false;
            try
            {
                nint pointer = GlobalLock(memory);
                if (pointer == 0) throw new InvalidOperationException("The clipboard could not be updated.");
                try
                {
                    for (int i = 0; i < text.Length; i++) Marshal.WriteInt16(pointer, i * 2, unchecked((short)text[i]));
                }
                finally { GlobalUnlock(memory); }
                if (SetClipboardData(UnicodeText, memory) == 0)
                    throw new InvalidOperationException("The clipboard could not be updated.");
                transferred = true; // The OS now owns the allocation.
                return GetClipboardSequenceNumber();
            }
            finally
            {
                if (!transferred)
                {
                    nint pointer = GlobalLock(memory);
                    if (pointer != 0)
                    {
                        for (int i = 0; i < bytes; i++) Marshal.WriteByte(pointer, i, 0);
                        GlobalUnlock(memory);
                    }
                    GlobalFree(memory);
                }
            }
        }
        finally { CloseClipboard(); }
    }

    private static void SetFlag(string name, int value)
    {
        uint format = RegisterClipboardFormatW(name);
        if (format == 0) return;
        nint memory = GlobalAlloc(0x0042, 4);
        if (memory == 0) return;
        nint pointer = GlobalLock(memory);
        if (pointer == 0) { GlobalFree(memory); return; }
        Marshal.WriteInt32(pointer, value);
        GlobalUnlock(memory);
        if (SetClipboardData(format, memory) == 0) GlobalFree(memory);
    }

    public async ValueTask DisposeAsync()
    {
        work.CompleteAdding();
        await stopped.Task;
        work.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Window;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(uint exStyle, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(nint window);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern nint GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] private static extern nint SetClipboardData(uint format, nint memory);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref Message message);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint memory);
}
