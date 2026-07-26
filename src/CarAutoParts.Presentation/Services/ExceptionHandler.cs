using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Presentation.Services;

public class ExceptionHandler : IExceptionHandler
{
    private readonly IDialogService _dialogService;
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<ExceptionHandler> _logger;

    public ExceptionHandler(
        IDialogService dialogService,
        ISnackbarService snackbarService,
        ILogger<ExceptionHandler> logger)
    {
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _logger = logger;
    }

    public void Handle(Exception exception, string? context = null)
    {
        exception = Unwrap(exception);
        _logger.LogError(exception, "Unhandled exception{Context}", context is null ? "" : $" ({context})");
        var message = GetFriendlyMessage(exception);

        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is { HasShutdownStarted: false } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => ShowError("Something went wrong", message));
            return;
        }

        ShowError("Something went wrong", message);
    }

    public async Task ExecuteAsync(Func<Task> action, string? context = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Handle(ex, context);
        }
    }

    public async Task<T?> ExecuteAsync<T>(Func<Task<T>> action, string? context = null)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            Handle(ex, context);
            return default;
        }
    }

    private void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static Exception Unwrap(Exception exception)
    {
        while (true)
        {
            if (exception is TargetInvocationException { InnerException: not null } tie)
            {
                exception = tie.InnerException;
                continue;
            }

            if (exception is AggregateException { InnerException: not null } aggregate)
            {
                exception = aggregate.InnerException;
                continue;
            }

            return exception;
        }
    }

    private static string GetFriendlyMessage(Exception exception)
    {
        exception = Unwrap(exception);

        return exception switch
        {
            InvalidOperationException => exception.Message,
            UnauthorizedAccessException => "You do not have permission to perform this action.",
            _ => exception.Message
        };
    }
}
