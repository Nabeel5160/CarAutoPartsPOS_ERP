using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.ToTable("SlaPolicies");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.IsDefault });
        builder.HasIndex(x => new { x.CompanyId, x.AppliesToEntityType });
        builder.HasOne(x => x.EscalateToUser).WithMany().HasForeignKey(x => x.EscalateToUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Targets).WithOne(t => t.SlaPolicy!).HasForeignKey(t => t.SlaPolicyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Rules).WithOne(r => r.SlaPolicy!).HasForeignKey(r => r.SlaPolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SlaPolicyRuleConfiguration : IEntityTypeConfiguration<SlaPolicyRule>
{
    public void Configure(EntityTypeBuilder<SlaPolicyRule> builder)
    {
        builder.ToTable("SlaPolicyRules");
        builder.HasIndex(x => new { x.CompanyId, x.SlaPolicyId, x.SortOrder });
        builder.HasOne(x => x.SlaPolicy).WithMany(p => p.Rules).HasForeignKey(x => x.SlaPolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SlaTargetConfiguration : IEntityTypeConfiguration<SlaTarget>
{
    public void Configure(EntityTypeBuilder<SlaTarget> builder)
    {
        builder.ToTable("SlaTargets");
        builder.HasIndex(x => new { x.SlaPolicyId, x.Metric, x.Priority }).IsUnique();
    }
}

public class SlaTimerConfiguration : IEntityTypeConfiguration<SlaTimer>
{
    public void Configure(EntityTypeBuilder<SlaTimer> builder)
    {
        builder.ToTable("SlaTimers");
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.HasIndex(x => new { x.CompanyId, x.EntityType, x.EntityId, x.Metric })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] <> 4");
        builder.HasIndex(x => x.ServiceTicketId);
        builder.HasOne(x => x.ServiceTicket).WithMany().HasForeignKey(x => x.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
        builder.HasOne(x => x.SlaPolicy).WithMany().HasForeignKey(x => x.SlaPolicyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SlaTarget).WithMany().HasForeignKey(x => x.SlaTargetId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Events).WithOne(e => e.SlaTimer!).HasForeignKey(e => e.SlaTimerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SlaEventConfiguration : IEntityTypeConfiguration<SlaEvent>
{
    public void Configure(EntityTypeBuilder<SlaEvent> builder)
    {
        builder.ToTable("SlaEvents");
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => x.SlaTimerId);
    }
}

public class BusinessCalendarConfiguration : IEntityTypeConfiguration<BusinessCalendar>
{
    public void Configure(EntityTypeBuilder<BusinessCalendar> builder)
    {
        builder.ToTable("BusinessCalendars");
        builder.Property(x => x.TimeZoneId).HasMaxLength(80).IsRequired();
        builder.Property(x => x.WorkIntervalsJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.HolidaysJson).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => x.CompanyId).IsUnique();
    }
}
