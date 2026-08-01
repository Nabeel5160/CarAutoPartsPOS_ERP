using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        builder.HasIndex(i => new { i.ProductId, i.WarehouseId }).IsUnique();
        builder.Property(i => i.QuantityOnHand).HasPrecision(18, 3);
        builder.Property(i => i.ReservedQuantity).HasPrecision(18, 3);
        builder.Property(i => i.AverageCost).HasPrecision(18, 4);

        builder.HasOne(i => i.Product).WithMany(p => p.InventoryItems).HasForeignKey(i => i.ProductId);
        builder.HasOne(i => i.Warehouse).WithMany(w => w.InventoryItems).HasForeignKey(i => i.WarehouseId);
    }
}

public class InventoryLocationBalanceConfiguration : IEntityTypeConfiguration<InventoryLocationBalance>
{
    public void Configure(EntityTypeBuilder<InventoryLocationBalance> builder)
    {
        builder.ToTable("InventoryLocationBalances");
        builder.HasIndex(b => new { b.InventoryItemId, b.WarehouseLocationId }).IsUnique();
        builder.Property(b => b.QuantityOnHand).HasPrecision(18, 3);
        builder.HasOne(b => b.InventoryItem).WithMany(i => i.LocationBalances).HasForeignKey(b => b.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(b => b.WarehouseLocation).WithMany(l => l.Balances).HasForeignKey(b => b.WarehouseLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasIndex(m => m.MovementDate);
        builder.Property(m => m.Quantity).HasPrecision(18, 3);
        builder.Property(m => m.UnitCost).HasPrecision(18, 4);
        builder.Property(m => m.ReferenceType).HasMaxLength(50);
        builder.Property(m => m.Notes).HasMaxLength(500);

        builder.HasOne(m => m.InventoryItem).WithMany(i => i.Movements).HasForeignKey(m => m.InventoryItemId);
    }
}

public class StockBatchConfiguration : IEntityTypeConfiguration<StockBatch>
{
    public void Configure(EntityTypeBuilder<StockBatch> builder)
    {
        builder.ToTable("StockBatches");
        builder.Property(b => b.BatchNumber).HasMaxLength(50).IsRequired();
        builder.Property(b => b.QuantityRemaining).HasPrecision(18, 3);
        builder.Property(b => b.UnitCost).HasPrecision(18, 4);

        builder.HasOne(b => b.InventoryItem).WithMany(i => i.Batches).HasForeignKey(b => b.InventoryItemId);
    }
}

public class SerialNumberConfiguration : IEntityTypeConfiguration<SerialNumber>
{
    public void Configure(EntityTypeBuilder<SerialNumber> builder)
    {
        builder.ToTable("SerialNumbers");
        builder.HasIndex(s => s.Serial).IsUnique();
        builder.Property(s => s.Serial).HasMaxLength(100).IsRequired();

        builder.HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId);
        builder.HasOne(s => s.CurrentWarehouse).WithMany().HasForeignKey(s => s.CurrentWarehouseId);
    }
}

public class SerialNumberHistoryConfiguration : IEntityTypeConfiguration<SerialNumberHistory>
{
    public void Configure(EntityTypeBuilder<SerialNumberHistory> builder)
    {
        builder.ToTable("SerialNumberHistories");
        builder.Property(h => h.Action).HasMaxLength(50).IsRequired();
        builder.Property(h => h.ReferenceType).HasMaxLength(50);
        builder.Property(h => h.Notes).HasMaxLength(500);

        builder.HasOne(h => h.SerialNumber).WithMany(s => s.History).HasForeignKey(h => h.SerialNumberId);
    }
}

public class InventoryTransferConfiguration : IEntityTypeConfiguration<InventoryTransfer>
{
    public void Configure(EntityTypeBuilder<InventoryTransfer> builder)
    {
        builder.ToTable("InventoryTransfers");
        builder.HasIndex(t => t.TransferNumber).IsUnique();
        builder.Property(t => t.TransferNumber).HasMaxLength(30).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(500);
        builder.Property(t => t.ApprovedBy).HasMaxLength(100);

        builder.HasOne(t => t.FromWarehouse).WithMany().HasForeignKey(t => t.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.ToWarehouse).WithMany().HasForeignKey(t => t.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryTransferLineConfiguration : IEntityTypeConfiguration<InventoryTransferLine>
{
    public void Configure(EntityTypeBuilder<InventoryTransferLine> builder)
    {
        builder.ToTable("InventoryTransferLines");
        builder.Property(l => l.Quantity).HasPrecision(18, 3);

        builder.HasOne(l => l.InventoryTransfer).WithMany(t => t.Lines).HasForeignKey(l => l.InventoryTransferId);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
        builder.HasOne(l => l.FromLocation).WithMany().HasForeignKey(l => l.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.ToLocation).WithMany().HasForeignKey(l => l.ToLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
