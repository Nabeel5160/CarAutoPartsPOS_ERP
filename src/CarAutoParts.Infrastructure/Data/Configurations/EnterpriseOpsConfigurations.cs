using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.ProductId, x.WarehouseId, x.Status });
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.ReferenceType).HasMaxLength(64);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class GoodsReceiptNoteConfiguration : IEntityTypeConfiguration<GoodsReceiptNote>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptNote> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.GrnNumber }).IsUnique();
        builder.Property(x => x.GrnNumber).HasMaxLength(40);
        builder.Property(x => x.LandedCostAmount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.BatchNumber).HasMaxLength(64);
        builder.Property(x => x.SerialNumbersJson).HasMaxLength(4000);
        builder.HasOne(x => x.GoodsReceiptNote).WithMany(g => g.Lines).HasForeignKey(x => x.GoodsReceiptNoteId);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class GrnLandedCostLineConfiguration : IEntityTypeConfiguration<GrnLandedCostLine>
{
    public void Configure(EntityTypeBuilder<GrnLandedCostLine> builder)
    {
        builder.ToTable("GrnLandedCostLines");
        builder.Property(x => x.CostType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasOne(x => x.GoodsReceiptNote).WithMany(g => g.LandedCostLines).HasForeignKey(x => x.GoodsReceiptNoteId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.InvoiceNumber }).IsUnique();
        builder.Property(x => x.InvoiceNumber).HasMaxLength(40);
        builder.Property(x => x.SubTotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2);
        builder.Property(x => x.MatchNotes).HasMaxLength(2000);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId);
        builder.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId);
        builder.HasOne(x => x.GoodsReceiptNote).WithMany().HasForeignKey(x => x.GoodsReceiptNoteId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class PurchaseInvoiceLineConfiguration : IEntityTypeConfiguration<PurchaseInvoiceLine>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceLine> builder)
    {
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasOne(x => x.PurchaseInvoice).WithMany(i => i.Lines).HasForeignKey(x => x.PurchaseInvoiceId);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class CycleCountConfiguration : IEntityTypeConfiguration<CycleCount>
{
    public void Configure(EntityTypeBuilder<CycleCount> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.CountNumber }).IsUnique();
        builder.Property(x => x.CountNumber).HasMaxLength(40);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class CycleCountLineConfiguration : IEntityTypeConfiguration<CycleCountLine>
{
    public void Configure(EntityTypeBuilder<CycleCountLine> builder)
    {
        builder.Property(x => x.SystemQuantity).HasPrecision(18, 3);
        builder.Property(x => x.CountedQuantity).HasPrecision(18, 3);
        builder.Ignore(x => x.Variance);
        builder.HasOne(x => x.CycleCount).WithMany(c => c.Lines).HasForeignKey(x => x.CycleCountId);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class SalesQuotationConfiguration : IEntityTypeConfiguration<SalesQuotation>
{
    public void Configure(EntityTypeBuilder<SalesQuotation> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.QuotationNumber }).IsUnique();
        builder.Property(x => x.QuotationNumber).HasMaxLength(40);
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class SalesQuotationLineConfiguration : IEntityTypeConfiguration<SalesQuotationLine>
{
    public void Configure(EntityTypeBuilder<SalesQuotationLine> builder)
    {
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasOne(x => x.SalesQuotation).WithMany(q => q.Lines).HasForeignKey(x => x.SalesQuotationId);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class DeliveryNoteConfiguration : IEntityTypeConfiguration<DeliveryNote>
{
    public void Configure(EntityTypeBuilder<DeliveryNote> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.DeliveryNumber }).IsUnique();
        builder.Property(x => x.DeliveryNumber).HasMaxLength(40);
        builder.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class DeliveryNoteLineConfiguration : IEntityTypeConfiguration<DeliveryNoteLine>
{
    public void Configure(EntityTypeBuilder<DeliveryNoteLine> builder)
    {
        builder.Property(x => x.QuantityOrdered).HasPrecision(18, 3);
        builder.Property(x => x.QuantityShipped).HasPrecision(18, 3);
        builder.HasOne(x => x.DeliveryNote).WithMany(d => d.Lines).HasForeignKey(x => x.DeliveryNoteId);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.Name });
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class PriceListItemConfiguration : IEntityTypeConfiguration<PriceListItem>
{
    public void Configure(EntityTypeBuilder<PriceListItem> builder)
    {
        builder.HasIndex(x => new { x.PriceListId, x.ProductId, x.MinQuantity });
        builder.Property(x => x.MinQuantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.HasOne(x => x.PriceList).WithMany(p => p.Items).HasForeignKey(x => x.PriceListId);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class ProductKitConfiguration : IEntityTypeConfiguration<ProductKit>
{
    public void Configure(EntityTypeBuilder<ProductKit> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.ParentProductId, x.Name });
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.HasOne(x => x.ParentProduct).WithMany().HasForeignKey(x => x.ParentProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class ProductKitComponentConfiguration : IEntityTypeConfiguration<ProductKitComponent>
{
    public void Configure(EntityTypeBuilder<ProductKitComponent> builder)
    {
        builder.HasIndex(x => new { x.ProductKitId, x.ComponentProductId });
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.HasOne(x => x.ProductKit).WithMany(k => k.Components).HasForeignKey(x => x.ProductKitId);
        builder.HasOne(x => x.ComponentProduct).WithMany().HasForeignKey(x => x.ComponentProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class ProductSupersessionConfiguration : IEntityTypeConfiguration<ProductSupersession>
{
    public void Configure(EntityTypeBuilder<ProductSupersession> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.OldProductId, x.EffectiveFrom });
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasOne(x => x.OldProduct).WithMany().HasForeignKey(x => x.OldProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.NewProduct).WithMany().HasForeignKey(x => x.NewProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
