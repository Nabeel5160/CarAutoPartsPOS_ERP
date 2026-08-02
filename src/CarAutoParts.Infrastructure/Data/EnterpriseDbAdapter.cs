using CarAutoParts.Application.Enterprise;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Infrastructure.Data;

/// <summary>Adapts ApplicationDbContext to enterprise application services.</summary>
public sealed class EnterpriseDbAdapter : IEnterpriseDb
{
    private readonly ApplicationDbContext _db;

    public EnterpriseDbAdapter(ApplicationDbContext db) => _db = db;

    public DbSet<StockReservation> StockReservations => _db.StockReservations;
    public DbSet<GoodsReceiptNote> GoodsReceiptNotes => _db.GoodsReceiptNotes;
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => _db.GoodsReceiptLines;
    public DbSet<GrnLandedCostLine> GrnLandedCostLines => _db.GrnLandedCostLines;
    public DbSet<PurchaseRequisition> PurchaseRequisitions => _db.PurchaseRequisitions;
    public DbSet<PurchaseRequisitionLine> PurchaseRequisitionLines => _db.PurchaseRequisitionLines;
    public DbSet<CompanySettings> CompanySettings => _db.CompanySettings;
    public DbSet<SerialNumber> SerialNumbers => _db.SerialNumbers;
    public DbSet<SerialNumberHistory> SerialNumberHistories => _db.SerialNumberHistories;
    public DbSet<CycleCount> CycleCounts => _db.CycleCounts;
    public DbSet<CycleCountLine> CycleCountLines => _db.CycleCountLines;
    public DbSet<PurchaseInvoice> PurchaseInvoices => _db.PurchaseInvoices;
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => _db.PurchaseInvoiceLines;
    public DbSet<PurchaseOrder> PurchaseOrders => _db.PurchaseOrders;
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => _db.PurchaseOrderLines;
    public DbSet<SalesQuotation> SalesQuotations => _db.SalesQuotations;
    public DbSet<SalesQuotationLine> SalesQuotationLines => _db.SalesQuotationLines;
    public DbSet<SalesOrder> SalesOrders => _db.SalesOrders;
    public DbSet<SalesOrderLine> SalesOrderLines => _db.SalesOrderLines;
    public DbSet<DeliveryNote> DeliveryNotes => _db.DeliveryNotes;
    public DbSet<DeliveryNoteLine> DeliveryNoteLines => _db.DeliveryNoteLines;
    public DbSet<PriceList> PriceLists => _db.PriceLists;
    public DbSet<PriceListItem> PriceListItems => _db.PriceListItems;
    public DbSet<ProductKit> ProductKits => _db.ProductKits;
    public DbSet<ProductKitComponent> ProductKitComponents => _db.ProductKitComponents;
    public DbSet<ProductSupersession> ProductSupersessions => _db.ProductSupersessions;
    public DbSet<InventoryItem> InventoryItems => _db.InventoryItems;
    public DbSet<InventoryLocationBalance> InventoryLocationBalances => _db.InventoryLocationBalances;
    public DbSet<StockMovement> StockMovements => _db.StockMovements;
    public DbSet<Product> Products => _db.Products;
    public DbSet<Warehouse> Warehouses => _db.Warehouses;
    public DbSet<WarehouseLocation> WarehouseLocations => _db.WarehouseLocations;
    public DbSet<Customer> Customers => _db.Customers;
    public DbSet<Supplier> Suppliers => _db.Suppliers;
    public DbSet<NumberSequence> NumberSequences => _db.NumberSequences;
    public DbSet<JournalEntry> JournalEntries => _db.JournalEntries;
    public DbSet<JournalLine> JournalLines => _db.JournalLines;
    public DbSet<GlAccount> GlAccounts => _db.GlAccounts;
    public DbSet<AccountMapping> AccountMappings => _db.AccountMappings;
    public DbSet<AccountingPeriod> AccountingPeriods => _db.AccountingPeriods;
    public DbSet<SalesInvoice> SalesInvoices => _db.SalesInvoices;
    public DbSet<Payment> Payments => _db.Payments;
    public DbSet<SupplierPayment> SupplierPayments => _db.SupplierPayments;
    public DbSet<FbrSubmission> FbrSubmissions => _db.FbrSubmissions;
    public DbSet<OpeningBalanceBatch> OpeningBalanceBatches => _db.OpeningBalanceBatches;
    public DbSet<OpeningBalanceLine> OpeningBalanceLines => _db.OpeningBalanceLines;
    public DbSet<BankStatement> BankStatements => _db.BankStatements;
    public DbSet<BankStatementLine> BankStatementLines => _db.BankStatementLines;
    public DbSet<SalesReturn> SalesReturns => _db.SalesReturns;
    public DbSet<PurchaseReturn> PurchaseReturns => _db.PurchaseReturns;
    public DbSet<CreditNoteApplication> CreditNoteApplications => _db.CreditNoteApplications;
    public DbSet<PurchaseCreditNoteApplication> PurchaseCreditNoteApplications => _db.PurchaseCreditNoteApplications;
    public DbSet<PurchaseRfq> PurchaseRfqs => _db.PurchaseRfqs;
    public DbSet<PurchaseRfqLine> PurchaseRfqLines => _db.PurchaseRfqLines;
    public DbSet<VendorQuote> VendorQuotes => _db.VendorQuotes;
    public DbSet<VendorQuoteLine> VendorQuoteLines => _db.VendorQuoteLines;
    public DbSet<SalesTarget> SalesTargets => _db.SalesTargets;
    public DbSet<AppUser> Users => _db.Users;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
