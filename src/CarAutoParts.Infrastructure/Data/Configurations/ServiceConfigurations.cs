using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class ServiceTicketConfiguration : IEntityTypeConfiguration<ServiceTicket>
{
    public void Configure(EntityTypeBuilder<ServiceTicket> builder)
    {
        builder.ToTable("ServiceTickets");
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.HasIndex(x => new { x.CompanyId, x.CustomerId });
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.WarrantyReference).HasMaxLength(100);
        builder.Property(x => x.WarrantyDecisionNotes).HasMaxLength(1000);
        builder.Property(x => x.WarrantyDecidedBy).HasMaxLength(100);
        builder.Property(x => x.WarrantyEvidenceNotes).HasMaxLength(2000);
        builder.Property(x => x.ReplacementQuantity).HasPrecision(18, 3);
        builder.Property(x => x.AmcReference).HasMaxLength(100);
        builder.HasIndex(x => new { x.CompanyId, x.IsWarrantyClaim, x.WarrantyClaimStatus });
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.ResolutionNotes).HasMaxLength(2000);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.SlaPolicy).WithMany().HasForeignKey(x => x.SlaPolicyId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.AmcContract).WithMany().HasForeignKey(x => x.AmcContractId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.WarrantySalesInvoice).WithMany().HasForeignKey(x => x.WarrantySalesInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ReplacementProduct).WithMany().HasForeignKey(x => x.ReplacementProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AmcContractConfiguration : IEntityTypeConfiguration<AmcContract>
{
    public void Configure(EntityTypeBuilder<AmcContract> builder)
    {
        builder.ToTable("AmcContracts");
        builder.HasIndex(x => new { x.CompanyId, x.ContractNumber }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.CustomerId, x.Status });
        builder.Property(x => x.ContractNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CoverageNotes).HasMaxLength(2000);
        builder.Property(x => x.AnnualAmount).HasPrecision(18, 2);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ServiceVisitConfiguration : IEntityTypeConfiguration<ServiceVisit>
{
    public void Configure(EntityTypeBuilder<ServiceVisit> builder)
    {
        builder.ToTable("ServiceVisits");
        builder.HasIndex(x => new { x.CompanyId, x.AssignedToUserId, x.ScheduledAt });
        builder.HasIndex(x => new { x.ServiceTicketId, x.Status });
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.ServiceTicket).WithMany(t => t.Visits).HasForeignKey(x => x.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ServiceTicketPartConfiguration : IEntityTypeConfiguration<ServiceTicketPart>
{
    public void Configure(EntityTypeBuilder<ServiceTicketPart> builder)
    {
        builder.ToTable("ServiceTicketParts");
        builder.HasIndex(x => x.ServiceTicketId);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.HasOne(x => x.ServiceTicket).WithMany(t => t.Parts).HasForeignKey(x => x.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}
