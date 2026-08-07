using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.CompanyId, x.FiscalYearId, x.Name });
        builder.HasOne(x => x.FiscalYear).WithMany().HasForeignKey(x => x.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(l => l.Budget).HasForeignKey(l => l.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BudgetLineConfiguration : IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> builder)
    {
        builder.ToTable("BudgetLines");
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.BudgetId, x.GlAccountId, x.AccountingPeriodId, x.CostCenterId });
        builder.HasOne(x => x.GlAccount).WithMany().HasForeignKey(x => x.GlAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CostCenter).WithMany().HasForeignKey(x => x.CostCenterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AccountingPeriod).WithMany().HasForeignKey(x => x.AccountingPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
