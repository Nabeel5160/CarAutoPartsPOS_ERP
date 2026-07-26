using System.Windows;
using MaterialDesignThemes.Wpf;

namespace CarAutoParts.Presentation.Services;

public class ThemeService : IThemeService
{
    private readonly IUserPreferenceService _preferences;
    private bool _isDark;
    private bool _initialized;

    public ThemeService(IUserPreferenceService preferences)
    {
        _preferences = preferences;
        _isDark = _preferences.GetDarkTheme(false);
    }

    public bool IsDark => _isDark;

    public event EventHandler? ThemeChanged;

    public void Initialize()
    {
        if (_initialized || System.Windows.Application.Current is null)
            return;

        ApplyTheme(_isDark, notify: false);
        _initialized = true;
    }

    public void ToggleTheme() => SetDark(!_isDark);

    public void SetDark(bool isDark)
    {
        if (!_initialized)
            Initialize();

        if (_isDark == isDark)
            return;

        _isDark = isDark;
        _preferences.SetDarkTheme(isDark);
        ApplyTheme(isDark, notify: true);
    }

    private void ApplyTheme(bool isDark, bool notify)
    {
        ApplyMaterialTheme(isDark);
        ThemeResources.Apply(System.Windows.Application.Current, isDark);

        if (notify)
            ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyMaterialTheme(bool isDark)
    {
        var helper = new PaletteHelper();
        var theme = helper.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
        helper.SetTheme(theme);
    }
}
