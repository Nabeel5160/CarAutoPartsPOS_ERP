using System.Collections.ObjectModel;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CarAutoParts.Presentation.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IExceptionHandler _exceptionHandler;
    private readonly INavigationService _navigationService;
    private readonly INavigationState _navigationState;

    public DashboardViewModel(
        IDashboardService dashboardService,
        IExceptionHandler exceptionHandler,
        INavigationService navigationService,
        INavigationState navigationState)
    {
        _dashboardService = dashboardService;
        _exceptionHandler = exceptionHandler;
        _navigationService = navigationService;
        _navigationState = navigationState;
    }

    public ObservableCollection<KpiItem> Kpis { get; } = new();

    public ISeries[] SalesSeries { get; private set; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; private set; } = Array.Empty<Axis>();
    public ISeries[] CategorySeries { get; private set; } = Array.Empty<ISeries>();

    public override async Task InitializeAsync()
    {
        await _exceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            var data = await _dashboardService.GetDashboardAsync();
            Kpis.Clear();
            Kpis.Add(new KpiItem("Today's Sales", data.TodaySales, "CurrencyUsd", "#2563EB"));
            Kpis.Add(new KpiItem("Month Sales", data.MonthSales, "TrendingUp", "#16A34A"));
            Kpis.Add(new KpiItem("Inventory Value", data.InventoryValue, "Warehouse", "#0EA5E9"));
            Kpis.Add(new KpiItem("Low Stock Items", data.LowStockCount, "Alert", "#D97706", ViewLowStockCommand));
            Kpis.Add(new KpiItem("Pending POs", data.PendingPurchaseOrders, "CartArrowDown", "#7C3AED"));
            Kpis.Add(new KpiItem("Notifications", data.UnreadNotifications, "Bell", "#DC2626"));

            var salesLabels = data.MonthlySales.Select(m => m.Month).ToArray();
            XAxes = [new Axis { Labels = salesLabels, LabelsRotation = 15 }];
            SalesSeries =
            [
                new ColumnSeries<decimal>
                {
                    Name = "Sales",
                    Values = data.MonthlySales.Select(m => m.Sales).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#2563EB"))
                },
                new LineSeries<decimal>
                {
                    Name = "Purchases",
                    Values = data.MonthlySales.Select(m => m.Purchases).ToArray(),
                    Stroke = new SolidColorPaint(SKColor.Parse("#0EA5E9"), 3),
                    Fill = null,
                    GeometrySize = 8
                }
            ];

            CategorySeries = data.CategoryDistribution
                .Select((c, i) => new PieSeries<decimal>
                {
                    Name = c.CategoryName,
                    Values = new[] { c.Value },
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsSize = 12
                })
                .Cast<ISeries>()
                .ToArray();

            OnPropertyChanged(nameof(SalesSeries));
            OnPropertyChanged(nameof(XAxes));
            OnPropertyChanged(nameof(CategorySeries));
            IsBusy = false;
        }, "Dashboard");
    }

    [RelayCommand]
    private Task RefreshAsync() => InitializeAsync();

    [RelayCommand]
    private async Task ViewLowStockAsync()
    {
        _navigationState.InventoryLowStockOnly = true;
        await _navigationService.NavigateToAsync<InventoryViewModel>();
    }
}

public record KpiItem(string Title, object Value, string Icon, string AccentColor, IRelayCommand? NavigateCommand = null);
