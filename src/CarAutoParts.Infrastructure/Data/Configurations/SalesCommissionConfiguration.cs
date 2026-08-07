using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class SalesCommissionConfiguration : IEntityTypeConfiguration<SalesCommission>
{
    public void Configure(EntityTypeBuilder<SalesCommission> builder)
    {
        builder.ToTable("SalesCommissions");
        builder.Property(x => x.CommissionPercent).HasPrecision(5, 2);
        builder.Property(x => x.CommissionAmount).HasPrecision(18, 2);
        builder.Property(x => x.InvoiceAmount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.CompanyId, x.SalesInvoiceId }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.UserId, x.InvoiceDate });
        builder.HasOne(x => x.SalesInvoice).WithMany().HasForeignKey(x => x.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
