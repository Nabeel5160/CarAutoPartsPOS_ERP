using CarAutoParts.Presentation.Services;
using CarAutoParts.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CarAutoParts.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddScoped<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IExceptionHandler, ExceptionHandler>();

        services.AddSingleton<IUserPreferenceService, UserPreferenceService>();
        services.AddSingleton<INavigationState, NavigationState>();
        services.AddScoped<IGlobalSearchService, GlobalSearchService>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<BrandsViewModel>();
        services.AddTransient<WarehousesViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<StockMovementsViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<PurchaseOrdersViewModel>();
        services.AddTransient<SalesInvoicesViewModel>();
        services.AddTransient<SalesHistoryViewModel>();
        services.AddTransient<PosViewModel>();
        services.AddTransient<ReturnsViewModel>();
        services.AddTransient<TransfersViewModel>();
        services.AddTransient<BarcodeViewModel>();
        services.AddTransient<SerialNumbersViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<AuditLogsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<NotificationsViewModel>();

        return services;
    }
}
