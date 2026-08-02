using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Ntn { get; set; }
    public string? Strn { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public ICollection<SupplierPayment> Payments { get; set; } = new List<SupplierPayment>();
}

public class SupplierPayment : BaseEntity
{
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    /// <summary>Withholding tax rate (%) applied under PK tax law, e.g. Section 153 supply payments.</summary>
    public decimal WithholdingTaxRate { get; set; }

    /// <summary>Withheld amount deducted from the gross payment and remitted to the tax authority.</summary>
    public decimal WithholdingTaxAmount { get; set; }
}

public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; } = CustomerType.WalkIn;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? NtnCnic { get; set; }
    public string? Province { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Salesperson commission % applied on invoices linked to this customer (Program B — sales thin).</summary>
    public decimal CommissionPercent { get; set; }

    public ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
    public ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
}
