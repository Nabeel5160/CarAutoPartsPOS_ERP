using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

public class SalesOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
}

public class SalesOrderLine : BaseEntity
{
    public int SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class SalesInvoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? PosReference { get; set; }
    public string? IdempotencyKey { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? BuyerName { get; set; }
    public string? BuyerNtnCnic { get; set; }
    public string? BuyerProvince { get; set; }
    public string? BuyerAddress { get; set; }
    public string? BuyerRegistrationType { get; set; }
    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public decimal ChangeDue { get; set; }
    public int? CashierShiftId { get; set; }
    public CashierShift? CashierShift { get; set; }
    public ICollection<SalesInvoiceLine> Lines { get; set; } = new List<SalesInvoiceLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public FbrSubmission? FbrSubmission { get; set; }
}

public class SalesInvoiceLine : BaseEntity
{
    public int SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? HsCode { get; set; }
    public string? UnitOfMeasure { get; set; }
}

public class Payment : BaseEntity
{
    public int SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string PaymentMethod { get; set; } = "Cash";
    public string? Reference { get; set; }
}

public class FbrSubmission : BaseEntity
{
    public int SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public string? FbrInvoiceNumber { get; set; }
    public FbrSubmissionStatus Status { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public class SalesReturn : BaseEntity
{
    public string ReturnNumber { get; set; } = string.Empty;
    public int? SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ReturnStatus Status { get; set; } = ReturnStatus.Draft;
    public ReturnType ReturnType { get; set; }
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public ICollection<SalesReturnLine> Lines { get; set; } = new List<SalesReturnLine>();
}

public class SalesReturnLine : BaseEntity
{
    public int SalesReturnId { get; set; }
    public SalesReturn SalesReturn { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class HeldSale : BaseEntity
{
    public string HoldNumber { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? BuyerName { get; set; }
    public string? Notes { get; set; }
    public HeldSaleStatus Status { get; set; } = HeldSaleStatus.Held;
    public DateTime HeldAt { get; set; } = DateTime.UtcNow;
    public DateTime? RecalledAt { get; set; }
    public ICollection<HeldSaleLine> Lines { get; set; } = new List<HeldSaleLine>();
}

public class HeldSaleLine : BaseEntity
{
    public int HeldSaleId { get; set; }
    public HeldSale HeldSale { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? UnitPriceOverride { get; set; }
    public decimal DiscountAmount { get; set; }
}

public class CashierShift : BaseEntity
{
    public string ShiftNumber { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public CashierShiftStatus Status { get; set; } = CashierShiftStatus.Open;
    public decimal OpeningFloat { get; set; }
    public decimal ClosingFloat { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
}
