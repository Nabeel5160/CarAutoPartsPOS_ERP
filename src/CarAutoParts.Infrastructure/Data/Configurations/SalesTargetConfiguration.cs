using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class SalesTargetConfiguration : IEntityTypeConfiguration<SalesTarget>
{
    public void Configure(EntityTypeBuilder<SalesTarget> builder)
    {
        builder.ToTable("SalesTargets");
        builder.HasIndex(x => new { x.CompanyId, x.UserId, x.PeriodYear, x.PeriodMonth }).IsUnique();
        builder.Property(x => x.TargetAmount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
