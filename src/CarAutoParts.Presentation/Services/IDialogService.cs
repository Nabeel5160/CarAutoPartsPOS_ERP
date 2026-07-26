namespace CarAutoParts.Presentation.Services;

public interface IDialogService
{
    void SetDialogHostIdentifier(object identifier);
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
}
