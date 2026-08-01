using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasIndex(p => new { p.CompanyId, p.Sku }).IsUnique();
        builder.HasIndex(p => new { p.CompanyId, p.Barcode }).HasFilter("[Barcode] IS NOT NULL");
        // Phase 12: OEM / part equality hot path for POS barcode-scanner latency budget
        builder.HasIndex(p => new { p.CompanyId, p.OemNumber }).HasFilter("[OemNumber] IS NOT NULL");
        builder.HasIndex(p => new { p.CompanyId, p.PartNumber }).HasFilter("[PartNumber] IS NOT NULL");
        builder.HasIndex(p => p.Name);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Barcode).HasMaxLength(50);
        builder.Property(p => p.OemNumber).HasMaxLength(100);
        builder.Property(p => p.PartNumber).HasMaxLength(100);
        builder.Property(p => p.Unit).HasMaxLength(20).IsRequired();
        builder.Property(p => p.PurchasePrice).HasPrecision(18, 2);
        builder.Property(p => p.SalePrice).HasPrecision(18, 2);
        builder.Property(p => p.CostPrice).HasPrecision(18, 2);
        builder.Property(p => p.TaxRatePercent).HasPrecision(5, 2);
        builder.Property(p => p.HsCode).HasMaxLength(20);

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasIndex(c => c.Name);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.Icon).HasMaxLength(50);

        builder.HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasIndex(b => new { b.CompanyId, b.Name }).IsUnique();
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.Property(b => b.LogoUrl).HasMaxLength(500);
    }
}

public class WarehouseLocationConfiguration : IEntityTypeConfiguration<WarehouseLocation>
{
    public void Configure(EntityTypeBuilder<WarehouseLocation> builder)
    {
        builder.ToTable("WarehouseLocations");
        builder.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasOne(x => x.Warehouse).WithMany(w => w.Locations).HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasIndex(w => w.Name);
        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Address).HasMaxLength(300);
        builder.Property(w => w.City).HasMaxLength(100);
        builder.Property(w => w.ContactPerson).HasMaxLength(100);
        builder.Property(w => w.PhoneNumber).HasMaxLength(30);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.Property(i => i.FilePath).HasMaxLength(500).IsRequired();

        builder.HasOne(i => i.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductVehicleCompatibilityConfiguration : IEntityTypeConfiguration<ProductVehicleCompatibility>
{
    public void Configure(EntityTypeBuilder<ProductVehicleCompatibility> builder)
    {
        builder.ToTable("ProductVehicleCompatibilities");
        builder.Property(v => v.Make).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(50).IsRequired();

        builder.HasOne(v => v.Product)
            .WithMany(p => p.VehicleCompatibilities)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Phase 19: fitment make/model filter + options
        builder.HasIndex(v => new { v.Make, v.Model });
    }
}
