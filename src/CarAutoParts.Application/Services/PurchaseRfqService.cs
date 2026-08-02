using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Thin purchasing RFQ workflow: create RFQ, collect vendor quotes, compare, link/create a PO.</summary>
public interface IPurchaseRfqService
{
    Task<PagedResult<PurchaseRfqDto>> GetAllAsync(QuerySpec? query = null, CancellationToken ct = default);
    Task<PurchaseRfqDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<PurchaseRfqDto>> CreateAsync(CreatePurchaseRfqRequest request, CancellationToken ct = default);
    Task<Result<PurchaseRfqDto>> SendAsync(int id, CancellationToken ct = default);
    Task<Result<PurchaseRfqDto>> CancelAsync(int id, CancellationToken ct = default);
    Task<Result<VendorQuoteDto>> AddVendorQuoteAsync(int rfqId, CreateVendorQuoteRequest request, CancellationToken ct = default);
    Task<Result<VendorQuoteDto>> SelectVendorQuoteAsync(int quoteId, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> CreatePoFromQuoteAsync(int quoteId, CancellationToken ct = default);
}

public sealed class PurchaseRfqService : IPurchaseRfqService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;
    private readonly IPurchaseOrderService _purchaseOrders;

    public PurchaseRfqService(IEnterpriseDb db, ICurrentCompanyContext company, IPurchaseOrderService purchaseOrders)
    {
        _db = db;
        _company = company;
        _purchaseOrders = purchaseOrders;
    }

    public async Task<PagedResult<PurchaseRfqDto>> GetAllAsync(QuerySpec? query = null, CancellationToken ct = default)
    {
        query ??= new QuerySpec();
        var q = IncludeAll(_db.PurchaseRfqs.AsNoTracking())
            .OrderByDescending(r => r.RfqDate)
            .ThenByDescending(r => r.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(r => r.RfqNumber.Contains(s));
        }

        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<PurchaseRfqDto>
        {
            Items = paged.Items.Select(Map).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<PurchaseRfqDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await LoadAsync(id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<Result<PurchaseRfqDto>> CreateAsync(CreatePurchaseRfqRequest request, CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result<PurchaseRfqDto>.Failure("Company context is required.");
        if (request.Lines.Count == 0)
            return Result<PurchaseRfqDto>.Failure("RFQ must have at least one line.");

        var companyId = _company.CompanyId.Value;
        var number = await EnterpriseDocumentNumbers.AllocateAsync(_db, "RFQ", ct);

        var entity = new PurchaseRfq
        {
            CompanyId = companyId,
            RfqNumber = number,
            Status = PurchaseRfqStatus.Draft,
            RfqDate = DateTime.UtcNow,
            ResponseDeadline = request.ResponseDeadline,
            Notes = request.Notes
        };

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                return Result<PurchaseRfqDto>.Failure("Line quantity must be positive.");
            if (!await _db.Products.AnyAsync(p => p.Id == line.ProductId && !p.IsDeleted, ct))
                return Result<PurchaseRfqDto>.Failure($"Product {line.ProductId} not found.");

            entity.Lines.Add(new PurchaseRfqLine
            {
                CompanyId = companyId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                Notes = line.Notes
            });
        }

        _db.PurchaseRfqs.Add(entity);
        await _db.SaveChangesAsync(ct);
        var loaded = await LoadAsync(entity.Id, ct);
        return Result<PurchaseRfqDto>.Success(Map(loaded!));
    }

    public async Task<Result<PurchaseRfqDto>> SendAsync(int id, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, ct);
        if (entity is null) return Result<PurchaseRfqDto>.Failure("RFQ not found.");
        if (entity.Status != PurchaseRfqStatus.Draft)
            return Result<PurchaseRfqDto>.Failure("Only draft RFQs can be sent.");

        entity.Status = PurchaseRfqStatus.Sent;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<PurchaseRfqDto>.Success(Map(entity));
    }

    public async Task<Result<PurchaseRfqDto>> CancelAsync(int id, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, ct);
        if (entity is null) return Result<PurchaseRfqDto>.Failure("RFQ not found.");
        if (entity.Status == PurchaseRfqStatus.Closed)
            return Result<PurchaseRfqDto>.Failure("Closed RFQs cannot be cancelled.");

        entity.Status = PurchaseRfqStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<PurchaseRfqDto>.Success(Map(entity));
    }

    public async Task<Result<VendorQuoteDto>> AddVendorQuoteAsync(
        int rfqId,
        CreateVendorQuoteRequest request,
        CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result<VendorQuoteDto>.Failure("Company context is required.");

        var rfq = await LoadAsync(rfqId, ct);
        if (rfq is null) return Result<VendorQuoteDto>.Failure("RFQ not found.");
        if (rfq.Status is PurchaseRfqStatus.Closed or PurchaseRfqStatus.Cancelled)
            return Result<VendorQuoteDto>.Failure("RFQ is closed or cancelled.");
        if (request.Lines.Count == 0)
            return Result<VendorQuoteDto>.Failure("Quote must have at least one line.");
        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && !s.IsDeleted, ct))
            return Result<VendorQuoteDto>.Failure("Supplier not found.");

        var companyId = _company.CompanyId.Value;
        var quote = new VendorQuote
        {
            CompanyId = companyId,
            PurchaseRfqId = rfqId,
            SupplierId = request.SupplierId,
            Status = VendorQuoteStatus.Received,
            QuoteDate = DateTime.UtcNow,
            ValidUntil = request.ValidUntil,
            Notes = request.Notes
        };

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0 || line.UnitPrice < 0)
                return Result<VendorQuoteDto>.Failure("Invalid quote line quantity/price.");
            if (!await _db.Products.AnyAsync(p => p.Id == line.ProductId && !p.IsDeleted, ct))
                return Result<VendorQuoteDto>.Failure($"Product {line.ProductId} not found.");

            quote.Lines.Add(new VendorQuoteLine
            {
                CompanyId = companyId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LeadTimeDays = line.LeadTimeDays,
                Notes = line.Notes
            });
        }

        _db.VendorQuotes.Add(quote);
        if (rfq.Status == PurchaseRfqStatus.Sent)
        {
            var tracked = await _db.PurchaseRfqs.FirstAsync(r => r.Id == rfqId, ct);
            tracked.Status = PurchaseRfqStatus.QuotesReceived;
            tracked.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);

        var loaded = await _db.VendorQuotes.AsNoTracking()
            .Include(q => q.Supplier)
            .Include(q => q.Lines).ThenInclude(l => l.Product)
            .FirstAsync(q => q.Id == quote.Id, ct);
        return Result<VendorQuoteDto>.Success(MapQuote(loaded));
    }

    public async Task<Result<VendorQuoteDto>> SelectVendorQuoteAsync(int quoteId, CancellationToken ct = default)
    {
        var quote = await _db.VendorQuotes
            .Include(q => q.Supplier)
            .Include(q => q.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(q => q.Id == quoteId && !q.IsDeleted, ct);
        if (quote is null) return Result<VendorQuoteDto>.Failure("Vendor quote not found.");

        var siblings = await _db.VendorQuotes
            .Where(q => q.PurchaseRfqId == quote.PurchaseRfqId && q.Id != quote.Id && q.Status == VendorQuoteStatus.Selected)
            .ToListAsync(ct);
        foreach (var other in siblings)
            other.Status = VendorQuoteStatus.Received;

        quote.Status = VendorQuoteStatus.Selected;
        quote.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<VendorQuoteDto>.Success(MapQuote(quote));
    }

    public async Task<Result<PurchaseOrderDetailDto>> CreatePoFromQuoteAsync(int quoteId, CancellationToken ct = default)
    {
        var quote = await _db.VendorQuotes
            .Include(q => q.Lines)
            .Include(q => q.PurchaseRfq)
            .FirstOrDefaultAsync(q => q.Id == quoteId && !q.IsDeleted, ct);
        if (quote is null) return Result<PurchaseOrderDetailDto>.Failure("Vendor quote not found.");
        if (quote.Lines.Count == 0) return Result<PurchaseOrderDetailDto>.Failure("Quote has no lines.");

        var lines = quote.Lines
            .Select(l => new PurchaseOrderLineDto(null, l.ProductId, null, l.Quantity, 0, l.UnitPrice, 0, 0, l.Quantity * l.UnitPrice))
            .ToList();

        var create = new PurchaseOrderCreateDto(
            quote.SupplierId,
            null,
            null,
            $"Created from RFQ {quote.PurchaseRfq.RfqNumber} / vendor quote #{quote.Id}",
            0,
            lines);

        var result = await _purchaseOrders.CreateAsync(create, ct);
        if (!result.Succeeded)
            return Result<PurchaseOrderDetailDto>.Failure(result.Error!);

        quote.Status = VendorQuoteStatus.Selected;
        quote.PurchaseRfq.PurchaseOrderId = result.Data!.Id;
        quote.PurchaseRfq.Status = PurchaseRfqStatus.Closed;
        quote.PurchaseRfq.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return result;
    }

    private static IQueryable<PurchaseRfq> IncludeAll(IQueryable<PurchaseRfq> q) => q
        .Include(r => r.Lines).ThenInclude(l => l.Product)
        .Include(r => r.PurchaseOrder)
        .Include(r => r.VendorQuotes).ThenInclude(v => v.Supplier)
        .Include(r => r.VendorQuotes).ThenInclude(v => v.Lines).ThenInclude(l => l.Product);

    private async Task<PurchaseRfq?> LoadAsync(int id, CancellationToken ct) =>
        await IncludeAll(_db.PurchaseRfqs).FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    private static PurchaseRfqDto Map(PurchaseRfq r) => new(
        r.Id,
        r.RfqNumber,
        r.Status,
        r.RfqDate,
        r.ResponseDeadline,
        r.Notes,
        r.PurchaseOrderId,
        r.PurchaseOrder?.OrderNumber,
        r.Lines.Select(l => new PurchaseRfqLineDto(l.Id, l.ProductId, l.Product?.Name, l.Product?.Sku, l.Quantity, l.Notes)).ToList(),
        r.VendorQuotes.Select(MapQuote).ToList());

    private static VendorQuoteDto MapQuote(VendorQuote q) => new(
        q.Id,
        q.PurchaseRfqId,
        q.SupplierId,
        q.Supplier?.Name,
        q.Status,
        q.QuoteDate,
        q.ValidUntil,
        q.Notes,
        q.Lines.Sum(l => l.Quantity * l.UnitPrice),
        q.Lines.Select(l => new VendorQuoteLineDto(
            l.Id, l.ProductId, l.Product?.Name, l.Quantity, l.UnitPrice, l.LeadTimeDays, l.Notes, l.Quantity * l.UnitPrice)).ToList());
}
