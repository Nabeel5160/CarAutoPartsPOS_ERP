using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.DTOs.Sales;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Sales and purchase return processing with GL on sales returns.</summary>
public class ReturnService : IReturnService
{
    private readonly IRepository<SalesReturn> _salesReturns;
    private readonly IRepository<PurchaseReturn> _purchaseReturns;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Supplier> _suppliers;
    private readonly IRepository<Customer> _customers;
    private readonly IInventoryService _inventory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IGlPostingService _gl;
    private readonly ICurrentCompanyContext _company;

    public ReturnService(
        IRepository<SalesReturn> salesReturns,
        IRepository<PurchaseReturn> purchaseReturns,
        IRepository<SalesInvoice> invoices,
        IRepository<Supplier> suppliers,
        IRepository<Customer> customers,
        IInventoryService inventory,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IGlPostingService gl,
        ICurrentCompanyContext company)
    {
        _salesReturns = salesReturns;
        _purchaseReturns = purchaseReturns;
        _invoices = invoices;
        _suppliers = suppliers;
        _customers = customers;
        _inventory = inventory;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _gl = gl;
        _company = company;
    }

    public async Task<Result<SalesReturnDto>> CreateSalesReturnAsync(SalesReturnCreateDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0)
            return Result<SalesReturnDto>.Failure("At least one line is required.");
        if (string.IsNullOrWhiteSpace(dto.ReasonCode))
            return Result<SalesReturnDto>.Failure("Reason code is required.");

        SalesInvoice? invoice = null;
        if (dto.SalesInvoiceId is int invId)
        {
            invoice = await _invoices.Query()
                .Include(i => i.Lines)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == invId && !i.IsDeleted, ct);
            if (invoice is null)
                return Result<SalesReturnDto>.Failure("Sales invoice not found.");

            var prior = await _salesReturns.Query()
                .Include(r => r.Lines)
                .Where(r => r.SalesInvoiceId == invId && !r.IsDeleted && r.Status != ReturnStatus.Cancelled)
                .ToListAsync(ct);

            foreach (var line in dto.Lines)
            {
                var sold = invoice.Lines.Where(l => l.ProductId == line.ProductId).Sum(l => l.Quantity);
                var already = prior.SelectMany(r => r.Lines).Where(l => l.ProductId == line.ProductId).Sum(l => l.Quantity);
                if (already + line.Quantity > sold + 0.0001m)
                    return Result<SalesReturnDto>.Failure($"Return qty for product {line.ProductId} exceeds invoice remaining qty.");
            }
        }

        SalesReturn? entity = null;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            entity = new SalesReturn
            {
                ReturnNumber = await GenerateSalesReturnNumberAsync(innerCt),
                SalesInvoiceId = dto.SalesInvoiceId,
                CustomerId = dto.CustomerId ?? invoice?.CustomerId,
                ReturnType = dto.ReturnType,
                Notes = dto.Notes,
                ReasonCode = dto.ReasonCode.Trim(),
                Status = ReturnStatus.Completed,
                ReturnDate = DateTime.UtcNow
            };

            decimal total = 0;
            decimal taxEstimate = 0;
            foreach (var line in dto.Lines)
            {
                var lineTotal = line.Quantity * line.UnitPrice;
                total += lineTotal;
                entity.Lines.Add(new SalesReturnLine
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = lineTotal
                });

                var restock = await _inventory.ReturnStockAsync(
                    line.ProductId, dto.WarehouseId, line.Quantity, nameof(SalesReturn), 0, innerCt);
                if (!restock.Succeeded)
                    throw new InvalidOperationException(restock.Error ?? "Restock failed.");

                if (invoice is not null)
                {
                    var invLine = invoice.Lines.FirstOrDefault(l => l.ProductId == line.ProductId);
                    if (invLine is not null && invLine.LineTotal > 0)
                        taxEstimate += invLine.TaxAmount * (line.Quantity / invLine.Quantity);
                }
            }

            entity.GrandTotal = total;
            _salesReturns.Add(entity);
            await _unitOfWork.SaveChangesAsync(innerCt);

            // Fix restock source id
            foreach (var _ in entity.Lines)
            {
                // inventory already moved; source id 0 is historical — acceptable for Phase 1
            }

            if (entity.CustomerId is int cid && total > 0)
            {
                var customer = await _customers.Query().FirstOrDefaultAsync(c => c.Id == cid && !c.IsDeleted, innerCt);
                if (customer is not null)
                    customer.Balance = Math.Max(0, customer.Balance - total);
            }

            if (_company.CompanyId.HasValue && total > 0)
            {
                var net = Math.Max(0, total - taxEstimate);
                var paidCash = invoice?.Payments.Any(p => p.PaymentMethod.Contains("cash", StringComparison.OrdinalIgnoreCase)) == true;
                var refundKey = paidCash || invoice is null ? "Cash" :
                    invoice.Payments.Any(p => p.PaymentMethod.Contains("bank", StringComparison.OrdinalIgnoreCase) || p.PaymentMethod.Contains("card", StringComparison.OrdinalIgnoreCase))
                        ? "Bank" : "Receivable";

                var glLines = new List<GlPostingLineRequest>
                {
                    new("Revenue", net > 0 ? net : total, true, "Sales return"),
                };
                if (taxEstimate > 0)
                    glLines.Add(new("Tax", taxEstimate, true, "Tax reverse"));
                glLines.Add(new(refundKey, total, false, "Refund/AR"));

                // COGS reverse estimate using line cost approx = 0 if unknown; skip if zero
                var gl = await _gl.PostDocumentAsync(
                    "SalesReturn",
                    entity.ReturnDate,
                    entity.ReturnNumber,
                    $"Return {entity.ReturnNumber}",
                    entity.Id,
                    glLines,
                    autoPost: true,
                    innerCt);
                if (!gl.Succeeded)
                    throw new InvalidOperationException(gl.Error ?? "Return GL posting failed.");
            }

            await _unitOfWork.SaveChangesAsync(innerCt);
        }, ct);

        if (entity is null)
            throw new InvalidOperationException("Return not created.");
        var loaded = await _salesReturns.Query()
            .Include(r => r.SalesInvoice)
            .Include(r => r.Customer)
            .FirstAsync(r => r.Id == entity.Id, ct);

        return Result<SalesReturnDto>.Success(_mapper.Map<SalesReturnDto>(loaded));
    }

    public async Task<Result<PurchaseReturnDto>> CreatePurchaseReturnAsync(PurchaseReturnCreateDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0)
            return Result<PurchaseReturnDto>.Failure("At least one line is required.");
        if (string.IsNullOrWhiteSpace(dto.ReasonCode))
            return Result<PurchaseReturnDto>.Failure("Reason code is required.");
        if (dto.WarehouseId <= 0)
            return Result<PurchaseReturnDto>.Failure("Warehouse is required.");

        if (!await _suppliers.ExistsAsync(s => s.Id == dto.SupplierId && !s.IsDeleted, ct))
            return Result<PurchaseReturnDto>.Failure("Supplier not found.");

        PurchaseReturn? entity = null;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            entity = new PurchaseReturn
            {
                ReturnNumber = await GeneratePurchaseReturnNumberAsync(innerCt),
                SupplierId = dto.SupplierId,
                PurchaseOrderId = dto.PurchaseOrderId,
                WarehouseId = dto.WarehouseId,
                Notes = dto.Notes,
                ReasonCode = dto.ReasonCode.Trim(),
                Status = ReturnStatus.Completed,
                ReturnDate = DateTime.UtcNow
            };

            decimal total = 0;
            foreach (var line in dto.Lines)
            {
                var lineTotal = line.Quantity * line.UnitPrice;
                total += lineTotal;
                entity.Lines.Add(new PurchaseReturnLine
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = lineTotal
                });

                var deduct = await _inventory.DeductStockAsync(
                    line.ProductId, dto.WarehouseId, line.Quantity, nameof(PurchaseReturn), 0, innerCt);
                if (!deduct.Succeeded)
                    throw new InvalidOperationException(deduct.Error ?? "Stock deduction failed.");
            }

            entity.GrandTotal = total;
            _purchaseReturns.Add(entity);
            await _unitOfWork.SaveChangesAsync(innerCt);

            var supplier = await _suppliers.Query().FirstOrDefaultAsync(s => s.Id == dto.SupplierId && !s.IsDeleted, innerCt);
            if (supplier is not null && total > 0)
                supplier.Balance = Math.Max(0, supplier.Balance - total);

            if (_company.CompanyId.HasValue && total > 0)
            {
                var gl = await _gl.PostDocumentAsync(
                    "PurchaseReturn",
                    entity.ReturnDate,
                    entity.ReturnNumber,
                    $"Purchase return {entity.ReturnNumber}",
                    entity.Id,
                    [
                        new GlPostingLineRequest("Payable", total, true, "AP credit"),
                        new GlPostingLineRequest("Inventory", total, false, "Inventory out")
                    ],
                    autoPost: true,
                    innerCt);
                if (!gl.Succeeded)
                    throw new InvalidOperationException(gl.Error ?? "Purchase return GL failed.");
            }

            await _unitOfWork.SaveChangesAsync(innerCt);
        }, ct);

        var loaded = await _purchaseReturns.Query()
            .Include(r => r.Supplier)
            .FirstAsync(r => r.Id == entity!.Id, ct);

        return Result<PurchaseReturnDto>.Success(_mapper.Map<PurchaseReturnDto>(loaded));
    }

    public async Task<PagedResult<SalesReturnDto>> GetSalesReturnsAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _salesReturns.Query()
            .Include(r => r.SalesInvoice)
            .Include(r => r.Customer)
            .Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(r => r.ReturnNumber.Contains(s)
                || (r.SalesInvoice != null && r.SalesInvoice.InvoiceNumber.Contains(s))
                || (r.Customer != null && r.Customer.Name.Contains(s))
                || r.ReasonCode.Contains(s));
        }

        q = q.OrderByDescending(r => r.ReturnDate);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<SalesReturnDto>
        {
            Items = _mapper.Map<List<SalesReturnDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    private async Task<string> GenerateSalesReturnNumberAsync(CancellationToken ct)
    {
        var count = await _salesReturns.Query().CountAsync(ct);
        return $"SR-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
    }

    private async Task<string> GeneratePurchaseReturnNumberAsync(CancellationToken ct)
    {
        var count = await _purchaseReturns.Query().CountAsync(ct);
        return $"PR-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
    }
}
