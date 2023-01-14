using System.Windows.Threading;

namespace TM.Helpers;

public static class ClipBoardHelper
{
    private static DispatcherTimer timer = new DispatcherTimer();
    public static void LoadClipBoard(string text, int seconds = 15)
    {
        //System.Timers.Timer timer = new System.Timers.Timer(seconds * 1000);
        //timer.Elapsed += ClearClipBoardEvent;
        //timer.AutoReset = false;
        //timer.Enabled = true;

        // access to clipboard only from UI thread
        timer.Stop();
        timer.Interval = TimeSpan.FromSeconds(seconds);
        timer.Tick += ClearClipBoardEvent;
        timer.Start();

        Clipboard.SetText(text);
    }

    private static void ClearClipBoardEvent(Object sender, EventArgs e)
    {
        Clipboard.Clear();
        ((DispatcherTimer)sender).Stop();
    }
}
