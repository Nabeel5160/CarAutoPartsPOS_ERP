using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.Inventory;

/// <summary>Inventory balance for a product in a warehouse.</summary>
public record InventoryItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string Sku,
    int WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    ValuationMethod ValuationMethod,
    decimal AverageCost,
    decimal StockValue);

/// <summary>Stock movement history entry.</summary>
public record StockMovementDto(
    int Id,
    int ProductId,
    string ProductName,
    int WarehouseId,
    string WarehouseName,
    StockMovementType MovementType,
    decimal Quantity,
    decimal UnitCost,
    string? ReferenceType,
    int? ReferenceId,
    string? Notes,
    DateTime MovementDate);

/// <summary>Inventory list filter criteria.</summary>
public class InventoryQueryDto
{
    public string? Search { get; set; }
    public int? ProductId { get; set; }
    public int? WarehouseId { get; set; }
    public bool LowStockOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Stock movement history filter criteria.</summary>
public class StockMovementQueryDto
{
    public int? ProductId { get; set; }
    public int? WarehouseId { get; set; }
    public StockMovementType? MovementType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Manual stock adjustment request.</summary>
public record StockAdjustmentDto(
    int ProductId,
    int WarehouseId,
    decimal QuantityDelta,
    string? Reason);

/// <summary>Low or overstock alert row.</summary>
public record LowStockAlertDto(
    int ProductId,
    string ProductName,
    string Sku,
    int WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    int MinimumStock,
    int ReorderLevel,
    bool IsOverstock);

/// <summary>Inventory valuation result (average or FIFO batch sum).</summary>
public record InventoryValueDto(
    decimal Value,
    string Method,
    int? WarehouseId,
    int? BranchId);

/// <summary>Tracked serial number.</summary>
public record SerialNumberDto(
    int Id,
    string Serial,
    int ProductId,
    string ProductName,
    SerialNumberStatus Status,
    int? CurrentWarehouseId,
    string? CurrentWarehouseName);

/// <summary>Serial number lifecycle event.</summary>
public record SerialNumberHistoryDto(
    int Id,
    string Action,
    string? ReferenceType,
    int? ReferenceId,
    string? Notes,
    DateTime ActionDate);
