using CommunityToolkit.Mvvm.ComponentModel;

namespace CarAutoParts.Presentation.ViewModels;

public interface IAsyncInitializable
{
    Task InitializeAsync();
}

public abstract partial class ViewModelBase : ObservableObject, IAsyncInitializable
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public virtual Task InitializeAsync() => Task.CompletedTask;
}
