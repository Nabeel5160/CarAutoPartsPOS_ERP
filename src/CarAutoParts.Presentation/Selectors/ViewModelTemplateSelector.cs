using System.Windows;
using System.Windows.Controls;
using CarAutoParts.Presentation.ViewModels;
using CarAutoParts.Presentation.Views;

namespace CarAutoParts.Presentation.Selectors;

public class ViewModelTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DashboardTemplate { get; set; }
    public DataTemplate? ProductsTemplate { get; set; }
    public DataTemplate? PosTemplate { get; set; }
    public DataTemplate? BarcodeTemplate { get; set; }
    public DataTemplate? AnalyticsTemplate { get; set; }
    public DataTemplate? SettingsTemplate { get; set; }
    public DataTemplate? CategoriesTemplate { get; set; }
    public DataTemplate? CustomersTemplate { get; set; }
    public DataTemplate? ReportsTemplate { get; set; }
    public DataTemplate? BrandsTemplate { get; set; }
    public DataTemplate? SuppliersTemplate { get; set; }
    public DataTemplate? WarehousesTemplate { get; set; }
    public DataTemplate? InventoryTemplate { get; set; }
    public DataTemplate? StockMovementsTemplate { get; set; }
    public DataTemplate? PurchaseOrdersTemplate { get; set; }
    public DataTemplate? SalesInvoicesTemplate { get; set; }
    public DataTemplate? SalesHistoryTemplate { get; set; }
    public DataTemplate? ReturnsTemplate { get; set; }
    public DataTemplate? TransfersTemplate { get; set; }
    public DataTemplate? SerialNumbersTemplate { get; set; }
    public DataTemplate? UsersTemplate { get; set; }
    public DataTemplate? AuditLogsTemplate { get; set; }
    public DataTemplate? NotificationsTemplate { get; set; }
    public DataTemplate? BackupTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is null)
            return null;

        return item switch
        {
            DashboardViewModel => DashboardTemplate,
            ProductsViewModel => ProductsTemplate,
            PosViewModel => PosTemplate,
            BarcodeViewModel => BarcodeTemplate,
            AnalyticsViewModel => AnalyticsTemplate,
            SettingsViewModel => SettingsTemplate,
            CategoriesViewModel => CategoriesTemplate,
            CustomersViewModel => CustomersTemplate,
            ReportsViewModel => ReportsTemplate,
            BrandsViewModel => BrandsTemplate,
            SuppliersViewModel => SuppliersTemplate,
            WarehousesViewModel => WarehousesTemplate,
            InventoryViewModel => InventoryTemplate,
            StockMovementsViewModel => StockMovementsTemplate,
            PurchaseOrdersViewModel => PurchaseOrdersTemplate,
            SalesInvoicesViewModel => SalesInvoicesTemplate,
            SalesHistoryViewModel => SalesHistoryTemplate,
            ReturnsViewModel => ReturnsTemplate,
            TransfersViewModel => TransfersTemplate,
            SerialNumbersViewModel => SerialNumbersTemplate,
            UsersViewModel => UsersTemplate,
            AuditLogsViewModel => AuditLogsTemplate,
            NotificationsViewModel => NotificationsTemplate,
            BackupViewModel => BackupTemplate,
            _ => null
        };
    }

    public static ViewModelTemplateSelector CreateDefault()
    {
        return new ViewModelTemplateSelector
        {
            DashboardTemplate = CreateTemplate<DashboardView>(),
            ProductsTemplate = CreateTemplate<ProductsView>(),
            PosTemplate = CreateTemplate<PosView>(),
            BarcodeTemplate = CreateTemplate<BarcodeView>(),
            AnalyticsTemplate = CreateTemplate<AnalyticsView>(),
            SettingsTemplate = CreateTemplate<SettingsView>(),
            CategoriesTemplate = CreateTemplate<CategoriesView>(),
            CustomersTemplate = CreateTemplate<CustomersView>(),
            ReportsTemplate = CreateTemplate<ReportsView>(),
            BrandsTemplate = CreateTemplate<BrandsView>(),
            SuppliersTemplate = CreateTemplate<SuppliersView>(),
            WarehousesTemplate = CreateTemplate<WarehousesView>(),
            InventoryTemplate = CreateTemplate<InventoryView>(),
            StockMovementsTemplate = CreateTemplate<StockMovementsView>(),
            PurchaseOrdersTemplate = CreateTemplate<PurchaseOrdersView>(),
            SalesInvoicesTemplate = CreateTemplate<SalesInvoicesView>(),
            SalesHistoryTemplate = CreateTemplate<SalesHistoryView>(),
            ReturnsTemplate = CreateTemplate<ReturnsView>(),
            TransfersTemplate = CreateTemplate<TransfersView>(),
            SerialNumbersTemplate = CreateTemplate<SerialNumbersView>(),
            UsersTemplate = CreateTemplate<UsersView>(),
            AuditLogsTemplate = CreateTemplate<AuditLogsView>(),
            NotificationsTemplate = CreateTemplate<NotificationsView>(),
            BackupTemplate = CreateTemplate<BackupView>()
        };
    }

    private static DataTemplate CreateTemplate<TView>() where TView : FrameworkElement, new()
    {
        var template = new DataTemplate(typeof(ViewModelBase));
        var factory = new FrameworkElementFactory(typeof(TView));
        template.VisualTree = factory;
        return template;
    }
}
