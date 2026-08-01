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

/// <summary>Sales and purchase returns / credit notes with GL and apply-to-invoice.</summary>
public class ReturnService : IReturnService
{
    private readonly IRepository<SalesReturn> _salesReturns;
    private readonly IRepository<PurchaseReturn> _purchaseReturns;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<PurchaseInvoice> _purchaseInvoices;
    private readonly IRepository<CreditNoteApplication> _cnApps;
    private readonly IRepository<PurchaseCreditNoteApplication> _pcnApps;
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
        IRepository<PurchaseInvoice> purchaseInvoices,
        IRepository<CreditNoteApplication> cnApps,
        IRepository<PurchaseCreditNoteApplication> pcnApps,
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
        _purchaseInvoices = purchaseInvoices;
        _cnApps = cnApps;
        _pcnApps = pcnApps;
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
        if (dto.StockAffected && dto.WarehouseId <= 0)
            return Result<SalesReturnDto>.Failure("Warehouse is required when stock is affected.");

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
                CreditNoteNumber = await GenerateCreditNoteNumberAsync("CN", innerCt),
                SalesInvoiceId = dto.SalesInvoiceId,
                CustomerId = dto.CustomerId ?? invoice?.CustomerId,
                ReturnType = dto.ReturnType,
                Notes = dto.Notes,
                ReasonCode = dto.ReasonCode.Trim(),
                StockAffected = dto.StockAffected,
                Status = ReturnStatus.Completed,
                ReturnDate = DateTime.UtcNow
            };

            decimal netTotal = 0;
            decimal taxTotal = 0;
            foreach (var line in dto.Lines)
            {
                var taxRate = line.TaxRate;
                if (taxRate <= 0 && invoice is not null)
                {
                    var invLine = invoice.Lines.FirstOrDefault(l => l.ProductId == line.ProductId);
                    if (invLine is not null) taxRate = invLine.TaxRate;
                }

                var lineNet = line.Quantity * line.UnitPrice;
                var lineTax = Math.Round(lineNet * taxRate / 100m, 2);
                netTotal += lineNet;
                taxTotal += lineTax;
                entity.Lines.Add(new SalesReturnLine
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TaxRate = taxRate,
                    TaxAmount = lineTax,
                    LineTotal = lineNet + lineTax
                });

                if (dto.StockAffected)
                {
                    var restock = await _inventory.ReturnStockAsync(
                        line.ProductId, dto.WarehouseId, line.Quantity, nameof(SalesReturn), 0, innerCt);
                    if (!restock.Succeeded)
                        throw new InvalidOperationException(restock.Error ?? "Restock failed.");
                }
            }

            entity.TaxAmount = taxTotal;
            entity.GrandTotal = netTotal + taxTotal;
            _salesReturns.Add(entity);
            await _unitOfWork.SaveChangesAsync(innerCt);

            if (entity.CustomerId is int cid && entity.GrandTotal > 0)
            {
                var customer = await _customers.Query().FirstOrDefaultAsync(c => c.Id == cid && !c.IsDeleted, innerCt);
                if (customer is not null)
                    customer.Balance = Math.Max(0, customer.Balance - entity.GrandTotal);
            }

            if (_company.CompanyId.HasValue && entity.GrandTotal > 0)
            {
                var paidCash = invoice?.Payments.Any(p => p.PaymentMethod.Contains("cash", StringComparison.OrdinalIgnoreCase)) == true;
                var refundKey = paidCash || invoice is null ? "Cash" :
                    invoice.Payments.Any(p => p.PaymentMethod.Contains("bank", StringComparison.OrdinalIgnoreCase) || p.PaymentMethod.Contains("card", StringComparison.OrdinalIgnoreCase))
                        ? "Bank" : "Receivable";

                var glLines = new List<GlPostingLineRequest>
                {
                    new("Revenue", netTotal > 0 ? netTotal : entity.GrandTotal, true, "Sales return"),
                };
                if (taxTotal > 0)
                    glLines.Add(new("Tax", taxTotal, true, "Tax reverse"));
                glLines.Add(new(refundKey, entity.GrandTotal, false, "Refund/AR"));

                var gl = await _gl.PostDocumentAsync(
                    "SalesReturn",
                    entity.ReturnDate,
                    entity.CreditNoteNumber ?? entity.ReturnNumber,
                    $"CN {entity.CreditNoteNumber}",
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

    public async Task<Result> ApplySalesCreditAsync(int salesReturnId, ApplyCreditNoteRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            return Result.Failure("Amount must be positive.");

        var cn = await _salesReturns.Query().FirstOrDefaultAsync(r => r.Id == salesReturnId && !r.IsDeleted, ct);
        if (cn is null) return Result.Failure("Credit note not found.");

        var unapplied = cn.GrandTotal - cn.AppliedAmount;
        if (request.Amount > unapplied + 0.01m)
            return Result.Failure($"Amount exceeds unapplied credit ({unapplied:N2}).");

        var invoice = await _invoices.Query()
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.SalesInvoiceId && !i.IsDeleted, ct);
        if (invoice is null) return Result.Failure("Sales invoice not found.");

        var paid = invoice.Payments.Sum(p => p.Amount);
        var priorCredit = await _cnApps.Query()
            .Where(a => a.SalesInvoiceId == invoice.Id && !a.IsDeleted)
            .SumAsync(a => a.Amount, ct);
        var outstanding = invoice.GrandTotal - paid - priorCredit;
        if (request.Amount > outstanding + 0.01m)
            return Result.Failure($"Amount exceeds invoice outstanding ({outstanding:N2}).");

        _cnApps.Add(new CreditNoteApplication
        {
            SalesReturnId = cn.Id,
            SalesInvoiceId = invoice.Id,
            Amount = request.Amount,
            Notes = request.Notes,
            AppliedAt = DateTime.UtcNow
        });
        cn.AppliedAmount += request.Amount;

        if (cn.CustomerId is int cid)
        {
            // Balance already reduced on CN create; applying allocates AR credit — no further balance change
        }

        if (outstanding - request.Amount <= 0.01m)
            invoice.PaymentStatus = PaymentStatus.Paid;
        else if (paid + priorCredit + request.Amount > 0)
            invoice.PaymentStatus = PaymentStatus.Partial;

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<PurchaseReturnDto>> CreatePurchaseReturnAsync(PurchaseReturnCreateDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0)
            return Result<PurchaseReturnDto>.Failure("At least one line is required.");
        if (string.IsNullOrWhiteSpace(dto.ReasonCode))
            return Result<PurchaseReturnDto>.Failure("Reason code is required.");
        if (dto.StockAffected && dto.WarehouseId <= 0)
            return Result<PurchaseReturnDto>.Failure("Warehouse is required when stock is affected.");

        if (!await _suppliers.ExistsAsync(s => s.Id == dto.SupplierId && !s.IsDeleted, ct))
            return Result<PurchaseReturnDto>.Failure("Supplier not found.");

        PurchaseReturn? entity = null;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            entity = new PurchaseReturn
            {
                ReturnNumber = await GeneratePurchaseReturnNumberAsync(innerCt),
                CreditNoteNumber = await GenerateCreditNoteNumberAsync("SCN", innerCt),
                SupplierId = dto.SupplierId,
                PurchaseOrderId = dto.PurchaseOrderId,
                WarehouseId = dto.WarehouseId,
                Notes = dto.Notes,
                ReasonCode = dto.ReasonCode.Trim(),
                StockAffected = dto.StockAffected,
                Status = ReturnStatus.Completed,
                ReturnDate = DateTime.UtcNow
            };

            decimal netTotal = 0;
            decimal taxTotal = 0;
            foreach (var line in dto.Lines)
            {
                var lineNet = line.Quantity * line.UnitPrice;
                var lineTax = Math.Round(lineNet * line.TaxRate / 100m, 2);
                netTotal += lineNet;
                taxTotal += lineTax;
                entity.Lines.Add(new PurchaseReturnLine
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TaxRate = line.TaxRate,
                    TaxAmount = lineTax,
                    LineTotal = lineNet + lineTax
                });

                if (dto.StockAffected)
                {
                    var deduct = await _inventory.DeductStockAsync(
                        line.ProductId, dto.WarehouseId, line.Quantity, nameof(PurchaseReturn), 0, innerCt);
                    if (!deduct.Succeeded)
                        throw new InvalidOperationException(deduct.Error ?? "Stock deduction failed.");
                }
            }

            entity.TaxAmount = taxTotal;
            entity.GrandTotal = netTotal + taxTotal;
            _purchaseReturns.Add(entity);
            await _unitOfWork.SaveChangesAsync(innerCt);

            var supplier = await _suppliers.Query().FirstOrDefaultAsync(s => s.Id == dto.SupplierId && !s.IsDeleted, innerCt);
            if (supplier is not null && entity.GrandTotal > 0)
                supplier.Balance = Math.Max(0, supplier.Balance - entity.GrandTotal);

            if (_company.CompanyId.HasValue && entity.GrandTotal > 0)
            {
                var glLines = new List<GlPostingLineRequest>
                {
                    new("Payable", entity.GrandTotal, true, "AP credit")
                };
                if (dto.StockAffected)
                    glLines.Add(new("Inventory", netTotal > 0 ? netTotal : entity.GrandTotal, false, "Inventory out"));
                else
                    glLines.Add(new("Inventory", entity.GrandTotal, false, "Purchase credit (no stock)"));
                if (taxTotal > 0 && dto.StockAffected)
                {
                    // Tax already in grand via payable; inventory is net — add tax credit to Tax account for balance
                    glLines.RemoveAt(glLines.Count - 1);
                    glLines.Add(new("Inventory", netTotal, false, "Inventory out"));
                    glLines.Add(new("Tax", taxTotal, false, "Input tax reverse"));
                }

                var gl = await _gl.PostDocumentAsync(
                    "PurchaseReturn",
                    entity.ReturnDate,
                    entity.CreditNoteNumber ?? entity.ReturnNumber,
                    $"SCN {entity.CreditNoteNumber}",
                    entity.Id,
                    glLines,
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

    public async Task<Result> ApplyPurchaseCreditAsync(int purchaseReturnId, ApplyPurchaseCreditRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            return Result.Failure("Amount must be positive.");

        var cn = await _purchaseReturns.Query().FirstOrDefaultAsync(r => r.Id == purchaseReturnId && !r.IsDeleted, ct);
        if (cn is null) return Result.Failure("Supplier credit note not found.");

        var unapplied = cn.GrandTotal - cn.AppliedAmount;
        if (request.Amount > unapplied + 0.01m)
            return Result.Failure($"Amount exceeds unapplied credit ({unapplied:N2}).");

        var invoice = await _purchaseInvoices.Query()
            .FirstOrDefaultAsync(i => i.Id == request.PurchaseInvoiceId && !i.IsDeleted, ct);
        if (invoice is null) return Result.Failure("Purchase invoice not found.");
        if (invoice.SupplierId != cn.SupplierId)
            return Result.Failure("Credit note supplier does not match invoice.");

        var priorCredit = await _pcnApps.Query()
            .Where(a => a.PurchaseInvoiceId == invoice.Id && !a.IsDeleted)
            .SumAsync(a => a.Amount, ct);
        var outstanding = invoice.GrandTotal - priorCredit;
        if (request.Amount > outstanding + 0.01m)
            return Result.Failure($"Amount exceeds invoice outstanding ({outstanding:N2}).");

        _pcnApps.Add(new PurchaseCreditNoteApplication
        {
            PurchaseReturnId = cn.Id,
            PurchaseInvoiceId = invoice.Id,
            Amount = request.Amount,
            Notes = request.Notes,
            AppliedAt = DateTime.UtcNow
        });
        cn.AppliedAmount += request.Amount;
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
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
                || (r.CreditNoteNumber != null && r.CreditNoteNumber.Contains(s))
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

    private async Task<string> GenerateCreditNoteNumberAsync(string prefix, CancellationToken ct)
    {
        var salesCount = await _salesReturns.Query().CountAsync(ct);
        var purchaseCount = await _purchaseReturns.Query().CountAsync(ct);
        var n = prefix == "CN" ? salesCount : purchaseCount;
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{(n + 1):D4}";
    }
}
