using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Services;
using Microsoft.EntityFrameworkCore;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.Enterprise;

public sealed class EnterprisePurchaseService : IEnterprisePurchaseService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;
    private readonly IGlPostingService _glPosting;
    private readonly IOutboxWriter _outbox;
    private readonly IApprovalWorkflowService _approvals;
    private readonly IOpsSlaClockService _opsSla;

    public EnterprisePurchaseService(
        IEnterpriseDb db,
        ICurrentCompanyContext company,
        IGlPostingService glPosting,
        IOutboxWriter outbox,
        IApprovalWorkflowService approvals,
        IOpsSlaClockService opsSla)
    {
        _db = db;
        _company = company;
        _glPosting = glPosting;
        _outbox = outbox;
        _approvals = approvals;
        _opsSla = opsSla;
    }

    public async Task<PagedResult<PurchaseInvoiceDto>> GetPurchaseInvoicesAsync(QuerySpec? query = null, CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();
        query ??= new QuerySpec();

        var baseQ = _db.PurchaseInvoices.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            baseQ = baseQ.Where(i => i.InvoiceNumber.Contains(s));
        }

        var total = await baseQ.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? QueryLimits.DefaultPageSize : query.PageSize, 1, QueryLimits.MaxPageSize);

        var items = await baseQ
            .Include(i => i.Lines)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            
            .ToListAsync(ct);

        return new PagedResult<PurchaseInvoiceDto>
        {
            Items = items.Select(MapInvoice).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Result<PurchaseInvoiceDto>> CreatePurchaseInvoiceAsync(
        CreatePurchaseInvoiceRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<PurchaseInvoiceDto>.Failure(error!);

        if (request.Lines.Count == 0)
            return Result<PurchaseInvoiceDto>.Failure("Invoice must have at least one line.");

        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && !s.IsDeleted, ct))
            return Result<PurchaseInvoiceDto>.Failure("Supplier not found.");

        var invoiceNumber = await EnterpriseDocumentNumbers.AllocateAsync(_db, "PI", ct);
        decimal subTotal = 0;

        var invoice = new PurchaseInvoice
        {
            CompanyId = companyId,
            InvoiceNumber = invoiceNumber,
            SupplierId = request.SupplierId,
            PurchaseOrderId = request.PurchaseOrderId,
            GoodsReceiptNoteId = request.GoodsReceiptNoteId,
            InvoiceDate = request.InvoiceDate,
            Status = PurchaseInvoiceStatus.Draft,
            TaxAmount = request.TaxAmount
        };

        foreach (var line in request.Lines)
        {
            var lineTotal = line.Quantity * line.UnitCost;
            subTotal += lineTotal;
            invoice.Lines.Add(new PurchaseInvoiceLine
            {
                CompanyId = companyId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                LineTotal = lineTotal
            });
        }

        invoice.SubTotal = subTotal;
        invoice.GrandTotal = subTotal + request.TaxAmount;

        _db.PurchaseInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        await _opsSla.OnApInvoiceOpenedAsync(invoice.Id, companyId, ct);
        return Result<PurchaseInvoiceDto>.Success(MapInvoice(invoice));
    }

    public async Task<Result<ThreeWayMatchResultDto>> MatchThreeWayAsync(int purchaseInvoiceId, CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<ThreeWayMatchResultDto>.Failure(error!);

        var invoice = await _db.PurchaseInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == purchaseInvoiceId, ct);

        if (invoice is null)
            return Result<ThreeWayMatchResultDto>.Failure("Purchase invoice not found.");

        if (invoice.GoodsReceiptNoteId is int grnId)
        {
            var grn = await _db.GoodsReceiptNotes.AsNoTracking().FirstOrDefaultAsync(g => g.Id == grnId, ct);
            if (grn is null)
                return Result<ThreeWayMatchResultDto>.Failure("Linked GRN not found.");
            if (grn.Status == GrnStatus.QcHold)
                return Result<ThreeWayMatchResultDto>.Failure("GRN is on QC hold; release QC before matching.");
            if (grn.Status != GrnStatus.Posted)
                return Result<ThreeWayMatchResultDto>.Failure("Linked GRN must be posted before matching.");
        }

        var settings = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var qtyTol = settings?.ThreeWayQtyTolerancePercent ?? 0m;
        var priceTol = settings?.ThreeWayPriceTolerancePercent ?? 0m;

        var poLines = invoice.PurchaseOrderId.HasValue
            ? await _db.PurchaseOrderLines
                .Where(l => l.PurchaseOrderId == invoice.PurchaseOrderId.Value && !l.IsDeleted)
                .ToListAsync(ct)
            : [];

        var grnLines = invoice.GoodsReceiptNoteId.HasValue
            ? await _db.GoodsReceiptLines
                .Where(l => l.GoodsReceiptNoteId == invoice.GoodsReceiptNoteId.Value)
                .ToListAsync(ct)
            : [];

        var matchLines = new List<ThreeWayMatchLineResult>();
        var allMatched = true;

        foreach (var invLine in invoice.Lines)
        {
            var poQty = poLines.Where(l => l.ProductId == invLine.ProductId).Sum(l => l.QuantityOrdered);
            var grnQty = grnLines.Where(l => l.ProductId == invLine.ProductId).Sum(l => l.Quantity);
            var lineMatched = ThreeWayMatchRules.LineMatched(
                invoice.PurchaseOrderId.HasValue,
                poQty,
                grnQty,
                invLine.Quantity,
                qtyTol);

            if (lineMatched && priceTol >= 0)
            {
                var poUnit = poLines.Where(l => l.ProductId == invLine.ProductId).Select(l => (decimal?)l.UnitPrice).FirstOrDefault();
                var grnUnit = grnLines.Where(l => l.ProductId == invLine.ProductId).Select(l => (decimal?)l.UnitCost).FirstOrDefault();
                var expected = poUnit ?? grnUnit ?? invLine.UnitCost;
                if (!ThreeWayMatchRules.PriceMatched(expected, invLine.UnitCost, priceTol))
                    lineMatched = false;
            }

            if (!lineMatched)
                allMatched = false;

            matchLines.Add(new ThreeWayMatchLineResult(
                invLine.ProductId,
                poQty,
                grnQty,
                invLine.Quantity,
                lineMatched));
        }

        invoice.ThreeWayMatched = allMatched;
        invoice.MatchNotes = allMatched
            ? $"Matched within qty tol {qtyTol}% / price tol {priceTol}%."
            : "Quantity or price mismatch detected on one or more lines.";
        invoice.Status = allMatched ? PurchaseInvoiceStatus.Matched : PurchaseInvoiceStatus.Draft;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Result<ThreeWayMatchResultDto>.Success(new ThreeWayMatchResultDto(
            invoice.Id,
            allMatched,
            invoice.MatchNotes,
            matchLines));
    }

    public async Task<Result<PurchaseInvoiceDto>> PostPurchaseInvoiceAsync(int purchaseInvoiceId, CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<PurchaseInvoiceDto>.Failure(error!);

        var invoice = await _db.PurchaseInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == purchaseInvoiceId, ct);

        if (invoice is null)
            return Result<PurchaseInvoiceDto>.Failure("Purchase invoice not found.");

        if (invoice.Status == PurchaseInvoiceStatus.Posted)
            return Result<PurchaseInvoiceDto>.Failure("Invoice is already posted.");

        if (!invoice.ThreeWayMatched)
            return Result<PurchaseInvoiceDto>.Failure("Invoice must pass three-way match before posting.");

        var gate = await _approvals.EnsureApprovedOrQueueAsync(
            "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, invoice.GrandTotal, ct);
        if (!gate.Succeeded)
            return Result<PurchaseInvoiceDto>.Failure(gate.Error ?? "Approval required.");

        var debitKey = invoice.GoodsReceiptNoteId.HasValue ? "GrnClearing" : "Inventory";
        var exTax = invoice.GrandTotal - invoice.TaxAmount;
        if (exTax < 0) exTax = 0;
        var glLines = new List<GlPostingLineRequest>
        {
            new(debitKey, exTax > 0 ? exTax : invoice.GrandTotal, true, $"PI {invoice.InvoiceNumber} {debitKey}"),
            new("Payable", invoice.GrandTotal, false, $"PI {invoice.InvoiceNumber} AP")
        };
        if (invoice.TaxAmount > 0)
            glLines.Insert(1, new("Tax", invoice.TaxAmount, true, $"PI {invoice.InvoiceNumber} Tax"));

        var journalResult = await _glPosting.PostDocumentAsync(
            "PurchaseInvoice",
            invoice.InvoiceDate,
            invoice.InvoiceNumber,
            $"Purchase invoice {invoice.InvoiceNumber}",
            invoice.Id,
            glLines,
            autoPost: true,
            ct);

        if (!journalResult.Succeeded)
            return Result<PurchaseInvoiceDto>.Failure(journalResult.Error!);

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == invoice.SupplierId && !s.IsDeleted, ct);
        if (supplier is not null)
            supplier.Balance += invoice.GrandTotal;

        _outbox.Enqueue("PurchaseInvoicePosted", new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.GrandTotal,
            JournalId = journalResult.Data!.JournalId
        });

        invoice.Status = PurchaseInvoiceStatus.Posted;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _opsSla.OnApInvoiceClosedAsync(invoice.Id, ct);

        return Result<PurchaseInvoiceDto>.Success(MapInvoice(invoice));
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

    private static PurchaseInvoiceDto MapInvoice(PurchaseInvoice i) => new(
        i.Id,
        i.InvoiceNumber,
        i.SupplierId,
        i.PurchaseOrderId,
        i.GoodsReceiptNoteId,
        i.InvoiceDate,
        i.Status,
        i.SubTotal,
        i.TaxAmount,
        i.GrandTotal,
        i.ThreeWayMatched,
        i.MatchNotes,
        i.Lines.Select(l => new PurchaseInvoiceLineDto(l.Id, l.ProductId, l.Quantity, l.UnitCost, l.LineTotal)).ToList());
}
