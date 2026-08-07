using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

// —— M2 Inventory / P2P ——

public enum ReservationStatus { Active = 0, Released = 1, Consumed = 2 }
public enum GrnStatus { Draft = 0, Posted = 1, Cancelled = 2, QcHold = 3 }
public enum PurchaseInvoiceStatus { Draft = 0, Matched = 1, Posted = 2, Cancelled = 3 }
public enum CycleCountStatus { Draft = 0, InProgress = 1, Completed = 2, Cancelled = 3 }

public class StockReservation : CompanyEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public int ReferenceId { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public DateTime? ExpiresAt { get; set; }
}

public class GoodsReceiptNote : CompanyEntity
{
    public string GrnNumber { get; set; } = string.Empty;
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public GrnStatus Status { get; set; } = GrnStatus.Draft;
    public decimal LandedCostAmount { get; set; }
    public string? Notes { get; set; }
    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
    public ICollection<GrnLandedCostLine> LandedCostLines { get; set; } = new List<GrnLandedCostLine>();
}

public class GoodsReceiptLine : CompanyEntity
{
    public int GoodsReceiptNoteId { get; set; }
    public GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
    public int? PurchaseOrderLineId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    /// <summary>JSON array of serial numbers captured at receive.</summary>
    public string? SerialNumbersJson { get; set; }
    /// <summary>Putaway bin; when null at post, warehouse receiving-default location is used.</summary>
    public int? WarehouseLocationId { get; set; }
    public WarehouseLocation? WarehouseLocation { get; set; }
}

public class GrnLandedCostLine : CompanyEntity
{
    public int GoodsReceiptNoteId { get; set; }
    public GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
    public string CostType { get; set; } = "Other"; // Freight, Duty, Other
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class PurchaseInvoice : CompanyEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public int? GoodsReceiptNoteId { get; set; }
    public GoodsReceiptNote? GoodsReceiptNote { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public bool ThreeWayMatched { get; set; }
    public string? MatchNotes { get; set; }
    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();
}

public class PurchaseInvoiceLine : CompanyEntity
{
    public int PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public class CycleCount : CompanyEntity
{
    public string CountNumber { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    /// <summary>When set, count sheet is scoped to this bin; system qty comes from location balance.</summary>
    public int? WarehouseLocationId { get; set; }
    public WarehouseLocation? WarehouseLocation { get; set; }
    public DateTime CountDate { get; set; } = DateTime.UtcNow;
    public CycleCountStatus Status { get; set; } = CycleCountStatus.Draft;
    public string? Notes { get; set; }
    public ICollection<CycleCountLine> Lines { get; set; } = new List<CycleCountLine>();
}

public class CycleCountLine : CompanyEntity
{
    public int CycleCountId { get; set; }
    public CycleCount CycleCount { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int? WarehouseLocationId { get; set; }
    public WarehouseLocation? WarehouseLocation { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal Variance => CountedQuantity - SystemQuantity;
}

// —— M3 O2C ——

public enum QuotationStatus { Draft = 0, Sent = 1, Accepted = 2, Expired = 3, Cancelled = 4 }
public enum DeliveryStatus { Draft = 0, Shipped = 1, Delivered = 2, Cancelled = 3 }

public class SalesQuotation : CompanyEntity
{
    public string QuotationNumber { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime QuotationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ValidUntil { get; set; }
    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public ICollection<SalesQuotationLine> Lines { get; set; } = new List<SalesQuotationLine>();
}

public class SalesQuotationLine : CompanyEntity
{
    public int SalesQuotationId { get; set; }
    public SalesQuotation SalesQuotation { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class DeliveryNote : CompanyEntity
{
    public string DeliveryNumber { get; set; } = string.Empty;
    public int? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public DateTime DeliveryDate { get; set; } = DateTime.UtcNow;
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Draft;
    /// <summary>Carrier / courier name (Program C2 — thin delivery tracking).</summary>
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? EtaUtc { get; set; }
    public ICollection<DeliveryNoteLine> Lines { get; set; } = new List<DeliveryNoteLine>();
}

public class DeliveryNoteLine : CompanyEntity
{
    public int DeliveryNoteId { get; set; }
    public DeliveryNote DeliveryNote { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityShipped { get; set; }
    public int? FromLocationId { get; set; }
    public WarehouseLocation? FromLocation { get; set; }
    public bool IsPicked { get; set; }
}

public class PriceList : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "PKR";
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ValidTo { get; set; }
    public bool IsDefault { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<PriceListItem> Items { get; set; } = new List<PriceListItem>();
}

public class PriceListItem : CompanyEntity
{
    public int PriceListId { get; set; }
    public PriceList PriceList { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal MinQuantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

// —— M4 Master data depth ——

public class ProductKit : CompanyEntity
{
    public int ParentProductId { get; set; }
    public Product ParentProduct { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public ICollection<ProductKitComponent> Components { get; set; } = new List<ProductKitComponent>();
}

public class ProductKitComponent : CompanyEntity
{
    public int ProductKitId { get; set; }
    public ProductKit ProductKit { get; set; } = null!;
    public int ComponentProductId { get; set; }
    public Product ComponentProduct { get; set; } = null!;
    public decimal Quantity { get; set; } = 1;
}

public class ProductSupersession : CompanyEntity
{
    public int OldProductId { get; set; }
    public Product OldProduct { get; set; } = null!;
    public int NewProductId { get; set; }
    public Product NewProduct { get; set; } = null!;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public string? Notes { get; set; }
}
