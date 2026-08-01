using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

public class InventoryItem : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public decimal QuantityOnHand { get; set; }
    public decimal ReservedQuantity { get; set; }
    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.Average;
    public decimal AverageCost { get; set; }
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
    public ICollection<StockBatch> Batches { get; set; } = new List<StockBatch>();
    public ICollection<InventoryLocationBalance> LocationBalances { get; set; } = new List<InventoryLocationBalance>();
}

/// <summary>
/// Bin-level quantity dimension. Warehouse rollup remains on <see cref="InventoryItem"/> for ATP.
/// Location balances should stay in sync with warehouse on-hand for putaway/pick.
/// </summary>
public class InventoryLocationBalance : BaseEntity
{
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public int WarehouseLocationId { get; set; }
    public WarehouseLocation WarehouseLocation { get; set; } = null!;
    public decimal QuantityOnHand { get; set; }
}

public class StockMovement : BaseEntity
{
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public StockMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
}

public class StockBatch : BaseEntity
{
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public decimal QuantityRemaining { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
}

public class SerialNumber : BaseEntity
{
    public string Serial { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public SerialNumberStatus Status { get; set; } = SerialNumberStatus.Available;
    public int? CurrentWarehouseId { get; set; }
    public Warehouse? CurrentWarehouse { get; set; }
    public ICollection<SerialNumberHistory> History { get; set; } = new List<SerialNumberHistory>();
}

public class SerialNumberHistory : BaseEntity
{
    public int SerialNumberId { get; set; }
    public SerialNumber SerialNumber { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
}
