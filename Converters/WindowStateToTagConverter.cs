using System.Globalization;
using System.Windows.Data;
using WindowPilot.Models;

namespace WindowPilot.Converters;

public sealed class WindowStateToTagConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is SavedWindowState state
            ? state switch
            {
                SavedWindowState.Minimized => "最小化",
                SavedWindowState.Maximized => "最大化",
                _ => "普通"
            }
            : "普通";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
