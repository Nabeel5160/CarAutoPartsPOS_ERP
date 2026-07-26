using System.Collections.ObjectModel;
using System.Windows;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IThemeService _themeService;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;
    private readonly IAuthService _authService;
    private readonly IUserPreferenceService _userPreferences;
    private readonly IGlobalSearchService _globalSearchService;
    private readonly INavigationState _navigationState;
    private readonly ISnackbarService _snackbarService;

    public ShellViewModel(
        INavigationService navigationService,
        IThemeService themeService,
        ICurrentUserService currentUser,
        INotificationService notificationService,
        IAuthService authService,
        IUserPreferenceService userPreferences,
        IGlobalSearchService globalSearchService,
        INavigationState navigationState,
        ISnackbarService snackbarService)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _authService = authService;
        _userPreferences = userPreferences;
        _globalSearchService = globalSearchService;
        _navigationState = navigationState;
        _snackbarService = snackbarService;

        IsSidebarOpen = _userPreferences.GetSidebarOpen();
        IsSidebarPinned = _userPreferences.GetSidebarPinned();

        _navigationService.Navigated += (_, _) =>
        {
            CurrentView = _navigationService.CurrentView;
            SyncNavigationSelection();
        };

        BuildNavigationItems();
        _themeService.ThemeChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ThemeToggleToolTip));
        };
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private int _unreadNotifications;

    [ObservableProperty]
    private bool _isSidebarOpen = true;

    [ObservableProperty]
    private bool _isSidebarPinned;

    [ObservableProperty]
    private string _currentPageTitle = "Dashboard";

    [ObservableProperty]
    private string _globalSearchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchResultsOpen;

    public ObservableCollection<GlobalSearchResult> SearchResults { get; } = new();

    public string UserDisplayName => _currentUser.CurrentUser?.DisplayName ?? "User";
    public bool IsDarkTheme => _themeService.IsDark;
    public string ThemeToggleToolTip => IsDarkTheme ? "Switch to light theme" : "Switch to dark theme";
    public Visibility NotificationBadgeVisibility => UnreadNotifications > 0 ? Visibility.Visible : Visibility.Collapsed;
    public bool IsSidebarVisible => IsSidebarPinned || IsSidebarOpen;
    public string SidebarPinToolTip => IsSidebarPinned ? "Unpin sidebar (allow hide)" : "Pin sidebar (keep visible)";

    [RelayCommand]
    private void ToggleSidebar()
    {
        if (IsSidebarPinned)
        {
            _snackbarService.Show("Sidebar is pinned. Unpin it first to hide the menu.");
            return;
        }

        IsSidebarOpen = !IsSidebarOpen;
        _userPreferences.SetSidebarOpen(IsSidebarOpen);
        NotifySidebarLayoutChanged();
    }

    [RelayCommand]
    private void ToggleSidebarPin()
    {
        IsSidebarPinned = !IsSidebarPinned;
        _userPreferences.SetSidebarPinned(IsSidebarPinned);

        if (IsSidebarPinned)
        {
            IsSidebarOpen = true;
            _userPreferences.SetSidebarOpen(true);
        }

        NotifySidebarLayoutChanged();
        _snackbarService.Show(IsSidebarPinned ? "Sidebar pinned." : "Sidebar unpinned.");
    }

    partial void OnIsSidebarOpenChanged(bool value)
    {
        if (!IsSidebarPinned)
            _userPreferences.SetSidebarOpen(value);

        NotifySidebarLayoutChanged();
    }

    partial void OnIsSidebarPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarPinToolTip));
        NotifySidebarLayoutChanged();
    }

    private void NotifySidebarLayoutChanged()
    {
        OnPropertyChanged(nameof(IsSidebarVisible));
        OnPropertyChanged(nameof(SidebarPinToolTip));
    }

    [RelayCommand]
    private async Task NavigateAsync(NavigationItem? item)
    {
        if (item is null)
            return;

        foreach (var nav in NavigationItems)
            nav.IsSelected = nav == item;

        CurrentPageTitle = item.Title;
        await _navigationService.NavigateToAsync(item.ViewModelType);
        CurrentView = _navigationService.CurrentView;
        await RefreshNotificationCountAsync();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        _snackbarService.Show(IsDarkTheme ? "Dark theme enabled." : "Light theme enabled.");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand]
    private async Task GlobalSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(GlobalSearchText))
        {
            IsSearchResultsOpen = false;
            return;
        }

        var results = await _globalSearchService.SearchAsync(GlobalSearchText);
        SearchResults.Clear();
        foreach (var result in results)
            SearchResults.Add(result);

        if (results.Count == 0)
        {
            IsSearchResultsOpen = false;
            _snackbarService.Show("No matches found.");
            return;
        }

        if (results.Count == 1)
        {
            IsSearchResultsOpen = false;
            await NavigateToSearchResultAsync(results[0]);
            return;
        }

        IsSearchResultsOpen = true;
    }

    [RelayCommand]
    private async Task SelectSearchResultAsync(GlobalSearchResult? match)
    {
        if (match is null)
            return;

        IsSearchResultsOpen = false;
        await NavigateToSearchResultAsync(match);
    }

    private async Task NavigateToSearchResultAsync(GlobalSearchResult match)
    {
        switch (match.Kind)
        {
            case GlobalSearchResultKind.Product:
                _navigationState.ProductSearch = match.SearchHint;
                break;
            case GlobalSearchResultKind.Customer:
                _navigationState.CustomerSearch = match.SearchHint;
                break;
            case GlobalSearchResultKind.Supplier:
                _navigationState.SupplierSearch = match.SearchHint;
                break;
            case GlobalSearchResultKind.PurchaseOrder:
                _navigationState.PurchaseOrderSearch = match.SearchHint;
                break;
            case GlobalSearchResultKind.SalesInvoice:
                _navigationState.SalesInvoiceSearch = match.SearchHint;
                break;
        }

        var nav = NavigationItems.FirstOrDefault(n => n.ViewModelType == match.ViewModelType);
        if (nav is not null)
        {
            await NavigateAsync(nav);
            _snackbarService.Show($"Opened: {match.Title}");
        }
        else
            _snackbarService.Show("Target module is not available.");
    }

    [RelayCommand]
    private void CloseSearchResults() => IsSearchResultsOpen = false;

    [RelayCommand]
    private async Task OpenNotificationsAsync()
    {
        var nav = NavigationItems.FirstOrDefault(n => n.ViewModelType == typeof(NotificationsViewModel));
        if (nav is null)
        {
            _snackbarService.Show("Notifications module is not available.");
            return;
        }

        await NavigateAsync(nav);
    }

    [RelayCommand]
    private async Task RefreshNotificationsAsync() => await RefreshNotificationCountAsync();

    private async Task RefreshNotificationCountAsync()
    {
        UnreadNotifications = await _notificationService.GetUnreadCountAsync();
        OnPropertyChanged(nameof(NotificationBadgeVisibility));
    }

    partial void OnUnreadNotificationsChanged(int value) =>
        OnPropertyChanged(nameof(NotificationBadgeVisibility));

    public override async Task InitializeAsync()
    {
        _themeService.Initialize();
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeToggleToolTip));

        var dashboard = NavigationItems.FirstOrDefault();
        if (dashboard is not null)
            await NavigateAsync(dashboard);
        else
            await RefreshNotificationCountAsync();
    }

    private void SyncNavigationSelection()
    {
        if (CurrentView is null)
            return;

        var vmType = CurrentView.GetType();
        var nav = NavigationItems.FirstOrDefault(n => n.ViewModelType == vmType);
        if (nav is null)
            return;

        foreach (var item in NavigationItems)
            item.IsSelected = item == nav;

        CurrentPageTitle = nav.Title;
    }

    private void BuildNavigationItems()
    {
        var items = new (string Title, string Icon, string? Permission, Type Vm)[]
        {
            ("Dashboard", "ViewDashboard", Permissions.DashboardView, typeof(DashboardViewModel)),
            ("Products", "PackageVariantClosed", Permissions.ProductsView, typeof(ProductsViewModel)),
            ("Categories", "Shape", Permissions.CategoriesView, typeof(CategoriesViewModel)),
            ("Brands", "Tag", Permissions.BrandsView, typeof(BrandsViewModel)),
            ("Warehouses", "Warehouse", Permissions.WarehousesView, typeof(WarehousesViewModel)),
            ("Inventory", "Archive", Permissions.InventoryView, typeof(InventoryViewModel)),
            ("Stock Movements", "SwapVertical", Permissions.InventoryView, typeof(StockMovementsViewModel)),
            ("Suppliers", "TruckDelivery", Permissions.SuppliersView, typeof(SuppliersViewModel)),
            ("Customers", "AccountGroup", Permissions.CustomersView, typeof(CustomersViewModel)),
            ("Purchase Orders", "CartArrowDown", Permissions.PurchasesView, typeof(PurchaseOrdersViewModel)),
            ("Sales Invoices", "Receipt", Permissions.SalesView, typeof(SalesInvoicesViewModel)),
            ("Sales History", "History", Permissions.SalesView, typeof(SalesHistoryViewModel)),
            ("POS", "CashRegister", Permissions.PosCheckout, typeof(PosViewModel)),
            ("Returns", "KeyboardReturn", Permissions.ReturnsManage, typeof(ReturnsViewModel)),
            ("Transfers", "SwapHorizontal", Permissions.TransfersView, typeof(TransfersViewModel)),
            ("Barcode", "Barcode", Permissions.ProductsView, typeof(BarcodeViewModel)),
            ("Serial Numbers", "Numeric", Permissions.SerialNumbersView, typeof(SerialNumbersViewModel)),
            ("Reports", "FileChart", Permissions.ReportsView, typeof(ReportsViewModel)),
            ("Analytics", "ChartLine", Permissions.AnalyticsView, typeof(AnalyticsViewModel)),
            ("Users", "AccountCog", Permissions.UsersView, typeof(UsersViewModel)),
            ("Audit Logs", "History", Permissions.AuditView, typeof(AuditLogsViewModel)),
            ("Settings", "Cog", Permissions.SettingsView, typeof(SettingsViewModel)),
            ("Backup", "Database", Permissions.BackupView, typeof(BackupViewModel)),
            ("Notifications", "Bell", null, typeof(NotificationsViewModel))
        };

        foreach (var (title, icon, permission, vm) in items)
        {
            if (permission is not null && !_currentUser.HasPermission(permission))
                continue;

            NavigationItems.Add(new NavigationItem
            {
                Title = title,
                Icon = icon,
                PermissionCode = permission,
                ViewModelType = vm
            });
        }
    }
}
