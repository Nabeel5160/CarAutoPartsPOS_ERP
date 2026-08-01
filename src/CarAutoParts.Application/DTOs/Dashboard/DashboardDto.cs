namespace CarAutoParts.Application.DTOs.Dashboard;

/// <summary>Dashboard KPI and chart aggregates.</summary>
public record DashboardDto(
    decimal TodaySales,
    decimal MonthSales,
    decimal MonthPurchases,
    decimal InventoryValue,
    int LowStockCount,
    int PendingPurchaseOrders,
    int PendingTransfers,
    int UnreadNotifications,
    IReadOnlyList<MonthlySalesPoint> MonthlySales,
    IReadOnlyList<InventoryTrendPoint> InventoryTrend,
    IReadOnlyList<TopProductPoint> TopProducts,
    IReadOnlyList<CategoryDistributionPoint> CategoryDistribution,
    int? BranchId = null,
    decimal TodayCashVariance = 0,
    int OpenShifts = 0);

/// <summary>Monthly sales data point for charts.</summary>
public record MonthlySalesPoint(string Month, decimal Sales, decimal Purchases);

/// <summary>Inventory value trend data point.</summary>
public record InventoryTrendPoint(DateTime Date, decimal Value, decimal Quantity);

/// <summary>Top-selling product data point.</summary>
public record TopProductPoint(int ProductId, string ProductName, string Sku, decimal QuantitySold, decimal Revenue);

/// <summary>Category distribution data point.</summary>
public record CategoryDistributionPoint(int CategoryId, string CategoryName, decimal Value, decimal Percentage);

/// <summary>Animated sales pulse timeline for dashboard charts.</summary>
public record DashboardTimelineDto(
    DateTime From,
    DateTime To,
    string Grain,
    string GroupBy,
    int? BranchId,
    IReadOnlyList<string> SeriesKeys,
    IReadOnlyList<DashboardTimelineFrameDto> Frames,
    IReadOnlyList<string> Months,
    IReadOnlyList<DashboardBar3DPointDto> CategoryMonthSales);

public record DashboardTimelineFrameDto(
    string Label,
    DateTime PeriodStart,
    IReadOnlyList<decimal> Values,
    decimal Total);

public record DashboardBar3DPointDto(string Category, string Month, decimal Sales);
