using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

/// <summary>Request for quotation sent to one or more suppliers (Program B — thin RFQ→compare→PO flow).</summary>
public class PurchaseRfq : CompanyEntity
{
    public string RfqNumber { get; set; } = string.Empty;
    public PurchaseRfqStatus Status { get; set; } = PurchaseRfqStatus.Draft;
    public DateTime RfqDate { get; set; } = DateTime.UtcNow;
    public DateTime? ResponseDeadline { get; set; }
    public string? Notes { get; set; }
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public ICollection<PurchaseRfqLine> Lines { get; set; } = new List<PurchaseRfqLine>();
    public ICollection<VendorQuote> VendorQuotes { get; set; } = new List<VendorQuote>();
}

/// <summary>Line item (product + desired quantity) requested on an RFQ.</summary>
public class PurchaseRfqLine : CompanyEntity
{
    public int PurchaseRfqId { get; set; }
    public PurchaseRfq PurchaseRfq { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A supplier's quote submitted in response to an RFQ.</summary>
public class VendorQuote : CompanyEntity
{
    public int PurchaseRfqId { get; set; }
    public PurchaseRfq PurchaseRfq { get; set; } = null!;
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public VendorQuoteStatus Status { get; set; } = VendorQuoteStatus.Draft;
    public DateTime QuoteDate { get; set; } = DateTime.UtcNow;
    public DateTime? ValidUntil { get; set; }
    public string? Notes { get; set; }
    public ICollection<VendorQuoteLine> Lines { get; set; } = new List<VendorQuoteLine>();
}

/// <summary>Per-product price/lead-time offered by a supplier on a <see cref="VendorQuote"/>.</summary>
public class VendorQuoteLine : CompanyEntity
{
    public int VendorQuoteId { get; set; }
    public VendorQuote VendorQuote { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? Notes { get; set; }
}
