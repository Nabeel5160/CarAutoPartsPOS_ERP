using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Purchase order lifecycle management.</summary>
public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IRepository<PurchaseOrder> _orders;
    private readonly IRepository<PurchaseOrderLine> _lines;
    private readonly IRepository<Supplier> _suppliers;
    private readonly IRepository<Product> _products;
    private readonly IInventoryService _inventory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<PurchaseOrderCreateDto> _validator;

    public PurchaseOrderService(
        IRepository<PurchaseOrder> orders,
        IRepository<PurchaseOrderLine> lines,
        IRepository<Supplier> suppliers,
        IRepository<Product> products,
        IInventoryService inventory,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<PurchaseOrderCreateDto> validator)
    {
        _orders = orders;
        _lines = lines;
        _suppliers = suppliers;
        _products = products;
        _inventory = inventory;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<PagedResult<PurchaseOrderListDto>> GetOrdersAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _orders.Query()
            .Include(o => o.Supplier)
            .Where(o => !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(s) || o.Supplier.Name.Contains(s));
        }

        if (query.Filters.TryGetValue("Status", out var statusObj) && statusObj is PurchaseOrderStatus status)
            q = q.Where(o => o.Status == status);

        q = q.OrderByDescending(o => o.OrderDate);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<PurchaseOrderListDto>
        {
            Items = _mapper.Map<List<PurchaseOrderListDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await _orders.Query()
            .Include(o => o.Supplier)
            .Include(o => o.Warehouse)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);

        return order is null ? null : _mapper.Map<PurchaseOrderDetailDto>(order);
    }

    /// <inheritdoc />
    public async Task<Result<PurchaseOrderDetailDto>> CreateAsync(PurchaseOrderCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<PurchaseOrderDetailDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        if (!await _suppliers.ExistsAsync(s => s.Id == dto.SupplierId && !s.IsDeleted, ct))
            return Result<PurchaseOrderDetailDto>.Failure("Supplier not found.");

        var order = new PurchaseOrder
        {
            OrderNumber = await GenerateOrderNumberAsync(ct),
            SupplierId = dto.SupplierId,
            ExpectedDate = dto.ExpectedDate,
            WarehouseId = dto.WarehouseId,
            Notes = dto.Notes,
            SupplierBackorderNotes = dto.SupplierBackorderNotes,
            PurchaseRequisitionId = dto.PurchaseRequisitionId,
            DiscountAmount = dto.DiscountAmount,
            Status = PurchaseOrderStatus.Draft
        };

        decimal subTotal = 0, tax = 0;
        foreach (var line in dto.Lines)
        {
            var product = await _products.GetByIdAsync(line.ProductId, ct);
            if (product is null || product.IsDeleted)
                return Result<PurchaseOrderDetailDto>.Failure($"Product {line.ProductId} not found.");

            var lineTax = line.QuantityOrdered * line.UnitPrice * line.TaxRate / 100m;
            var lineTotal = line.QuantityOrdered * line.UnitPrice + lineTax - line.DiscountAmount;
            subTotal += line.QuantityOrdered * line.UnitPrice;
            tax += lineTax;

            order.Lines.Add(new PurchaseOrderLine
            {
                ProductId = line.ProductId,
                QuantityOrdered = line.QuantityOrdered,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate,
                DiscountAmount = line.DiscountAmount,
                LineTotal = lineTotal
            });
        }

        order.SubTotal = subTotal;
        order.TaxAmount = tax;
        order.GrandTotal = subTotal + tax - dto.DiscountAmount;
        _orders.Add(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<PurchaseOrderDetailDto>.Success((await GetByIdAsync(order.Id, ct))!);
    }

    /// <inheritdoc />
    public async Task<Result<PurchaseOrderDetailDto>> UpdateAsync(int id, PurchaseOrderCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<PurchaseOrderDetailDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var order = await _orders.Query()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);

        if (order is null)
            return Result<PurchaseOrderDetailDto>.Failure("Purchase order not found.");

        if (order.Status != PurchaseOrderStatus.Draft)
            return Result<PurchaseOrderDetailDto>.Failure("Only draft orders can be updated.");

        order.SupplierId = dto.SupplierId;
        order.ExpectedDate = dto.ExpectedDate;
        order.WarehouseId = dto.WarehouseId;
        order.Notes = dto.Notes;
        order.SupplierBackorderNotes = dto.SupplierBackorderNotes;
        order.DiscountAmount = dto.DiscountAmount;
        order.Lines.Clear();

        decimal subTotal = 0, tax = 0;
        foreach (var line in dto.Lines)
        {
            var lineTax = line.QuantityOrdered * line.UnitPrice * line.TaxRate / 100m;
            var lineTotal = line.QuantityOrdered * line.UnitPrice + lineTax - line.DiscountAmount;
            subTotal += line.QuantityOrdered * line.UnitPrice;
            tax += lineTax;

            order.Lines.Add(new PurchaseOrderLine
            {
                ProductId = line.ProductId,
                QuantityOrdered = line.QuantityOrdered,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate,
                DiscountAmount = line.DiscountAmount,
                LineTotal = lineTotal
            });
        }

        order.SubTotal = subTotal;
        order.TaxAmount = tax;
        order.GrandTotal = subTotal + tax - dto.DiscountAmount;
        order.UpdatedAt = DateTime.UtcNow;
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<PurchaseOrderDetailDto>.Success((await GetByIdAsync(id, ct))!);
    }

    /// <inheritdoc />
    public async Task<Result> ApproveAsync(int id, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null || order.IsDeleted)
            return Result.Failure("Purchase order not found.");

        if (order.Status != PurchaseOrderStatus.Draft)
            return Result.Failure("Only draft orders can be approved.");

        order.Status = PurchaseOrderStatus.Approved;
        order.UpdatedAt = DateTime.UtcNow;
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> CancelAsync(int id, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null || order.IsDeleted)
            return Result.Failure("Purchase order not found.");

        order.Status = PurchaseOrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ReceiveAsync(int id, ReceivePurchaseOrderDto dto, CancellationToken ct = default)
    {
        var order = await _orders.Query()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);

        if (order is null)
            return Result.Failure("Purchase order not found.");

        if (order.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
            return Result.Failure("Order must be approved before receiving.");

        if (!order.WarehouseId.HasValue)
            return Result.Failure("Warehouse is required for receiving.");

        foreach (var recv in dto.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == recv.LineId);
            if (line is null) continue;

            var qty = Math.Min(recv.QuantityReceived, line.QuantityOrdered - line.QuantityReceived);
            if (qty <= 0) continue;

            var receiveResult = await _inventory.ReceiveStockAsync(
                line.ProductId,
                order.WarehouseId.Value,
                qty,
                line.UnitPrice,
                recv.BatchNumber,
                ct);

            if (!receiveResult.Succeeded)
                return receiveResult;

            line.QuantityReceived += qty;
        }

        var allReceived = order.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered);
        order.Status = allReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        order.UpdatedAt = DateTime.UtcNow;
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        var count = await _orders.Query().CountAsync(ct);
        return $"PO-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
    }
}
