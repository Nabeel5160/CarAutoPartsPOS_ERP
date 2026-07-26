using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace CarAutoParts.Presentation.Converters;

public class PackIconKindConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && Enum.TryParse<PackIconKind>(s, out var kind))
            return kind;
        return PackIconKind.Circle;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
