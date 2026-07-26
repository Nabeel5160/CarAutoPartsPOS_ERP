using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Transfers;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Inter-warehouse inventory transfers.</summary>
public class TransferService : ITransferService
{
    private readonly IRepository<InventoryTransfer> _transfers;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<Product> _products;
    private readonly IInventoryService _inventory;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public TransferService(
        IRepository<InventoryTransfer> transfers,
        IRepository<Warehouse> warehouses,
        IRepository<Product> products,
        IInventoryService inventory,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _transfers = transfers;
        _warehouses = warehouses;
        _products = products;
        _inventory = inventory;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TransferListDto>> GetTransfersAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _transfers.Query()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Where(t => !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.TransferNumber.Contains(s));
        }

        if (query.Filters.TryGetValue("Status", out var statusObj) && statusObj is TransferStatus status)
            q = q.Where(t => t.Status == status);

        q = q.OrderByDescending(t => t.TransferDate);

        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<TransferListDto>
        {
            Items = _mapper.Map<List<TransferListDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<TransferDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var transfer = await _transfers.Query()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        return transfer is null ? null : _mapper.Map<TransferDetailDto>(transfer);
    }

    /// <inheritdoc />
    public async Task<Result<TransferDetailDto>> CreateAsync(TransferCreateDto dto, CancellationToken ct = default)
    {
        if (dto.FromWarehouseId == dto.ToWarehouseId)
            return Result<TransferDetailDto>.Failure("Source and destination warehouses must differ.");

        if (dto.Lines.Count == 0)
            return Result<TransferDetailDto>.Failure("At least one line is required.");

        if (!await _warehouses.ExistsAsync(w => w.Id == dto.FromWarehouseId && !w.IsDeleted, ct))
            return Result<TransferDetailDto>.Failure("Source warehouse not found.");

        if (!await _warehouses.ExistsAsync(w => w.Id == dto.ToWarehouseId && !w.IsDeleted, ct))
            return Result<TransferDetailDto>.Failure("Destination warehouse not found.");

        var transfer = new InventoryTransfer
        {
            TransferNumber = await GenerateTransferNumberAsync(ct),
            FromWarehouseId = dto.FromWarehouseId,
            ToWarehouseId = dto.ToWarehouseId,
            Notes = dto.Notes,
            Status = TransferStatus.Draft,
            TransferDate = DateTime.UtcNow
        };

        foreach (var line in dto.Lines)
        {
            if (!await _products.ExistsAsync(p => p.Id == line.ProductId && !p.IsDeleted, ct))
                return Result<TransferDetailDto>.Failure($"Product {line.ProductId} not found.");

            transfer.Lines.Add(new InventoryTransferLine
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity
            });
        }

        _transfers.Add(transfer);
        await _unitOfWork.SaveChangesAsync(ct);

        var detail = await _transfers.Query()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.Lines).ThenInclude(l => l.Product)
            .FirstAsync(t => t.Id == transfer.Id, ct);

        return Result<TransferDetailDto>.Success(_mapper.Map<TransferDetailDto>(detail));
    }

    /// <inheritdoc />
    public async Task<Result> ApproveAsync(int id, CancellationToken ct = default)
    {
        var transfer = await _transfers.GetByIdAsync(id, ct);
        if (transfer is null || transfer.IsDeleted)
            return Result.Failure("Transfer not found.");

        if (transfer.Status != TransferStatus.Draft)
            return Result.Failure("Only draft transfers can be approved.");

        transfer.Status = TransferStatus.Approved;
        transfer.ApprovedBy = _currentUser.CurrentUser?.DisplayName;
        transfer.ApprovedAt = DateTime.UtcNow;
        transfer.UpdatedAt = DateTime.UtcNow;
        _transfers.Update(transfer);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ShipAsync(int id, CancellationToken ct = default)
    {
        var transfer = await _transfers.Query()
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        if (transfer is null)
            return Result.Failure("Transfer not found.");

        if (transfer.Status != TransferStatus.Approved)
            return Result.Failure("Only approved transfers can be shipped.");

        foreach (var line in transfer.Lines)
        {
            var deduct = await _inventory.DeductStockAsync(
                line.ProductId,
                transfer.FromWarehouseId,
                line.Quantity,
                nameof(InventoryTransfer),
                transfer.Id,
                ct);
            if (!deduct.Succeeded)
                return deduct;
        }

        transfer.Status = TransferStatus.InTransit;
        transfer.UpdatedAt = DateTime.UtcNow;
        _transfers.Update(transfer);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> CompleteAsync(int id, CancellationToken ct = default)
    {
        var transfer = await _transfers.Query()
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        if (transfer is null)
            return Result.Failure("Transfer not found.");

        if (transfer.Status == TransferStatus.Approved)
        {
            var ship = await ShipAsync(id, ct);
            if (!ship.Succeeded) return ship;
            transfer = await _transfers.Query()
                .Include(t => t.Lines)
                .FirstAsync(t => t.Id == id, ct);
        }

        if (transfer.Status != TransferStatus.InTransit)
            return Result.Failure("Transfer must be in transit (shipped) before receive/complete.");

        foreach (var line in transfer.Lines)
        {
            await _inventory.ReceiveStockAsync(
                line.ProductId,
                transfer.ToWarehouseId,
                line.Quantity,
                0,
                null,
                ct);
        }

        transfer.Status = TransferStatus.Completed;
        transfer.UpdatedAt = DateTime.UtcNow;
        _transfers.Update(transfer);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<string> GenerateTransferNumberAsync(CancellationToken ct)
    {
        var count = await _transfers.Query().CountAsync(ct);
        return $"TR-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
    }
}
