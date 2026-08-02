using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class PurchaseRfqConfiguration : IEntityTypeConfiguration<PurchaseRfq>
{
    public void Configure(EntityTypeBuilder<PurchaseRfq> builder)
    {
        builder.ToTable("PurchaseRfqs");
        builder.HasIndex(x => new { x.CompanyId, x.RfqNumber }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.Property(x => x.RfqNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Lines).WithOne(l => l.PurchaseRfq).HasForeignKey(l => l.PurchaseRfqId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.VendorQuotes).WithOne(q => q.PurchaseRfq).HasForeignKey(q => q.PurchaseRfqId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseRfqLineConfiguration : IEntityTypeConfiguration<PurchaseRfqLine>
{
    public void Configure(EntityTypeBuilder<PurchaseRfqLine> builder)
    {
        builder.ToTable("PurchaseRfqLines");
        builder.HasIndex(x => x.PurchaseRfqId);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VendorQuoteConfiguration : IEntityTypeConfiguration<VendorQuote>
{
    public void Configure(EntityTypeBuilder<VendorQuote> builder)
    {
        builder.ToTable("VendorQuotes");
        builder.HasIndex(x => new { x.CompanyId, x.PurchaseRfqId });
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(l => l.VendorQuote).HasForeignKey(l => l.VendorQuoteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class VendorQuoteLineConfiguration : IEntityTypeConfiguration<VendorQuoteLine>
{
    public void Configure(EntityTypeBuilder<VendorQuoteLine> builder)
    {
        builder.ToTable("VendorQuoteLines");
        builder.HasIndex(x => x.VendorQuoteId);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
