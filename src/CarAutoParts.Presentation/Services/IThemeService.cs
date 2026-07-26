namespace CarAutoParts.Presentation.Services;

public interface IThemeService
{
    bool IsDark { get; }
    event EventHandler? ThemeChanged;
    void Initialize();
    void ToggleTheme();
    void SetDark(bool isDark);
}
