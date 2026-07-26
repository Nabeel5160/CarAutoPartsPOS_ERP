using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Enterprise;

public sealed class AccountMappingService : IAccountMappingService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;

    public AccountMappingService(IEnterpriseDb db, ICurrentCompanyContext company)
    {
        _db = db;
        _company = company;
    }

    public async Task<IReadOnlyList<AccountMappingDto>> GetAccountMappingsAsync(CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();

        var items = await _db.AccountMappings
            .AsNoTracking()
            .Include(m => m.Account)
            .OrderBy(m => m.DocumentType)
            .ThenBy(m => m.MappingKey)
            .ToListAsync(ct);

        return items.Select(MapMapping).ToList();
    }

    public async Task<Result<AccountMappingDto>> CreateAccountMappingAsync(
        CreateAccountMappingRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<AccountMappingDto>.Failure(error!);

        if (string.IsNullOrWhiteSpace(request.DocumentType) || string.IsNullOrWhiteSpace(request.MappingKey))
            return Result<AccountMappingDto>.Failure("Document type and mapping key are required.");

        if (!await _db.GlAccounts.AnyAsync(a => a.Id == request.AccountId && !a.IsDeleted, ct))
            return Result<AccountMappingDto>.Failure("GL account not found.");

        var duplicate = await _db.AccountMappings
            .AnyAsync(m =>
                m.DocumentType == request.DocumentType.Trim() &&
                m.MappingKey == request.MappingKey.Trim(), ct);

        if (duplicate)
            return Result<AccountMappingDto>.Failure("An account mapping with the same document type and key already exists.");

        var mapping = new AccountMapping
        {
            CompanyId = companyId,
            DocumentType = request.DocumentType.Trim(),
            MappingKey = request.MappingKey.Trim(),
            AccountId = request.AccountId
        };

        _db.AccountMappings.Add(mapping);
        await _db.SaveChangesAsync(ct);

        var created = await _db.AccountMappings
            .AsNoTracking()
            .Include(m => m.Account)
            .FirstAsync(m => m.Id == mapping.Id, ct);

        return Result<AccountMappingDto>.Success(MapMapping(created));
    }

    public async Task<Result<AccountMappingDto>> UpdateAccountMappingAsync(
        int id,
        UpdateAccountMappingRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<AccountMappingDto>.Failure(error!);

        if (string.IsNullOrWhiteSpace(request.DocumentType) || string.IsNullOrWhiteSpace(request.MappingKey))
            return Result<AccountMappingDto>.Failure("Document type and mapping key are required.");

        var mapping = await _db.AccountMappings
            .Include(m => m.Account)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (mapping is null)
            return Result<AccountMappingDto>.Failure("Account mapping not found.");

        if (!await _db.GlAccounts.AnyAsync(a => a.Id == request.AccountId && !a.IsDeleted, ct))
            return Result<AccountMappingDto>.Failure("GL account not found.");

        var duplicate = await _db.AccountMappings
            .AnyAsync(m =>
                m.Id != id &&
                m.DocumentType == request.DocumentType.Trim() &&
                m.MappingKey == request.MappingKey.Trim(), ct);

        if (duplicate)
            return Result<AccountMappingDto>.Failure("An account mapping with the same document type and key already exists.");

        mapping.DocumentType = request.DocumentType.Trim();
        mapping.MappingKey = request.MappingKey.Trim();
        mapping.AccountId = request.AccountId;
        mapping.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<AccountMappingDto>.Success(MapMapping(mapping));
    }

    public async Task<Result> DeleteAccountMappingAsync(int id, CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result.Failure(error!);

        var mapping = await _db.AccountMappings.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mapping is null)
            return Result.Failure("Account mapping not found.");

        mapping.IsDeleted = true;
        mapping.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
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

    private void EnsureCompanyOrThrow()
    {
        if (!_company.CompanyId.HasValue)
            throw new InvalidOperationException("Company context is required.");
    }

    private static AccountMappingDto MapMapping(AccountMapping m) => new(
        m.Id,
        m.DocumentType,
        m.MappingKey,
        m.AccountId,
        m.Account?.Code,
        m.Account?.Name);
}
