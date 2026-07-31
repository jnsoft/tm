using TM.Helpers;

namespace TM.Services;

public sealed class ShellService
{
    public void CopyToClipboard(string text, int secondsToHold)
    {
        if (!string.IsNullOrWhiteSpace(text))
            ClipBoardHelper.LoadClipBoard(text, secondsToHold);
    }

    public void Shutdown() => Application.Current.Shutdown();
}
