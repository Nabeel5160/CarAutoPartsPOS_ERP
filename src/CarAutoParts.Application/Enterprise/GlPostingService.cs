using CarAutoParts.Application.Common;
using CarAutoParts.Application.Finance;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Enterprise;

public sealed class GlPostingService : IGlPostingService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;
    private readonly IOutboxWriter _outbox;
    private readonly IAccountingPeriodService _periods;

    public GlPostingService(
        IEnterpriseDb db,
        ICurrentCompanyContext company,
        IOutboxWriter outbox,
        IAccountingPeriodService periods)
    {
        _db = db;
        _company = company;
        _outbox = outbox;
        _periods = periods;
    }

    public Task<Result<GlJournalDraftDto>> CreateBalancedJournalAsync(
        string documentType,
        DateTime journalDate,
        string? reference,
        string? description,
        int? sourceDocumentId,
        IReadOnlyList<GlPostingLineRequest> lines,
        CancellationToken ct = default)
        => PostDocumentAsync(documentType, journalDate, reference, description, sourceDocumentId, lines, autoPost: true, ct);

    public async Task<Result<GlJournalDraftDto>> PostDocumentAsync(
        string documentType,
        DateTime journalDate,
        string? reference,
        string? description,
        int? sourceDocumentId,
        IReadOnlyList<GlPostingLineRequest> lines,
        bool autoPost = true,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<GlJournalDraftDto>.Failure(error!);

        if (lines.Count < 2)
            return Result<GlJournalDraftDto>.Failure("Journal must have at least two lines.");

        var periodResult = await _periods.EnsureOpenAsync(journalDate, ct);
        if (!periodResult.Succeeded)
            return Result<GlJournalDraftDto>.Failure(periodResult.Error ?? "Period locked.");

        var mappings = await _db.AccountMappings
            .Where(m => m.DocumentType == documentType)
            .ToDictionaryAsync(m => m.MappingKey, ct);

        var journalNumber = await EnterpriseDocumentNumbers.AllocateAsync(_db, "JV", ct);
        var journal = new JournalEntry
        {
            CompanyId = companyId,
            JournalNumber = journalNumber,
            JournalDate = journalDate.Date,
            Reference = reference,
            Description = description,
            Status = JournalStatus.Draft,
            SourceDocumentType = documentType,
            SourceDocumentId = sourceDocumentId
        };

        foreach (var line in lines)
        {
            if (!mappings.TryGetValue(line.MappingKey, out var mapping))
                return Result<GlJournalDraftDto>.Failure($"Account mapping '{line.MappingKey}' not found for {documentType}.");

            if (line.Amount <= 0)
                return Result<GlJournalDraftDto>.Failure("Line amounts must be positive.");

            journal.Lines.Add(new JournalLine
            {
                CompanyId = companyId,
                AccountId = mapping.AccountId,
                Description = line.Description,
                Debit = line.IsDebit ? line.Amount : 0,
                Credit = line.IsDebit ? 0 : line.Amount
            });
        }

        try
        {
            journal.EnsureBalanced();
            if (autoPost)
                journal.Post(periodResult.Data!);
        }
        catch (Exception ex)
        {
            return Result<GlJournalDraftDto>.Failure(ex.Message);
        }

        _db.JournalEntries.Add(journal);
        await _db.SaveChangesAsync(ct);

        _outbox.Enqueue("DocumentGlPosted", new
        {
            journal.Id,
            journal.JournalNumber,
            journal.Status,
            documentType,
            sourceDocumentId,
            journal.TotalDebit
        });

        return Result<GlJournalDraftDto>.Success(new GlJournalDraftDto(
            journal.Id,
            journal.JournalNumber,
            journal.Status,
            journal.TotalDebit,
            journal.TotalCredit));
    }

    private bool EnsureCompany(out int companyId, out string? error)
    {
        if (_company.CompanyId.HasValue)
        {
            companyId = _company.CompanyId.Value;
            error = null;
            return true;
        }

        companyId = 0;
        error = "Company context is required.";
        return false;
    }
}
