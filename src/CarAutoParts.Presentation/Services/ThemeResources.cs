using System.Windows;
using System.Windows.Media;

namespace CarAutoParts.Presentation.Services;

/// <summary>Theme brushes replaced on each apply so WPF never mutates frozen resources.</summary>
public static class ThemeResources
{
    private static readonly (string Key, Color Light, Color Dark)[] Palette =
    {
        ("AppBackgroundBrush", ColorFrom("#F1F5F9"), ColorFrom("#0B1220")),
        ("SurfaceBrush", ColorFrom("#FFFFFF"), ColorFrom("#111827")),
        ("CardBrush", ColorFrom("#F8FAFC"), ColorFrom("#1F2937")),
        ("BorderBrush", ColorFrom("#E2E8F0"), ColorFrom("#374151")),
        ("TextPrimaryBrush", ColorFrom("#0F172A"), ColorFrom("#F8FAFC")),
        ("TextSecondaryBrush", ColorFrom("#64748B"), ColorFrom("#94A3B8")),
        ("GlassBrush", ColorFrom("#CCFFFFFF"), ColorFrom("#CC111827")),
        ("NavSelectedBrush", ColorFrom("#DBEAFE"), ColorFrom("#1E3A5F")),
        ("NavSelectedForegroundBrush", ColorFrom("#1D4ED8"), ColorFrom("#93C5FD"))
    };

    public static void Apply(System.Windows.Application app, bool isDark)
    {
        foreach (var (key, light, dark) in Palette)
            app.Resources[key] = new SolidColorBrush(isDark ? dark : light);
    }

    private static Color ColorFrom(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex)!;
}
