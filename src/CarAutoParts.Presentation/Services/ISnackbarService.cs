using MaterialDesignThemes.Wpf;

namespace CarAutoParts.Presentation.Services;

public interface ISnackbarService
{
    SnackbarMessageQueue MessageQueue { get; }
    void SetMessageQueue(SnackbarMessageQueue queue);
    void Show(string message, string? actionContent = null, Action? actionHandler = null);
}
