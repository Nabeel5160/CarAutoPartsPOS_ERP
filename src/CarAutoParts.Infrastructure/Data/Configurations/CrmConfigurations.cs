using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.HasIndex(x => new { x.CompanyId, x.Name });
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.Email).HasMaxLength(100);
        builder.Property(x => x.Source).HasMaxLength(80);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.LostReason).HasMaxLength(500);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ConvertedCustomer).WithMany().HasForeignKey(x => x.ConvertedCustomerId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CrmActivityConfiguration : IEntityTypeConfiguration<CrmActivity>
{
    public void Configure(EntityTypeBuilder<CrmActivity> builder)
    {
        builder.ToTable("CrmActivities");
        builder.HasIndex(x => new { x.CompanyId, x.DueAt });
        builder.HasIndex(x => new { x.CompanyId, x.AssignedToUserId });
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.AttachmentPath).HasMaxLength(500);
        builder.Property(x => x.AttachmentName).HasMaxLength(200);
        builder.HasOne(x => x.Lead).WithMany(l => l.Activities).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("Opportunities");
        builder.HasIndex(x => new { x.CompanyId, x.Stage });
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Value).HasPrecision(18, 2);
        builder.Property(x => x.LostReason).HasMaxLength(500);
        builder.Property(x => x.WinReason).HasMaxLength(500);
        builder.HasOne(x => x.Lead).WithMany(l => l.Opportunities).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Quotation).WithMany().HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class OpportunityStageHistoryConfiguration : IEntityTypeConfiguration<OpportunityStageHistory>
{
    public void Configure(EntityTypeBuilder<OpportunityStageHistory> builder)
    {
        builder.ToTable("OpportunityStageHistories");
        builder.HasIndex(x => new { x.CompanyId, x.OpportunityId, x.ChangedAt });
        builder.Property(x => x.ChangedBy).HasMaxLength(100);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne(x => x.Opportunity).WithMany(o => o.StageHistory).HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CrmAssignmentRuleConfiguration : IEntityTypeConfiguration<CrmAssignmentRule>
{
    public void Configure(EntityTypeBuilder<CrmAssignmentRule> builder)
    {
        builder.ToTable("CrmAssignmentRules");
        builder.Property(x => x.Source).HasMaxLength(80);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CrmEmailTemplateConfiguration : IEntityTypeConfiguration<CrmEmailTemplate>
{
    public void Configure(EntityTypeBuilder<CrmEmailTemplate> builder)
    {
        builder.ToTable("CrmEmailTemplates");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
    }
}
