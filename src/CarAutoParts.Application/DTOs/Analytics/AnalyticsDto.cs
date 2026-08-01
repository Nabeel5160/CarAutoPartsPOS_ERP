namespace CarAutoParts.Application.DTOs.Analytics;

/// <summary>Inventory and sales analytics aggregates.</summary>
public record AnalyticsDto(
    IReadOnlyList<TopProductAnalyticsDto> TopSellingProducts,
    IReadOnlyList<SlowMovingProductDto> SlowMovingProducts,
    IReadOnlyList<AbcAnalysisItemDto> AbcAnalysis,
    decimal TotalInventoryValue,
    decimal TurnoverRatio,
    IReadOnlyList<DeadStockItemDto> DeadStock,
    IReadOnlyList<FastMoverItemDto> FastMovers,
    decimal GrossMarginAmount,
    decimal GrossMarginPercent,
    int? BranchId);

/// <summary>Top-selling product analytics row.</summary>
public record TopProductAnalyticsDto(
    int ProductId,
    string ProductName,
    string Sku,
    decimal QuantitySold,
    decimal Revenue,
    decimal Profit);

/// <summary>Slow-moving product analytics row.</summary>
public record SlowMovingProductDto(
    int ProductId,
    string ProductName,
    string Sku,
    decimal QuantityOnHand,
    int DaysSinceLastSale);

/// <summary>ABC classification item.</summary>
public record AbcAnalysisItemDto(
    int ProductId,
    string ProductName,
    string Sku,
    decimal AnnualUsageValue,
    decimal CumulativePercentage,
    char AbcClass);

/// <summary>Dead stock: on-hand with no/aged sales.</summary>
public record DeadStockItemDto(
    int ProductId,
    string ProductName,
    string Sku,
    decimal QuantityOnHand,
    decimal StockValue,
    int DaysSinceLastSale);

/// <summary>Fast mover by period velocity.</summary>
public record FastMoverItemDto(
    int ProductId,
    string ProductName,
    string Sku,
    decimal QuantitySold,
    decimal Revenue,
    decimal Profit);
