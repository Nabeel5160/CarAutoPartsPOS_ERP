using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PosWpf.Common;

/// <summary>Formats a decimal as PKR currency.</summary>
public class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d) return $"Rs {d:N2}";
        if (value is int i) return $"Rs {i:N2}";
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Maps a "truthy" value to Visibility. Handles bool, int/count (>0), and
/// strings (non-empty). Invert the result with parameter "invert".
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value switch
        {
            bool bb => bb,
            int i => i > 0,
            string s => !string.IsNullOrWhiteSpace(s),
            null => false,
            _ => true
        };
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase)) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Maps a success flag to a status brush (green / red).</summary>
public class StatusBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Success = new(Color.FromRgb(0x16, 0xA3, 0x4A));
    private static readonly SolidColorBrush Error = new(Color.FromRgb(0xDC, 0x26, 0x26));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Success : Error;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
