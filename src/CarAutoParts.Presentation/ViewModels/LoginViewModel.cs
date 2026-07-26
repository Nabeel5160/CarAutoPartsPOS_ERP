using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IExceptionHandler _exceptionHandler;

    public LoginViewModel(IAuthService authService, IExceptionHandler exceptionHandler)
    {
        _authService = authService;
        _exceptionHandler = exceptionHandler;
    }

    [ObservableProperty]
    private string _username = "admin";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public event Action<bool>? LoginCompleted;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.LoginAsync(Username.Trim(), Password);
            if (result is null)
            {
                ErrorMessage = "Invalid username or password.";
                LoginCompleted?.Invoke(false);
                return;
            }

            LoginCompleted?.Invoke(true);
        }
        catch (Exception ex)
        {
            _exceptionHandler.Handle(ex, "Login");
            ErrorMessage = "Unable to sign in. Please try again.";
            LoginCompleted?.Invoke(false);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
