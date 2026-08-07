using CarAutoParts.Domain.Common;

namespace CarAutoParts.Domain.Entities;

/// <summary>Commission accrual snapshot when a sales invoice is posted (Program C2).</summary>
public class SalesCommission : CompanyEntity
{
    public int SalesInvoiceId { get; set; }
    public SalesInvoice SalesInvoice { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int? UserId { get; set; }
    public AppUser? User { get; set; }
    public decimal CommissionPercent { get; set; }
    public decimal CommissionAmount { get; set; }
    /// <summary>Invoice grand total at post — used for sales-target attainment.</summary>
    public decimal InvoiceAmount { get; set; }
    public DateTime InvoiceDate { get; set; }
}
