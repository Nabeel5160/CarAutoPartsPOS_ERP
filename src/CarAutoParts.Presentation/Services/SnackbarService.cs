using MaterialDesignThemes.Wpf;

namespace CarAutoParts.Presentation.Services;

public class SnackbarService : ISnackbarService
{
    private SnackbarMessageQueue _queue = new();

    public SnackbarMessageQueue MessageQueue => _queue;

    public void SetMessageQueue(SnackbarMessageQueue queue) => _queue = queue;

    public void Show(string message, string? actionContent = null, Action? actionHandler = null)
    {
        if (actionContent is not null && actionHandler is not null)
            _queue.Enqueue(message, actionContent, actionHandler);
        else
            _queue.Enqueue(message);
    }
}
