namespace CarAutoParts.Presentation.Services;

public interface IExceptionHandler
{
    void Handle(Exception exception, string? context = null);
    Task ExecuteAsync(Func<Task> action, string? context = null);
    Task<T?> ExecuteAsync<T>(Func<Task<T>> action, string? context = null);
}
