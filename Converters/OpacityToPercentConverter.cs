using System.Globalization;
using System.Windows.Data;

namespace WindowPilot.Converters;

public sealed class OpacityToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int percent ? $"{percent}% 不透明" : "100% 不透明";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Replace("%", string.Empty).Trim();
        return int.TryParse(text, out var result) ? result : System.Windows.Data.Binding.DoNothing;
    }
}
