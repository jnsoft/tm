using System.Windows.Controls;
using System.Windows.Data;
using TM.Entities;

namespace TM.Helpers;

[ValueConversion(typeof(object), typeof(string))]
public class TreeViewItemToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ((TreeViewItem)value).Header;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PriorityToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return (Priority)Enum.Parse(typeof(Priority), value.ToString(), true);
    }
}

public class TabSizeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        TabControl tabControl = values[0] as TabControl;
        double width = tabControl.ActualWidth / tabControl.Items.Count;
        //Subtract 1, otherwise we could overflow to two rows.
        return (width <= 1) ? 0 : (width - 2);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter,
        System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
