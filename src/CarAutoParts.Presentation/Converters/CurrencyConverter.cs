using System.Globalization;
using System.Windows.Data;

namespace CarAutoParts.Presentation.Converters;

public class CurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return d.ToString("N2", culture);
        if (value is double dbl)
            return dbl.ToString("N2", culture);
        if (value is int i)
            return i.ToString("N2", culture);
        return "0.00";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && decimal.TryParse(s, NumberStyles.Number, culture, out var result))
            return result;
        return 0m;
    }
}
