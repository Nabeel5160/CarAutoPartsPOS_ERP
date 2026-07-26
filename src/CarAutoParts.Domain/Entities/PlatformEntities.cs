using CarAutoParts.Domain.Common;

namespace CarAutoParts.Domain.Entities;

public class Company : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Ntn { get; set; }
    public string? Strn { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string CurrencyCode { get; set; } = "PKR";
    public bool IsActive { get; set; } = true;
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}

public class Branch : CompanyEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public Company Company { get; set; } = null!;
    public ICollection<CostCenter> CostCenters { get; set; } = new List<CostCenter>();
}

public class CostCenter : CompanyEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class FiscalYear : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public ICollection<AccountingPeriod> Periods { get; set; } = new List<AccountingPeriod>();
}

public class AccountingPeriod : CompanyEntity
{
    public int FiscalYearId { get; set; }
    public FiscalYear FiscalYear { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int PeriodNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}

public class NumberSequence : CompanyEntity
{
    public string DocumentType { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int NextValue { get; set; } = 1;
    public int Padding { get; set; } = 6;
    public bool Gapless { get; set; }
    public string? Suffix { get; set; }

    public string PeekNext() => $"{Prefix}{NextValue.ToString().PadLeft(Padding, '0')}{Suffix}";

    public string AllocateNext()
    {
        var value = PeekNext();
        NextValue++;
        return value;
    }
}

public class OutboxMessage : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public string? Error { get; set; }
    public int AttemptCount { get; set; }
}

public class DocumentAttachment : CompanyEntity
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
