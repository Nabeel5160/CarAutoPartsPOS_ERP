using CarAutoParts.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CarAutoParts.Presentation.Services;

public class NavigationService : INavigationService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private IServiceScope? _moduleScope;
    private object? _currentView;

    public NavigationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public object? CurrentView
    {
        get => _currentView;
        private set
        {
            _currentView = value;
            Navigated?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Navigated;

    public void NavigateTo<TViewModel>() where TViewModel : class
        => NavigateTo(typeof(TViewModel));

    public void NavigateTo(Type viewModelType)
    {
        ReplaceModuleScope();
        var viewModel = _moduleScope!.ServiceProvider.GetRequiredService(viewModelType);
        CurrentView = viewModel;
    }

    public async Task NavigateToAsync<TViewModel>() where TViewModel : class
        => await NavigateToAsync(typeof(TViewModel));

    public async Task NavigateToAsync(Type viewModelType)
    {
        ReplaceModuleScope();
        var viewModel = _moduleScope!.ServiceProvider.GetRequiredService(viewModelType);

        if (viewModel is IAsyncInitializable initializable)
            await initializable.InitializeAsync();

        CurrentView = viewModel;
    }

    public void Dispose()
    {
        _moduleScope?.Dispose();
        _moduleScope = null;
    }

    private void ReplaceModuleScope()
    {
        _moduleScope?.Dispose();
        _moduleScope = _scopeFactory.CreateScope();
    }
}
