using System.Windows.Controls;

namespace TM.Helpers;

public static class WpfDialogHelper
{
    public static bool GetPassword(string Title, string DialogMessage, out SecureString password)
    {
        password = null;

        Window w = new Window();
        //  w.Owner = owner;

        setProperties(w, Title);

        TextBlock text = new TextBlock();
        text.Text = DialogMessage;
        text.Margin = new Thickness(10, 10, 10, 0);

        PasswordBox pw = new PasswordBox();
        pw.MaxLength = 256;
        pw.Margin = new Thickness(10, 10, 10, 0);

        WrapPanel wp = new WrapPanel();
        wp.HorizontalAlignment = HorizontalAlignment.Center;
        wp.Children.Add(getButton("Ok", ok_Click, new Thickness(0, 0, 10, 0)));
        wp.Children.Add(getButton("Cancel", cancel_Click, new Thickness(10, 0, 0, 0)));
        wp.Margin = new Thickness(0, 20, 0, 0);

        StackPanel sp = new StackPanel();
        sp.Children.Add(text);
        sp.Children.Add(pw);
        sp.Children.Add(wp);
        w.Content = sp;

        pw.Focus();

        if (w.ShowDialog() == true)
        {
            password = pw.SecurePassword;
            return true;
        }

        return false;

        void ok_Click(object sender, RoutedEventArgs e) => w.DialogResult = true;

        void cancel_Click(object sender, RoutedEventArgs e) => w.Close();
    }

    public static bool GetText(string Title, string DialogMessage, out string input)
    {
        input = "";

        Window w = new Window();
        //  w.Owner = owner;

        setProperties(w, Title);

        TextBlock text = new TextBlock();
        text.Text = DialogMessage;
        text.Margin = new Thickness(10, 10, 10, 0);

        TextBox tb = new TextBox();
        tb.MaxLength = 256;
        tb.Margin = new Thickness(10, 10, 10, 0);
        tb.AcceptsReturn = true;

        WrapPanel wp = new WrapPanel();
        wp.HorizontalAlignment = HorizontalAlignment.Center;
        wp.Children.Add(getButton("Ok", ok_Click, new Thickness(0, 0, 10, 0)));
        wp.Children.Add(getButton("Cancel", cancel_Click, new Thickness(10, 0, 0, 0)));
        wp.Margin = new Thickness(0, 20, 0, 0);

        StackPanel sp = new StackPanel();
        sp.Children.Add(text);
        sp.Children.Add(tb);
        sp.Children.Add(wp);
        w.Content = sp;

        tb.Focus();

        if (w.ShowDialog() == true)
        {
            input = tb.Text;
            return true;
        }

        return false;

        void ok_Click(object sender, RoutedEventArgs e) => w.DialogResult = true;

        void cancel_Click(object sender, RoutedEventArgs e) => w.Close();
    }

    private static Button getButton(string text, RoutedEventHandler eventhandler, Thickness margin)
    {
        Button btn = new Button();
        btn.Width = 60;
        btn.Height = 25;
        btn.Margin = margin;
        btn.Click += eventhandler;
        btn.Content = text;
        return btn;
    }

    private static void setProperties(Window w, string Title)
    {
        w.Height = 150;
        w.Width = 300;
        w.WindowStyle = WindowStyle.SingleBorderWindow;
        w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        w.ResizeMode = ResizeMode.NoResize;
        w.Title = Title;
    }

}
