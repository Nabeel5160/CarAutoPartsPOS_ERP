using System.IO;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CarAutoParts.Presentation.ViewModels;

public record ReportActionItem(string Title, string Description, string ActionKey);

public partial class ReportsViewModel : EntityModuleViewModelBase<ReportActionItem>
{
    private readonly IReportService _service;

    public ReportsViewModel(
        IReportService service,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Reports", "Export operational reports to Excel", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        PrimaryActionText = "Export Inventory";
    }

    [ObservableProperty]
    private DateTime _exportFromDate = DateTime.Today.AddMonths(-1);

    [ObservableProperty]
    private DateTime _exportToDate = DateTime.Today;

    protected override Task LoadItemsAsync()
    {
        Items.Add(new ReportActionItem(
            "Inventory Report",
            "Current stock levels and values across warehouses",
            "inventory"));
        Items.Add(new ReportActionItem(
            "Sales Report",
            "Sales invoices for a date range (Excel)",
            "sales"));
        Items.Add(new ReportActionItem(
            "Purchase Report",
            "Purchase orders for a date range (Excel)",
            "purchase"));
        Items.Add(new ReportActionItem(
            "Profit Report",
            "Product revenue summary for a date range (Excel)",
            "profit"));
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task NewAsync() => await ExportInventoryAsync();

    [RelayCommand]
    private async Task ExportInventoryAsync()
    {
        await ExportReportAsync("inventory", "Inventory Report", "InventoryReport.xlsx");
    }

    [RelayCommand]
    private async Task RunReportAsync(ReportActionItem? item)
    {
        if (item is null)
            return;

        switch (item.ActionKey)
        {
            case "inventory":
                await ExportInventoryAsync();
                break;
            case "sales":
                await ExportReportAsync("sales", "Sales Report", "SalesReport.xlsx");
                break;
            case "purchase":
                await ExportReportAsync("purchase", "Purchase Report", "PurchaseReport.xlsx");
                break;
            case "profit":
                await ExportReportAsync("profit", "Profit Report", "ProfitReport.xlsx");
                break;
        }
    }

    private async Task ExportReportAsync(string reportKey, string title, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = $"Save {title}",
            Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            FileName = defaultFileName,
            DefaultExt = ".xlsx"
        };

        if (dialog.ShowDialog() != true)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            var from = ExportFromDate.Date;
            var to = ExportToDate.Date.AddDays(1).AddTicks(-1);

            byte[] bytes = reportKey switch
            {
                "inventory" => await _service.ExportInventoryReportAsync("xlsx"),
                "sales" => await _service.ExportSalesReportAsync(from, to, "monthly", "xlsx"),
                "purchase" => await _service.ExportPurchaseReportAsync(from, to, "xlsx"),
                "profit" => await _service.ExportProfitReportAsync(from, to, "xlsx"),
                _ => throw new InvalidOperationException("Unknown report type.")
            };

            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            SnackbarService.Show($"{title} exported successfully.");
            IsBusy = false;
        }, title);
    }
}
