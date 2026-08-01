using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.SubTotal).HasPrecision(18, 2);
        builder.Property(o => o.TaxAmount).HasPrecision(18, 2);
        builder.Property(o => o.DiscountAmount).HasPrecision(18, 2);
        builder.Property(o => o.GrandTotal).HasPrecision(18, 2);
        builder.Property(o => o.Notes).HasMaxLength(1000);

        builder.HasOne(o => o.Customer).WithMany(c => c.SalesOrders).HasForeignKey(o => o.CustomerId);
    }
}

public class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("SalesOrderLines");
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.TaxRate).HasPrecision(5, 2);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);

        builder.HasOne(l => l.SalesOrder).WithMany(o => o.Lines).HasForeignKey(l => l.SalesOrderId);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
    }
}

public class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("SalesInvoices");
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasIndex(i => i.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.Property(i => i.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(i => i.PosReference).HasMaxLength(50);
        builder.Property(i => i.IdempotencyKey).HasMaxLength(128);
        builder.Property(i => i.SubTotal).HasPrecision(18, 2);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 2);
        builder.Property(i => i.DiscountAmount).HasPrecision(18, 2);
        builder.Property(i => i.GrandTotal).HasPrecision(18, 2);
        builder.Property(i => i.ChangeDue).HasPrecision(18, 2);
        builder.Property(i => i.BuyerName).HasMaxLength(200);
        builder.Property(i => i.BuyerNtnCnic).HasMaxLength(20);
        builder.Property(i => i.BuyerProvince).HasMaxLength(50);
        builder.Property(i => i.BuyerRegistrationType).HasMaxLength(30);

        builder.HasOne(i => i.Customer).WithMany(c => c.SalesInvoices).HasForeignKey(i => i.CustomerId);
        builder.HasOne(i => i.SalesOrder).WithMany().HasForeignKey(i => i.SalesOrderId);
        builder.HasOne(i => i.Warehouse).WithMany().HasForeignKey(i => i.WarehouseId);
        builder.HasOne(i => i.CashierShift).WithMany().HasForeignKey(i => i.CashierShiftId).OnDelete(DeleteBehavior.SetNull);

        // Phase 19: day-range sales / dashboard / reports
        builder.HasIndex(i => new { i.InvoiceDate, i.WarehouseId });
    }
}

public class SalesInvoiceLineConfiguration : IEntityTypeConfiguration<SalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceLine> builder)
    {
        builder.ToTable("SalesInvoiceLines");
        builder.Property(l => l.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Sku).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.TaxRate).HasPrecision(5, 2);
        builder.Property(l => l.TaxAmount).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);
        builder.Property(l => l.HsCode).HasMaxLength(20);
        builder.Property(l => l.UnitOfMeasure).HasMaxLength(20);

        builder.HasOne(l => l.SalesInvoice).WithMany(i => i.Lines).HasForeignKey(l => l.SalesInvoiceId);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.PaymentMethod).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(100);

        builder.HasOne(p => p.SalesInvoice).WithMany(i => i.Payments).HasForeignKey(p => p.SalesInvoiceId);
    }
}

public class FbrSubmissionConfiguration : IEntityTypeConfiguration<FbrSubmission>
{
    public void Configure(EntityTypeBuilder<FbrSubmission> builder)
    {
        builder.ToTable("FbrSubmissions");
        builder.Property(f => f.FbrInvoiceNumber).HasMaxLength(50);
        builder.Property(f => f.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(f => f.SalesInvoice)
            .WithOne(i => i.FbrSubmission)
            .HasForeignKey<FbrSubmission>(f => f.SalesInvoiceId);
    }
}

public class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("SalesReturns");
        builder.HasIndex(r => r.ReturnNumber).IsUnique();
        builder.Property(r => r.ReturnNumber).HasMaxLength(30).IsRequired();
        builder.Property(r => r.GrandTotal).HasPrecision(18, 2);
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.ReasonCode).HasMaxLength(50).IsRequired();

        builder.HasOne(r => r.SalesInvoice).WithMany().HasForeignKey(r => r.SalesInvoiceId);
        builder.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId);

        // Phase 19: day sales returns filter
        builder.HasIndex(r => r.ReturnDate);
    }
}

public class SalesReturnLineConfiguration : IEntityTypeConfiguration<SalesReturnLine>
{
    public void Configure(EntityTypeBuilder<SalesReturnLine> builder)
    {
        builder.ToTable("SalesReturnLines");
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);

        builder.HasOne(l => l.SalesReturn).WithMany(r => r.Lines).HasForeignKey(l => l.SalesReturnId);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
    }
}

public class HeldSaleConfiguration : IEntityTypeConfiguration<HeldSale>
{
    public void Configure(EntityTypeBuilder<HeldSale> builder)
    {
        builder.ToTable("HeldSales");
        builder.HasIndex(h => h.HoldNumber).IsUnique();
        builder.Property(h => h.HoldNumber).HasMaxLength(30).IsRequired();
        builder.Property(h => h.UserName).HasMaxLength(100);
        builder.Property(h => h.BuyerName).HasMaxLength(200);
        builder.Property(h => h.Notes).HasMaxLength(1000);
        builder.HasOne(h => h.Warehouse).WithMany().HasForeignKey(h => h.WarehouseId);
        builder.HasOne(h => h.Customer).WithMany().HasForeignKey(h => h.CustomerId);
    }
}

public class HeldSaleLineConfiguration : IEntityTypeConfiguration<HeldSaleLine>
{
    public void Configure(EntityTypeBuilder<HeldSaleLine> builder)
    {
        builder.ToTable("HeldSaleLines");
        builder.Property(l => l.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.UnitPriceOverride).HasPrecision(18, 4);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 2);
        builder.HasOne(l => l.HeldSale).WithMany(h => h.Lines).HasForeignKey(l => l.HeldSaleId);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
    }
}

public class CashierShiftConfiguration : IEntityTypeConfiguration<CashierShift>
{
    public void Configure(EntityTypeBuilder<CashierShift> builder)
    {
        builder.ToTable("CashierShifts");
        builder.HasIndex(s => s.ShiftNumber).IsUnique();
        builder.Property(s => s.ShiftNumber).HasMaxLength(30).IsRequired();
        builder.Property(s => s.UserName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.OpeningFloat).HasPrecision(18, 2);
        builder.Property(s => s.ClosingFloat).HasPrecision(18, 2);
        builder.Property(s => s.DeclaredClosingCash).HasPrecision(18, 2);
        builder.Property(s => s.ExpectedCash).HasPrecision(18, 2);
        builder.Property(s => s.CashVariance).HasPrecision(18, 2);
        builder.Property(s => s.Notes).HasMaxLength(1000);
        builder.HasOne(s => s.Warehouse).WithMany().HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.Till).WithMany().HasForeignKey(s => s.TillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.UserId, s.Status });
        builder.HasIndex(s => new { s.TillId, s.Status });
    }
}

public class TillConfiguration : IEntityTypeConfiguration<Till>
{
    public void Configure(EntityTypeBuilder<Till> builder)
    {
        builder.ToTable("Tills");
        builder.Property(t => t.Code).HasMaxLength(30).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => new { t.BranchId, t.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasOne(t => t.Branch).WithMany().HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Warehouse).WithMany().HasForeignKey(t => t.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SafeDropConfiguration : IEntityTypeConfiguration<SafeDrop>
{
    public void Configure(EntityTypeBuilder<SafeDrop> builder)
    {
        builder.ToTable("SafeDrops");
        builder.Property(d => d.Amount).HasPrecision(18, 2);
        builder.Property(d => d.Notes).HasMaxLength(500);
        builder.Property(d => d.CreatedByUserName).HasMaxLength(100);
        builder.HasOne(d => d.CashierShift).WithMany(s => s.SafeDrops).HasForeignKey(d => d.CashierShiftId);
        builder.HasOne(d => d.Till).WithMany().HasForeignKey(d => d.TillId).OnDelete(DeleteBehavior.Restrict);
    }
}
