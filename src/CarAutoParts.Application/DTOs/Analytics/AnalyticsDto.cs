namespace CarAutoParts.Application.DTOs.Analytics;

/// <summary>Inventory and sales analytics aggregates.</summary>
public record AnalyticsDto(
    IReadOnlyList<TopProductAnalyticsDto> TopSellingProducts,
    IReadOnlyList<SlowMovingProductDto> SlowMovingProducts,
    IReadOnlyList<AbcAnalysisItemDto> AbcAnalysis,
    decimal TotalInventoryValue,
    decimal TurnoverRatio);

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
