using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Transfers;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Inter-warehouse inventory transfers with optional inter-branch GIT GL.</summary>
public class TransferService : ITransferService
{
    private readonly IRepository<InventoryTransfer> _transfers;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<Product> _products;
    private readonly IInventoryService _inventory;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentCompanyContext _company;
    private readonly IGlPostingService _gl;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IApprovalWorkflowService _approvals;
    private readonly IMoneyAuditService _moneyAudit;

    public TransferService(
        IRepository<InventoryTransfer> transfers,
        IRepository<Warehouse> warehouses,
        IRepository<Product> products,
        IInventoryService inventory,
        ICurrentUserService currentUser,
        ICurrentCompanyContext company,
        IGlPostingService gl,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IApprovalWorkflowService approvals,
        IMoneyAuditService moneyAudit)
    {
        _transfers = transfers;
        _warehouses = warehouses;
        _products = products;
        _inventory = inventory;
        _currentUser = currentUser;
        _company = company;
        _gl = gl;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _approvals = approvals;
        _moneyAudit = moneyAudit;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TransferListDto>> GetTransfersAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _transfers.Query()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.Lines)
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
            Items = paged.Items.Select(MapList).ToList(),
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

        return transfer is null ? null : MapDetail(transfer);
    }

    /// <inheritdoc />
    public async Task<Result<TransferDetailDto>> CreateAsync(TransferCreateDto dto, CancellationToken ct = default)
    {
        if (dto.FromWarehouseId == dto.ToWarehouseId)
            return Result<TransferDetailDto>.Failure("Source and destination warehouses must differ.");

        if (dto.Lines.Count == 0)
            return Result<TransferDetailDto>.Failure("At least one line is required.");

        var fromWh = await _warehouses.Query().FirstOrDefaultAsync(w => w.Id == dto.FromWarehouseId && !w.IsDeleted, ct);
        if (fromWh is null)
            return Result<TransferDetailDto>.Failure("Source warehouse not found.");

        var toWh = await _warehouses.Query().FirstOrDefaultAsync(w => w.Id == dto.ToWarehouseId && !w.IsDeleted, ct);
        if (toWh is null)
            return Result<TransferDetailDto>.Failure("Destination warehouse not found.");

        if (fromWh.BranchId is int fromBranch && !_company.IsBranchAllowed(fromBranch))
            return Result<TransferDetailDto>.Failure("Source warehouse branch is not allowed for this user.");

        if (toWh.BranchId is int toBranch && !_company.IsBranchAllowed(toBranch))
            return Result<TransferDetailDto>.Failure("Destination warehouse branch is not allowed for this user.");

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
                Quantity = line.Quantity,
                FromLocationId = line.FromLocationId,
                ToLocationId = line.ToLocationId
            });
        }

        _transfers.Add(transfer);
        await _unitOfWork.SaveChangesAsync(ct);

        var detail = await _transfers.Query()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.Lines).ThenInclude(l => l.Product)
            .FirstAsync(t => t.Id == transfer.Id, ct);

        return Result<TransferDetailDto>.Success(MapDetail(detail));
    }

    /// <inheritdoc />
    public async Task<Result> ApproveAsync(int id, CancellationToken ct = default)
    {
        var transfer = await _transfers.Query()
            .Include(t => t.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (transfer is null)
            return Result.Failure("Transfer not found.");

        if (transfer.Status != TransferStatus.Draft)
            return Result.Failure("Only draft transfers can be approved.");

        var amount = transfer.Lines.Sum(l =>
            l.Quantity * (l.Product?.CostPrice ?? l.Product?.PurchasePrice ?? 0));
        var gate = await _approvals.EnsureApprovedOrQueueAsync(
            "InventoryTransfer", transfer.Id, transfer.TransferNumber, amount, ct);
        if (!gate.Succeeded)
            return gate;

        transfer.Status = TransferStatus.Approved;
        transfer.ApprovedBy = _currentUser.CurrentUser?.DisplayName;
        transfer.ApprovedAt = DateTime.UtcNow;
        transfer.UpdatedAt = DateTime.UtcNow;
        _transfers.Update(transfer);
        await _unitOfWork.SaveChangesAsync(ct);
        await _moneyAudit.RecordAsync(Domain.Enums.AuditAction.Approve, nameof(InventoryTransfer), transfer.Id,
            $"Approved {transfer.TransferNumber}", ct: ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<TransferDetailDto>> ConfirmPickAsync(
        int id,
        ConfirmTransferPickRequest? request = null,
        CancellationToken ct = default)
    {
        var transfer = await _transfers.Query()
            .Include(t => t.Lines).ThenInclude(l => l.Product)
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        if (transfer is null)
            return Result<TransferDetailDto>.Failure("Transfer not found.");

        if (transfer.Status is not (TransferStatus.Approved or TransferStatus.Draft))
            return Result<TransferDetailDto>.Failure("Pick can only be confirmed on draft/approved transfers.");

        foreach (var line in transfer.Lines)
        {
            var spec = request?.Lines?.FirstOrDefault(l => l.LineId == line.Id);
            if (spec is not null)
            {
                if (spec.FromLocationId.HasValue) line.FromLocationId = spec.FromLocationId;
                if (spec.ToLocationId.HasValue) line.ToLocationId = spec.ToLocationId;
            }

            line.IsPicked = true;
            line.UpdatedAt = DateTime.UtcNow;
        }

        transfer.UpdatedAt = DateTime.UtcNow;
        _transfers.Update(transfer);
        await _unitOfWork.SaveChangesAsync(ct);
        var detail = await GetByIdAsync(id, ct);
        return detail is null
            ? Result<TransferDetailDto>.Failure("Transfer not found after pick.")
            : Result<TransferDetailDto>.Success(detail);
    }

    /// <inheritdoc />
    public async Task<Result> ShipAsync(int id, CancellationToken ct = default)
    {
        var transfer = await _transfers.Query()
            .Include(t => t.Lines)
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        if (transfer is null)
            return Result.Failure("Transfer not found.");

        if (transfer.Status != TransferStatus.Approved)
            return Result.Failure("Only approved transfers can be shipped.");

        if (transfer.Lines.Count > 0 && transfer.Lines.Any(l => !l.IsPicked))
            return Result.Failure("Confirm pick list before shipping.");

        decimal totalCost = 0;
        foreach (var line in transfer.Lines)
        {
            var deduct = await _inventory.TransferOutAsync(
                line.ProductId,
                transfer.FromWarehouseId,
                line.Quantity,
                transfer.Id,
                line.FromLocationId,
                ct);
            if (!deduct.Succeeded)
                return Result.Failure(deduct.Error ?? "Transfer out failed.");

            line.ShippedUnitCost = deduct.Data;
            totalCost += line.ShippedUnitCost * line.Quantity;
        }

        if (IsInterBranch(transfer) && totalCost > 0 && _company.CompanyId.HasValue)
        {
            var gl = await _gl.PostDocumentAsync(
                "InventoryTransfer",
                DateTime.UtcNow.Date,
                transfer.TransferNumber,
                $"Ship {transfer.TransferNumber}",
                transfer.Id,
                [
                    new GlPostingLineRequest("GoodsInTransit", totalCost, true, "GIT"),
                    new GlPostingLineRequest("Inventory", totalCost, false, "Inventory out")
                ],
                autoPost: true,
                ct);
            if (!gl.Succeeded)
                return Result.Failure(gl.Error ?? "Transfer ship GL failed.");
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
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        if (transfer is null)
            return Result.Failure("Transfer not found.");

        if (transfer.Status == TransferStatus.Approved)
        {
            var ship = await ShipAsync(id, ct);
            if (!ship.Succeeded) return ship;
            transfer = await _transfers.Query()
                .Include(t => t.Lines)
                .Include(t => t.FromWarehouse)
                .Include(t => t.ToWarehouse)
                .FirstAsync(t => t.Id == id, ct);
        }

        if (transfer.Status != TransferStatus.InTransit)
            return Result.Failure("Transfer must be in transit (shipped) before receive/complete.");

        decimal totalCost = 0;
        foreach (var line in transfer.Lines)
        {
            var recv = await _inventory.TransferInAsync(
                line.ProductId,
                transfer.ToWarehouseId,
                line.Quantity,
                line.ShippedUnitCost,
                transfer.Id,
                line.ToLocationId,
                ct);
            if (!recv.Succeeded)
                return Result.Failure(recv.Error ?? "Transfer in failed.");
            totalCost += line.ShippedUnitCost * line.Quantity;
        }

        if (IsInterBranch(transfer) && totalCost > 0 && _company.CompanyId.HasValue)
        {
            var gl = await _gl.PostDocumentAsync(
                "InventoryTransfer",
                DateTime.UtcNow.Date,
                transfer.TransferNumber,
                $"Receive {transfer.TransferNumber}",
                transfer.Id,
                [
                    new GlPostingLineRequest("Inventory", totalCost, true, "Inventory in"),
                    new GlPostingLineRequest("GoodsInTransit", totalCost, false, "GIT clear")
                ],
                autoPost: true,
                ct);
            if (!gl.Succeeded)
                return Result.Failure(gl.Error ?? "Transfer receive GL failed.");
        }

        transfer.Status = TransferStatus.Completed;
        transfer.UpdatedAt = DateTime.UtcNow;
        _transfers.Update(transfer);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static bool IsInterBranch(InventoryTransfer t) =>
        t.FromWarehouse?.BranchId is int from
        && t.ToWarehouse?.BranchId is int to
        && from != to;

    private static TransferListDto MapList(InventoryTransfer t) => new(
        t.Id,
        t.TransferNumber,
        t.FromWarehouse.Name,
        t.ToWarehouse.Name,
        t.Status,
        t.TransferDate,
        IsInterBranch(t),
        t.Lines.Count > 0 && t.Lines.All(l => l.IsPicked));

    private static TransferDetailDto MapDetail(InventoryTransfer t) => new(
        t.Id,
        t.TransferNumber,
        t.FromWarehouseId,
        t.FromWarehouse.Name,
        t.ToWarehouseId,
        t.ToWarehouse.Name,
        t.Status,
        t.TransferDate,
        t.Notes,
        t.ApprovedBy,
        t.ApprovedAt,
        IsInterBranch(t),
        t.Lines.Select(l => new TransferLineDto(
            l.ProductId,
            l.Product?.Name,
            l.Quantity,
            l.ShippedUnitCost,
            l.FromLocationId,
            l.ToLocationId,
            l.IsPicked,
            l.Id)).ToList(),
        t.Lines.Count > 0 && t.Lines.All(l => l.IsPicked));

    private async Task<string> GenerateTransferNumberAsync(CancellationToken ct)
    {
        var count = await _transfers.Query().CountAsync(ct);
        return $"TR-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
    }
}
