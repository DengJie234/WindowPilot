using System.Globalization;
using System.Windows.Data;

namespace WindowPilot.Converters;

public sealed class BoolToStatusTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "已启用";
    public string FalseText { get; set; } = "未启用";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string text && text.Contains('|'))
        {
            var parts = text.Split('|');
            return value is bool b && b ? parts[0] : parts[1];
        }

        return value is bool result && result ? TrueText : FalseText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
