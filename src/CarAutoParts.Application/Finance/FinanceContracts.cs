using CarAutoParts.Application.Common;
using CarAutoParts.Domain.Entities;
using MediatR;

namespace CarAutoParts.Application.Finance;

public record GlAccountDto(int Id, string Code, string Name, AccountType AccountType, int? ParentAccountId, bool IsPostable, bool IsActive);

public record JournalLineDto(int AccountId, string? Description, decimal Debit, decimal Credit, int? CostCenterId);

public record JournalDto(
    int Id,
    string JournalNumber,
    DateTime JournalDate,
    JournalStatus Status,
    string? Reference,
    string? Description,
    decimal TotalDebit,
    decimal TotalCredit,
    IReadOnlyList<JournalLineDto> Lines);

public record AccountingPeriodDto(int Id, string Name, int PeriodNumber, DateTime StartDate, DateTime EndDate, bool IsClosed, int FiscalYearId);

public record CompanyDto(int Id, string Code, string Name, string? Ntn, string CurrencyCode, bool IsActive);

public record BranchDto(int Id, int CompanyId, string Code, string Name, bool IsDefault, bool IsActive);

public record NumberSequenceDto(int Id, string DocumentType, string Prefix, int NextValue, int Padding, bool Gapless);

// Queries / Commands
public record GetCompaniesQuery : IRequest<IReadOnlyList<CompanyDto>>;
public record GetBranchesQuery(int CompanyId) : IRequest<IReadOnlyList<BranchDto>>;
public record GetChartOfAccountsQuery : IRequest<IReadOnlyList<GlAccountDto>>;
public record GetOpenPeriodsQuery : IRequest<IReadOnlyList<AccountingPeriodDto>>;
public record GetJournalsQuery(int Page = 1, int PageSize = 50) : IRequest<PagedResult<JournalDto>>;

public record CreateGlAccountCommand(string Code, string Name, AccountType AccountType, int? ParentAccountId, bool IsPostable) : IRequest<Result<GlAccountDto>>;
public record CreateJournalCommand(DateTime JournalDate, string? Reference, string? Description, IReadOnlyList<JournalLineDto> Lines) : IRequest<Result<JournalDto>>;
public record PostJournalCommand(int JournalId) : IRequest<Result>;
public record ClosePeriodCommand(int PeriodId) : IRequest<Result>;
public record ReopenPeriodCommand(int PeriodId) : IRequest<Result>;
public record GetNextDocumentNumberQuery(string DocumentType) : IRequest<Result<string>>;
