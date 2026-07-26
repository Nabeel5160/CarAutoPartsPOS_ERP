namespace CarAutoParts.Presentation.Services;

public interface INavigationService
{
    object? CurrentView { get; }
    event EventHandler? Navigated;
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo(Type viewModelType);
    Task NavigateToAsync<TViewModel>() where TViewModel : class;
    Task NavigateToAsync(Type viewModelType);
}
