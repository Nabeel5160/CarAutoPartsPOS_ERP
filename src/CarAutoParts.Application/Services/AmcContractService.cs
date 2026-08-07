using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public interface IAmcContractService
{
    Task<IReadOnlyList<AmcContractDto>> ListAsync(int? customerId = null, AmcContractStatus? status = null, CancellationToken ct = default);
    Task<AmcContractDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<AmcContractDto>> UpsertAsync(AmcContractUpsertDto dto, CancellationToken ct = default);
    Task<Result> SoftDeleteAsync(int id, CancellationToken ct = default);
}

public sealed class AmcContractService : IAmcContractService
{
    private readonly IRepository<AmcContract> _contracts;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Product> _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _user;

    public AmcContractService(
        IRepository<AmcContract> contracts,
        IRepository<Customer> customers,
        IRepository<Product> products,
        IUnitOfWork uow,
        ICurrentCompanyContext company,
        ICurrentUserService user)
    {
        _contracts = contracts;
        _customers = customers;
        _products = products;
        _uow = uow;
        _company = company;
        _user = user;
    }

    private int? TryCompanyId() => _company.CompanyId is int id && id > 0 ? id : null;

    public async Task<IReadOnlyList<AmcContractDto>> ListAsync(
        int? customerId = null, AmcContractStatus? status = null, CancellationToken ct = default)
    {
        await ExpireOverdueAsync(ct);
        var q = _contracts.Query().AsNoTracking().Include(c => c.Customer).Include(c => c.Product).AsQueryable();
        if (customerId is int cid) q = q.Where(c => c.CustomerId == cid);
        if (status is AmcContractStatus st) q = q.Where(c => c.Status == st);
        var list = await q.OrderByDescending(c => c.EndDate).Take(200).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<AmcContractDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await ExpireOverdueAsync(ct);
        var c = await _contracts.Query().AsNoTracking()
            .Include(x => x.Customer).Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : Map(c);
    }

    public async Task<Result<AmcContractDto>> UpsertAsync(AmcContractUpsertDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ContractNumber))
            return Result<AmcContractDto>.Failure("Contract number is required.");
        if (dto.EndDate.Date < dto.StartDate.Date)
            return Result<AmcContractDto>.Failure("End date must be on or after start date.");

        var companyId = TryCompanyId();
        if (companyId is null)
            return Result<AmcContractDto>.Failure("Company context is required.");

        if (!await _customers.Query().AsNoTracking().AnyAsync(c => c.Id == dto.CustomerId, ct))
            return Result<AmcContractDto>.Failure("Customer not found.");
        if (dto.ProductId is int pid && !await _products.Query().AsNoTracking().AnyAsync(p => p.Id == pid, ct))
            return Result<AmcContractDto>.Failure("Product not found.");

        var number = dto.ContractNumber.Trim();
        AmcContract entity;
        if (dto.Id is int id)
        {
            var existing = await _contracts.Query().FirstOrDefaultAsync(c => c.Id == id, ct);
            if (existing is null)
                return Result<AmcContractDto>.Failure("Contract not found.");
            var dup = await _contracts.Query().AnyAsync(
                c => c.Id != id && c.ContractNumber == number, ct);
            if (dup)
                return Result<AmcContractDto>.Failure("Contract number already exists.");
            entity = existing;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _user.CurrentUser?.Username;
        }
        else
        {
            if (await _contracts.Query().AnyAsync(c => c.ContractNumber == number, ct))
                return Result<AmcContractDto>.Failure("Contract number already exists.");
            entity = new AmcContract
            {
                CompanyId = companyId.Value,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _user.CurrentUser?.Username ?? "system"
            };
            _contracts.Add(entity);
        }

        entity.ContractNumber = number;
        entity.CustomerId = dto.CustomerId;
        entity.StartDate = dto.StartDate.Date;
        entity.EndDate = dto.EndDate.Date;
        entity.Status = ResolveStatus(dto.Status, entity.EndDate);
        entity.CoverageNotes = string.IsNullOrWhiteSpace(dto.CoverageNotes) ? null : dto.CoverageNotes.Trim();
        entity.AnnualAmount = dto.AnnualAmount;
        entity.ProductId = dto.ProductId;

        await _uow.SaveChangesAsync(ct);
        var loaded = await _contracts.Query().AsNoTracking()
            .Include(c => c.Customer).Include(c => c.Product)
            .FirstAsync(c => c.Id == entity.Id, ct);
        return Result<AmcContractDto>.Success(Map(loaded));
    }

    public async Task<Result> SoftDeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _contracts.Query().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null)
            return Result.Failure("Contract not found.");
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _user.CurrentUser?.Username;
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task ExpireOverdueAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var overdue = await _contracts.Query()
            .Where(c => c.Status == AmcContractStatus.Active && c.EndDate < today)
            .ToListAsync(ct);
        if (overdue.Count == 0) return;
        foreach (var c in overdue)
        {
            c.Status = AmcContractStatus.Expired;
            c.UpdatedAt = DateTime.UtcNow;
        }
        await _uow.SaveChangesAsync(ct);
    }

    private static AmcContractStatus ResolveStatus(AmcContractStatus requested, DateTime endDate)
    {
        if (requested is AmcContractStatus.Cancelled or AmcContractStatus.Draft)
            return requested;
        if (endDate.Date < DateTime.UtcNow.Date)
            return AmcContractStatus.Expired;
        return requested == AmcContractStatus.Expired ? AmcContractStatus.Expired : AmcContractStatus.Active;
    }

    private static AmcContractDto Map(AmcContract c) => new(
        c.Id, c.ContractNumber, c.CustomerId, c.Customer?.Name,
        c.StartDate, c.EndDate, c.Status, c.CoverageNotes, c.AnnualAmount,
        c.ProductId, c.Product?.Name);
}
