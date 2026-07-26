using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.SubTotal).HasPrecision(18, 2);
        builder.Property(o => o.TaxAmount).HasPrecision(18, 2);
        builder.Property(o => o.DiscountAmount).HasPrecision(18, 2);
        builder.Property(o => o.GrandTotal).HasPrecision(18, 2);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.SupplierBackorderNotes).HasMaxLength(1000);

        builder.HasOne(o => o.Supplier).WithMany(s => s.PurchaseOrders).HasForeignKey(o => o.SupplierId);
        builder.HasOne(o => o.Warehouse).WithMany().HasForeignKey(o => o.WarehouseId);
        builder.HasOne(o => o.PurchaseRequisition).WithMany().HasForeignKey(o => o.PurchaseRequisitionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines");
        builder.Property(l => l.QuantityOrdered).HasPrecision(18, 3);
        builder.Property(l => l.QuantityReceived).HasPrecision(18, 3);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.TaxRate).HasPrecision(5, 2);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 2);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);

        builder.HasOne(l => l.PurchaseOrder).WithMany(o => o.Lines).HasForeignKey(l => l.PurchaseOrderId);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
    }
}

public class PurchaseOrderAttachmentConfiguration : IEntityTypeConfiguration<PurchaseOrderAttachment>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderAttachment> builder)
    {
        builder.ToTable("PurchaseOrderAttachments");
        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.FilePath).HasMaxLength(500).IsRequired();

        builder.HasOne(a => a.PurchaseOrder).WithMany(o => o.Attachments).HasForeignKey(a => a.PurchaseOrderId);
    }
}

public class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> builder)
    {
        builder.ToTable("PurchaseReturns");
        builder.HasIndex(r => r.ReturnNumber).IsUnique();
        builder.Property(r => r.ReturnNumber).HasMaxLength(30).IsRequired();
        builder.Property(r => r.GrandTotal).HasPrecision(18, 2);
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.ReasonCode).HasMaxLength(50).IsRequired();

        builder.HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId);
        builder.HasOne(r => r.Warehouse).WithMany().HasForeignKey(r => r.WarehouseId);
        builder.HasOne(r => r.PurchaseOrder).WithMany().HasForeignKey(r => r.PurchaseOrderId);
    }
}

public class PurchaseRequisitionConfiguration : IEntityTypeConfiguration<PurchaseRequisition>
{
    public void Configure(EntityTypeBuilder<PurchaseRequisition> builder)
    {
        builder.ToTable("PurchaseRequisitions");
        builder.HasIndex(r => r.RequisitionNumber).IsUnique();
        builder.Property(r => r.RequisitionNumber).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.RequestedBy).HasMaxLength(100);
        builder.Property(r => r.ApprovedBy).HasMaxLength(100);
        builder.Property(r => r.RejectionReason).HasMaxLength(500);

        builder.HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId);
        builder.HasOne(r => r.Warehouse).WithMany().HasForeignKey(r => r.WarehouseId);
    }
}

public class PurchaseRequisitionLineConfiguration : IEntityTypeConfiguration<PurchaseRequisitionLine>
{
    public void Configure(EntityTypeBuilder<PurchaseRequisitionLine> builder)
    {
        builder.ToTable("PurchaseRequisitionLines");
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.SuggestedUnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.PurchaseRequisition).WithMany(r => r.Lines).HasForeignKey(l => l.PurchaseRequisitionId);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
    }
}

public class PurchaseReturnLineConfiguration : IEntityTypeConfiguration<PurchaseReturnLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnLine> builder)
    {
        builder.ToTable("PurchaseReturnLines");
        builder.Property(l => l.Quantity).HasPrecision(18, 3);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.LineTotal).HasPrecision(18, 2);

        builder.HasOne(l => l.PurchaseReturn).WithMany(r => r.Lines).HasForeignKey(l => l.PurchaseReturnId);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
    }
}
