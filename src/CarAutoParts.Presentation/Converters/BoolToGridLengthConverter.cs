using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CarAutoParts.Presentation.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    public double VisibleWidth { get; set; } = 280;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is true;
        return visible ? new GridLength(VisibleWidth) : new GridLength(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
