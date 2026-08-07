using System.Linq.Expressions;
using System.Reflection;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentCompanyContext _companyContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentCompanyContext? companyContext = null)
        : base(options)
    {
        _companyContext = companyContext ?? NullCurrentCompanyContext.Instance;
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVehicleCompatibility> ProductVehicleCompatibilities => Set<ProductVehicleCompatibility>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryLocationBalance> InventoryLocationBalances => Set<InventoryLocationBalance>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockBatch> StockBatches => Set<StockBatch>();
    public DbSet<SerialNumber> SerialNumbers => Set<SerialNumber>();
    public DbSet<SerialNumberHistory> SerialNumberHistories => Set<SerialNumberHistory>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderAttachment> PurchaseOrderAttachments => Set<PurchaseOrderAttachment>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnLine> PurchaseReturnLines => Set<PurchaseReturnLine>();
    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<PurchaseRequisitionLine> PurchaseRequisitionLines => Set<PurchaseRequisitionLine>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<FbrSubmission> FbrSubmissions => Set<FbrSubmission>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnLine> SalesReturnLines => Set<SalesReturnLine>();
    public DbSet<HeldSale> HeldSales => Set<HeldSale>();
    public DbSet<HeldSaleLine> HeldSaleLines => Set<HeldSaleLine>();
    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();
    public DbSet<Till> Tills => Set<Till>();
    public DbSet<SafeDrop> SafeDrops => Set<SafeDrop>();
    public DbSet<InventoryTransfer> InventoryTransfers => Set<InventoryTransfer>();
    public DbSet<InventoryTransferLine> InventoryTransferLines => Set<InventoryTransferLine>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<AppConfigEntry> AppConfigEntries => Set<AppConfigEntry>();
    public DbSet<BackupHistory> BackupHistories => Set<BackupHistory>();

    // Platform / Finance
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<AccountMapping> AccountMappings => Set<AccountMapping>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<OpeningBalanceBatch> OpeningBalanceBatches => Set<OpeningBalanceBatch>();
    public DbSet<OpeningBalanceLine> OpeningBalanceLines => Set<OpeningBalanceLine>();
    public DbSet<BankStatement> BankStatements => Set<BankStatement>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();
    public DbSet<CreditNoteApplication> CreditNoteApplications => Set<CreditNoteApplication>();
    public DbSet<PurchaseCreditNoteApplication> PurchaseCreditNoteApplications => Set<PurchaseCreditNoteApplication>();

    // Enterprise ops (M2/M3)
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<GoodsReceiptNote> GoodsReceiptNotes => Set<GoodsReceiptNote>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<GrnLandedCostLine> GrnLandedCostLines => Set<GrnLandedCostLine>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<CycleCount> CycleCounts => Set<CycleCount>();
    public DbSet<CycleCountLine> CycleCountLines => Set<CycleCountLine>();
    public DbSet<SalesQuotation> SalesQuotations => Set<SalesQuotation>();
    public DbSet<SalesQuotationLine> SalesQuotationLines => Set<SalesQuotationLine>();
    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<DeliveryNoteLine> DeliveryNoteLines => Set<DeliveryNoteLine>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
    public DbSet<ProductKit> ProductKits => Set<ProductKit>();
    public DbSet<ProductKitComponent> ProductKitComponents => Set<ProductKitComponent>();
    public DbSet<ProductSupersession> ProductSupersessions => Set<ProductSupersession>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<CrmActivity> CrmActivities => Set<CrmActivity>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunityStageHistory> OpportunityStageHistories => Set<OpportunityStageHistory>();
    public DbSet<CrmAssignmentRule> CrmAssignmentRules => Set<CrmAssignmentRule>();
    public DbSet<CrmEmailTemplate> CrmEmailTemplates => Set<CrmEmailTemplate>();

    // Program B — ops gaps (RFQ, sales targets)
    public DbSet<PurchaseRfq> PurchaseRfqs => Set<PurchaseRfq>();
    public DbSet<PurchaseRfqLine> PurchaseRfqLines => Set<PurchaseRfqLine>();
    public DbSet<VendorQuote> VendorQuotes => Set<VendorQuote>();
    public DbSet<VendorQuoteLine> VendorQuoteLines => Set<VendorQuoteLine>();
    public DbSet<SalesTarget> SalesTargets => Set<SalesTarget>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<SalesCommission> SalesCommissions => Set<SalesCommission>();

    // Program C1 — Service Light
    public DbSet<ServiceTicket> ServiceTickets => Set<ServiceTicket>();
    public DbSet<KbArticle> KbArticles => Set<KbArticle>();
    public DbSet<AmcContract> AmcContracts => Set<AmcContract>();
    public DbSet<ServiceVisit> ServiceVisits => Set<ServiceVisit>();
    public DbSet<ServiceTicketPart> ServiceTicketParts => Set<ServiceTicketPart>();

    // Program C2 — SLA
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<SlaPolicyRule> SlaPolicyRules => Set<SlaPolicyRule>();
    public DbSet<SlaTarget> SlaTargets => Set<SlaTarget>();
    public DbSet<SlaTimer> SlaTimers => Set<SlaTimer>();
    public DbSet<SlaEvent> SlaEvents => Set<SlaEvent>();
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ApplyRowVersionConvention(modelBuilder);
        ApplySoftDeleteAndCompanyFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ApplyRowVersionConvention(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(BaseEntity.RowVersion))
                .IsRowVersion();
        }
    }

    private void ApplySoftDeleteAndCompanyFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (!typeof(BaseEntity).IsAssignableFrom(clr))
                continue;

            if (typeof(CompanyEntity).IsAssignableFrom(clr))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetCompanyOwnedFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clr);
                method.Invoke(this, [modelBuilder]);
            }
            else
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(clr);
                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    private void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void SetCompanyOwnedFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : CompanyEntity
    {
        // Never dereference a nullable context — EF evaluates filter members at query time.
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (!_companyContext.CompanyId.HasValue || e.CompanyId == _companyContext.CompanyId));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        CollectOutboxFromAggregates();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void CollectOutboxFromAggregates()
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                OutboxMessages.Add(new OutboxMessage
                {
                    Type = domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
                    Payload = System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredAtUtc = domainEvent.OccurredAtUtc,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                });
            }
            aggregate.ClearDomainEvents();
        }
    }
}
