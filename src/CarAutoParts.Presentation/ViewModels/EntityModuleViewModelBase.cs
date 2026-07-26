using System.Collections.ObjectModel;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public abstract partial class EntityModuleViewModelBase<TListItem> : ViewModelBase
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    protected EntityModuleViewModelBase(
        string pageTitle,
        string pageSubtitle,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
    {
        PageTitle = pageTitle;
        PageSubtitle = pageSubtitle;
        ExceptionHandler = exceptionHandler;
        SnackbarService = snackbarService;
        DialogService = dialogService;
    }

    protected IExceptionHandler ExceptionHandler { get; }
    protected ISnackbarService SnackbarService { get; }
    protected IDialogService DialogService { get; }

    public string PageTitle { get; }
    public string PageSubtitle { get; }

    public ObservableCollection<TListItem> Items { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _placeholderMessage = "Loading...";

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _showPaging;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private string _primaryActionText = "New";

    [ObservableProperty]
    private bool _showPrimaryAction = true;

    /// <summary>When true, filter/search handlers skip redundant reloads during module init.</summary>
    protected bool SuppressFilterRefresh { get; set; }

    /// <summary>True after the first successful module load completes.</summary>
    protected bool IsModuleReady { get; set; }

    public override async Task InitializeAsync()
    {
        IsModuleReady = false;
        SuppressFilterRefresh = true;
        try
        {
            await RefreshAsync();
        }
        finally
        {
            SuppressFilterRefresh = false;
            IsModuleReady = true;
        }
    }

    [RelayCommand]
    protected async Task RefreshAsync()
    {
        await _refreshGate.WaitAsync();
        try
        {
            await ExceptionHandler.ExecuteAsync(async () =>
            {
                IsBusy = true;
                PlaceholderMessage = string.Empty;
                Items.Clear();
                await LoadItemsAsync();
                PlaceholderMessage = Items.Count == 0 ? "No records found." : string.Empty;
                IsBusy = false;
            }, PageTitle);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    [RelayCommand]
    protected async Task SearchAsync()
    {
        if (SuppressFilterRefresh || !IsModuleReady || IsBusy)
            return;

        CurrentPage = 1;
        await RefreshAsync();
    }

    protected abstract Task LoadItemsAsync();
}
