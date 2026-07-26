using System.IO;
using System.Text.Json;

namespace CarAutoParts.Presentation.Services;

public class UserPreferenceService : IUserPreferenceService
{
    private readonly string _filePath;
    private UserPreferences _prefs;

    public UserPreferenceService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CarAutoPartsERP");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "user-preferences.json");
        _prefs = Load();
    }

    public bool GetSidebarOpen(bool defaultValue = true) => _prefs.IsSidebarOpen ?? defaultValue;

    public void SetSidebarOpen(bool isOpen)
    {
        _prefs.IsSidebarOpen = isOpen;
        Save();
    }

    public bool GetSidebarPinned(bool defaultValue = false) => _prefs.IsSidebarPinned ?? defaultValue;

    public void SetSidebarPinned(bool isPinned)
    {
        _prefs.IsSidebarPinned = isPinned;
        Save();
    }

    public bool GetDarkTheme(bool defaultValue = false) => _prefs.IsDarkTheme ?? defaultValue;

    public void SetDarkTheme(bool isDark)
    {
        _prefs.IsDarkTheme = isDark;
        Save();
    }

    private UserPreferences Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new UserPreferences();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
        }
        catch
        {
            return new UserPreferences();
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_prefs);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Preferences are best-effort.
        }
    }

    private sealed class UserPreferences
    {
        public bool? IsSidebarOpen { get; set; }
        public bool? IsSidebarPinned { get; set; }
        public bool? IsDarkTheme { get; set; }
    }
}
