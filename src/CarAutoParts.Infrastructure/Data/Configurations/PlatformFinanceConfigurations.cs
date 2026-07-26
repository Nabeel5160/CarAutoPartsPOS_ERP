using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.HasOne(x => x.Company).WithMany(c => c.Branches).HasForeignKey(x => x.CompanyId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        builder.HasOne(x => x.Branch).WithMany(b => b.CostCenters).HasForeignKey(x => x.BranchId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.HasIndex(x => new { x.FiscalYearId, x.PeriodNumber }).IsUnique();
        builder.HasOne(x => x.FiscalYear).WithMany(y => y.Periods).HasForeignKey(x => x.FiscalYearId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.DocumentType }).IsUnique();
        builder.Property(x => x.DocumentType).HasMaxLength(64);
        builder.Property(x => x.Prefix).HasMaxLength(16);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc });
        builder.Property(x => x.Type).HasMaxLength(200);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class DocumentAttachmentConfiguration : IEntityTypeConfiguration<DocumentAttachment>
{
    public void Configure(EntityTypeBuilder<DocumentAttachment> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.EntityType, x.EntityId });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class GlAccountConfiguration : IEntityTypeConfiguration<GlAccount>
{
    public void Configure(EntityTypeBuilder<GlAccount> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        builder.Property(x => x.Code).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.HasOne(x => x.ParentAccount).WithMany(x => x.Children).HasForeignKey(x => x.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class AccountMappingConfiguration : IEntityTypeConfiguration<AccountMapping>
{
    public void Configure(EntityTypeBuilder<AccountMapping> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.DocumentType, x.MappingKey }).IsUnique();
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.HasIndex(x => new { x.CompanyId, x.JournalNumber }).IsUnique();
        builder.Property(x => x.JournalNumber).HasMaxLength(40);
        builder.HasOne(x => x.AccountingPeriod).WithMany().HasForeignKey(x => x.AccountingPeriodId);
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.Property(x => x.Debit).HasPrecision(18, 2);
        builder.Property(x => x.Credit).HasPrecision(18, 2);
        builder.HasOne(x => x.JournalEntry).WithMany(j => j.Lines).HasForeignKey(x => x.JournalEntryId);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
