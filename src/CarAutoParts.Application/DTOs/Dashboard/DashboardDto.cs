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
    IReadOnlyList<CategoryDistributionPoint> CategoryDistribution);

/// <summary>Monthly sales data point for charts.</summary>
public record MonthlySalesPoint(string Month, decimal Sales, decimal Purchases);

/// <summary>Inventory value trend data point.</summary>
public record InventoryTrendPoint(DateTime Date, decimal Value, decimal Quantity);

/// <summary>Top-selling product data point.</summary>
public record TopProductPoint(int ProductId, string ProductName, string Sku, decimal QuantitySold, decimal Revenue);

/// <summary>Category distribution data point.</summary>
public record CategoryDistributionPoint(int CategoryId, string CategoryName, decimal Value, decimal Percentage);
