using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasIndex(s => s.Name);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Company).HasMaxLength(200);
        builder.Property(s => s.Email).HasMaxLength(100);
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Ntn).HasMaxLength(20);
        builder.Property(s => s.Strn).HasMaxLength(20);
        builder.Property(s => s.Balance).HasPrecision(18, 2);
    }
}

public class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> builder)
    {
        builder.ToTable("SupplierPayments");
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Reference).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(500);
        builder.Property(p => p.WithholdingTaxRate).HasPrecision(5, 2);
        builder.Property(p => p.WithholdingTaxAmount).HasPrecision(18, 2);

        builder.HasOne(p => p.Supplier).WithMany(s => s.Payments).HasForeignKey(p => p.SupplierId);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasIndex(c => c.Name);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.Email).HasMaxLength(100);
        builder.Property(c => c.NtnCnic).HasMaxLength(20);
        builder.Property(c => c.Province).HasMaxLength(50);
        builder.Property(c => c.CreditLimit).HasPrecision(18, 2);
        builder.Property(c => c.Balance).HasPrecision(18, 2);
        builder.Property(c => c.CommissionPercent).HasPrecision(5, 2);
    }
}
