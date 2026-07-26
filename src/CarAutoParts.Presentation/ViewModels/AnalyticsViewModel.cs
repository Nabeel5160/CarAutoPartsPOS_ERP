using System.Collections.ObjectModel;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CarAutoParts.Presentation.ViewModels;

public partial class AnalyticsViewModel : ViewModelBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IExceptionHandler _exceptionHandler;

    public AnalyticsViewModel(IAnalyticsService analyticsService, IExceptionHandler exceptionHandler)
    {
        _analyticsService = analyticsService;
        _exceptionHandler = exceptionHandler;
        FromDate = DateTime.Today.AddDays(-30);
        ToDate = DateTime.Today;
    }

    public ObservableCollection<string> Insights { get; } = new();
    public ISeries[] TrendSeries { get; private set; } = Array.Empty<ISeries>();
    public Axis[] TrendXAxes { get; private set; } = Array.Empty<Axis>();

    [ObservableProperty]
    private DateTime _fromDate;

    [ObservableProperty]
    private DateTime _toDate;

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private Task ApplyDateFilterAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        await _exceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            var from = DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Utc);
            var to = DateTime.SpecifyKind(ToDate.Date, DateTimeKind.Utc);

            if (from > to)
            {
                Insights.Clear();
                Insights.Add("From date must be on or before To date.");
                IsBusy = false;
                return;
            }

            var data = await _analyticsService.GetAnalyticsAsync(from, to);
            Insights.Clear();
            Insights.Add($"Period: {FromDate:d} — {ToDate:d}");
            Insights.Add($"Inventory value: {data.TotalInventoryValue:N2}");
            Insights.Add($"Turnover ratio: {data.TurnoverRatio:N2}");
            foreach (var top in data.TopSellingProducts.Take(3))
                Insights.Add($"Top seller: {top.ProductName} — {top.Revenue:N2}");

            TrendXAxes =
            [
                new Axis
                {
                    Labels = data.TopSellingProducts.Take(6).Select(p => p.ProductName).ToArray(),
                    LabelsRotation = 15
                }
            ];
            TrendSeries =
            [
                new ColumnSeries<decimal>
                {
                    Name = "Revenue",
                    Values = data.TopSellingProducts.Take(6).Select(p => p.Revenue).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#2563EB"))
                }
            ];

            OnPropertyChanged(nameof(TrendSeries));
            OnPropertyChanged(nameof(TrendXAxes));
            IsBusy = false;
        }, "Analytics");
    }
}
