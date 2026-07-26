namespace CarAutoParts.Presentation.Services;

public interface IUserPreferenceService
{
    bool GetSidebarOpen(bool defaultValue = true);
    void SetSidebarOpen(bool isOpen);

    bool GetSidebarPinned(bool defaultValue = false);
    void SetSidebarPinned(bool isPinned);

    bool GetDarkTheme(bool defaultValue = false);
    void SetDarkTheme(bool isDark);
}
