using System.Text;
using System.Text.Json;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Fbr;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>POS checkout with multi-tender, kits, price lists, shift gate, GL, and FBR.</summary>
public class PosCheckoutService : IPosCheckoutService
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<FbrSubmission> _fbrSubmissions;
    private readonly IRepository<CompanySettings> _settings;
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<ProductKit> _kits;
    private readonly IRepository<CashierShift> _shifts;
    private readonly IRepository<HeldSale> _held;
    private readonly IRepository<ProductSupersession> _supersessions;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IInventoryService _inventory;
    private readonly IFbrService _fbrService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<PosCheckoutDto> _validator;
    private readonly IGlPostingService _gl;
    private readonly IFbrOutboxService _fbrOutbox;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _currentUser;
    private readonly IEnterpriseSalesService _salesEnterprise;
    private readonly IAtpService _atp;
    private readonly IFeatureGate _features;

    public PosCheckoutService(
        IRepository<Product> products,
        IRepository<SalesInvoice> invoices,
        IRepository<FbrSubmission> fbrSubmissions,
        IRepository<CompanySettings> settings,
        IRepository<Payment> payments,
        IRepository<Customer> customers,
        IRepository<ProductKit> kits,
        IRepository<CashierShift> shifts,
        IRepository<HeldSale> held,
        IRepository<ProductSupersession> supersessions,
        IRepository<Warehouse> warehouses,
        IInventoryService inventory,
        IFbrService fbrService,
        IUnitOfWork unitOfWork,
        IValidator<PosCheckoutDto> validator,
        IGlPostingService gl,
        IFbrOutboxService fbrOutbox,
        ICurrentCompanyContext company,
        ICurrentUserService currentUser,
        IEnterpriseSalesService salesEnterprise,
        IAtpService atp,
        IFeatureGate features)
    {
        _products = products;
        _invoices = invoices;
        _fbrSubmissions = fbrSubmissions;
        _settings = settings;
        _payments = payments;
        _customers = customers;
        _kits = kits;
        _shifts = shifts;
        _held = held;
        _supersessions = supersessions;
        _warehouses = warehouses;
        _inventory = inventory;
        _fbrService = fbrService;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _gl = gl;
        _fbrOutbox = fbrOutbox;
        _company = company;
        _currentUser = currentUser;
        _salesEnterprise = salesEnterprise;
        _atp = atp;
        _features = features;
    }

    /// <summary>
    /// POS catalog search. Exact SKU/barcode/OEM/part uses indexed equality (no full scan);
    /// contains/fitment is a capped fallback. Smoke: p95 &lt;1s mindset on 50k SKU with exact scanner codes.
    /// </summary>
    public Task<IReadOnlyList<PosProductDto>> GetPosProductsAsync(
        string? search = null,
        string? make = null,
        string? model = null,
        int? year = null,
        CancellationToken ct = default) =>
        GetPosProductsCoreAsync(new PosProductSearchQuery(search, make, model, year), ct);

    private async Task<IReadOnlyList<PosProductDto>> GetPosProductsCoreAsync(PosProductSearchQuery query, CancellationToken ct)
    {
        var oemField = await _features.GetFieldAsync(ConfigKeys.FieldProductOem, ct);
        var partField = await _features.GetFieldAsync(ConfigKeys.FieldProductPartNumber, ct);
        var fitmentSearch = await _features.BehaviorEnabledAsync(ConfigKeys.BehFitmentSearch, ct);
        var supersessionOn = await _features.BehaviorEnabledAsync(ConfigKeys.BehSupersession, ct);
        var fitmentField = await _features.GetFieldAsync(ConfigKeys.FieldProductFitment, ct);

        var make = fitmentSearch && !string.IsNullOrWhiteSpace(query.Make) ? query.Make.Trim() : null;
        var model = fitmentSearch && !string.IsNullOrWhiteSpace(query.Model) ? query.Model.Trim() : null;
        int? fitYear = fitmentSearch && query.Year is >= 1980 and <= 2100 ? query.Year : null;
        var hasFitmentFilter = make is not null || model is not null || fitYear is not null;

        var search = CatalogSearchNormalizer.NormalizePaste(query.Search);
        var candidates = CatalogSearchNormalizer.BuildExactCandidates(search);

        if (candidates.Count > 0 && !hasFitmentFilter)
        {
            // Hot path: equality on indexed columns — prefer exact barcode/SKU/OEM/part before fuzzy Contains.
            var exact = await _products.Query()
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive &&
                    (candidates.Contains(p.Sku) ||
                     (p.Barcode != null && candidates.Contains(p.Barcode)) ||
                     (oemField.Visible && p.OemNumber != null && candidates.Contains(p.OemNumber)) ||
                     (partField.Visible && p.PartNumber != null && candidates.Contains(p.PartNumber))))
                .OrderBy(p => p.Name)
                .Take(QueryLimits.PosExactMatchTake)
                .Select(p => new PosRow(
                    p.Id, p.Name, p.Sku, p.Barcode, p.SalePrice, p.TaxRatePercent, p.Unit, p.HsCode,
                    p.InventoryItems.Where(i => !i.IsDeleted).Sum(i => i.QuantityOnHand - i.ReservedQuantity),
                    p.OemNumber, p.PartNumber))
                .ToListAsync(ct);

            if (exact.Count > 0)
                return await MapPosRowsAsync(exact, oemField.Visible, partField.Visible, supersessionOn, fitmentField.Visible && fitmentSearch, exactMatch: true, ct);

            HashSet<int>? crossRefIds = null;
            if (supersessionOn)
            {
                var oldMatches = await _products.Query()
                    .AsNoTracking()
                    .Where(p => !p.IsDeleted && (
                        candidates.Contains(p.Sku) ||
                        (oemField.Visible && p.OemNumber != null && candidates.Contains(p.OemNumber)) ||
                        (partField.Visible && p.PartNumber != null && candidates.Contains(p.PartNumber))))
                    .Select(p => p.Id)
                    .ToListAsync(ct);
                if (oldMatches.Count > 0)
                {
                    var newIds = await _supersessions.Query()
                        .AsNoTracking()
                        .Where(x => !x.IsDeleted && oldMatches.Contains(x.OldProductId))
                        .Select(x => x.NewProductId)
                        .ToListAsync(ct);
                    if (newIds.Count > 0)
                        crossRefIds = newIds.ToHashSet();
                }
            }

            int? yearToken = int.TryParse(search, out var y) && y is >= 1980 and <= 2100 ? y : null;

            // Fallback: Contains / fitment — capped Take(100).
            var soft = await _products.Query()
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive)
                .Where(p =>
                    (crossRefIds != null && crossRefIds.Contains(p.Id)) ||
                    p.Name.Contains(search) ||
                    p.Sku.Contains(search) ||
                    (p.Barcode != null && p.Barcode.Contains(search)) ||
                    (oemField.Visible && p.OemNumber != null && p.OemNumber.Contains(search)) ||
                    (partField.Visible && p.PartNumber != null && p.PartNumber.Contains(search)) ||
                    (fitmentSearch && p.VehicleCompatibilities.Any(v =>
                        v.Make.Contains(search) ||
                        v.Model.Contains(search) ||
                        (yearToken != null && (v.YearFrom == null || v.YearFrom <= yearToken) && (v.YearTo == null || v.YearTo >= yearToken)))))
                .OrderBy(p => p.Name)
                .Take(QueryLimits.PosSoftSearchTake)
                .Select(p => new PosRow(
                    p.Id, p.Name, p.Sku, p.Barcode, p.SalePrice, p.TaxRatePercent, p.Unit, p.HsCode,
                    p.InventoryItems.Where(i => !i.IsDeleted).Sum(i => i.QuantityOnHand - i.ReservedQuantity),
                    p.OemNumber, p.PartNumber))
                .ToListAsync(ct);

            return await MapPosRowsAsync(soft, oemField.Visible, partField.Visible, supersessionOn, fitmentField.Visible && fitmentSearch, exactMatch: false, ct);
        }

        // Browse or fitment-picker filter (no text / with make-model-year).
        var browseQ = _products.Query()
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive);

        if (hasFitmentFilter)
        {
            browseQ = browseQ.Where(p => p.VehicleCompatibilities.Any(v =>
                (make == null || v.Make == make) &&
                (model == null || v.Model == model) &&
                (fitYear == null || ((v.YearFrom == null || v.YearFrom <= fitYear) && (v.YearTo == null || v.YearTo >= fitYear)))));
        }

        if (!string.IsNullOrEmpty(search))
        {
            browseQ = browseQ.Where(p =>
                p.Name.Contains(search) ||
                p.Sku.Contains(search) ||
                (p.Barcode != null && p.Barcode.Contains(search)) ||
                (oemField.Visible && p.OemNumber != null && p.OemNumber.Contains(search)) ||
                (partField.Visible && p.PartNumber != null && p.PartNumber.Contains(search)));
        }

        var browse = await browseQ
            .OrderBy(p => p.Name)
            .Take(QueryLimits.PosSoftSearchTake)
            .Select(p => new PosRow(
                p.Id, p.Name, p.Sku, p.Barcode, p.SalePrice, p.TaxRatePercent, p.Unit, p.HsCode,
                p.InventoryItems.Where(i => !i.IsDeleted).Sum(i => i.QuantityOnHand - i.ReservedQuantity),
                p.OemNumber, p.PartNumber))
            .ToListAsync(ct);

        return await MapPosRowsAsync(browse, oemField.Visible, partField.Visible, supersessionOn, fitmentField.Visible && fitmentSearch, exactMatch: false, ct);
    }

    /// <inheritdoc />
    public async Task<FitmentOptionsDto> GetFitmentOptionsAsync(string? make = null, CancellationToken ct = default)
    {
        if (!await _features.BehaviorEnabledAsync(ConfigKeys.BehFitmentSearch, ct))
            return new FitmentOptionsDto(Array.Empty<string>(), Array.Empty<string>());

        var compat = _products.Query()
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsActive)
            .SelectMany(p => p.VehicleCompatibilities.Where(v => !v.IsDeleted));

        var makes = await compat
            .Select(v => v.Make)
            .Distinct()
            .OrderBy(m => m)
            .Take(500)
            .ToListAsync(ct);

        IReadOnlyList<string> models = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(make))
        {
            var m = make.Trim();
            models = await compat
                .Where(v => v.Make == m)
                .Select(v => v.Model)
                .Distinct()
                .OrderBy(x => x)
                .Take(500)
                .ToListAsync(ct);
        }

        return new FitmentOptionsDto(makes, models);
    }

    private async Task<IReadOnlyList<PosProductDto>> MapPosRowsAsync(
        List<PosRow> rows,
        bool showOem,
        bool showPart,
        bool supersessionOn,
        bool showFitment,
        bool exactMatch,
        CancellationToken ct)
    {
        if (rows.Count == 0)
            return Array.Empty<PosProductDto>();

        Dictionary<int, string>? supersededBy = null;
        Dictionary<int, string>? supersedes = null;
        Dictionary<int, string>? fitment = null;

        var ids = rows.Select(r => r.Id).ToList();

        if (supersessionOn)
        {
            var links = await _supersessions.Query()
                .AsNoTracking()
                .Where(x => !x.IsDeleted && (ids.Contains(x.OldProductId) || ids.Contains(x.NewProductId)))
                .Select(x => new
                {
                    x.OldProductId,
                    x.NewProductId,
                    OldSku = x.OldProduct.Sku,
                    NewSku = x.NewProduct.Sku
                })
                .ToListAsync(ct);

            supersededBy = links
                .Where(l => ids.Contains(l.OldProductId))
                .GroupBy(l => l.OldProductId)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.NewSku).Distinct()));

            supersedes = links
                .Where(l => ids.Contains(l.NewProductId))
                .GroupBy(l => l.NewProductId)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.OldSku).Distinct()));
        }

        if (showFitment)
        {
            var fits = await _products.Query()
                .AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    Lines = p.VehicleCompatibilities
                        .Where(v => !v.IsDeleted)
                        .OrderBy(v => v.Make).ThenBy(v => v.Model)
                        .Take(3)
                        .Select(v => v.Make + " " + v.Model +
                            (v.YearFrom != null || v.YearTo != null
                                ? " " + (v.YearFrom != null ? v.YearFrom.ToString() : "") +
                                  (v.YearTo != null && v.YearTo != v.YearFrom ? "-" + v.YearTo : "")
                                : ""))
                        .ToList()
                })
                .ToListAsync(ct);
            fitment = fits.ToDictionary(x => x.Id, x => string.Join("; ", x.Lines));
        }

        return rows.Select(p => new PosProductDto(
            p.Id, p.Name, p.Sku, p.Barcode, p.SalePrice, p.TaxRatePercent, p.Unit, p.HsCode,
            p.Stock,
            showOem ? p.OemNumber : null,
            showPart ? p.PartNumber : null,
            exactMatch,
            supersessionOn && supersedes is not null && supersedes.TryGetValue(p.Id, out var olds) ? olds : null,
            supersessionOn && supersededBy is not null && supersededBy.TryGetValue(p.Id, out var news) ? news : null,
            fitment is not null && fitment.TryGetValue(p.Id, out var fs) && !string.IsNullOrWhiteSpace(fs) ? fs : null
        )).ToList();
    }

    private sealed record PosRow(
        int Id, string Name, string Sku, string? Barcode, decimal SalePrice, decimal TaxRatePercent,
        string Unit, string? HsCode, decimal Stock, string? OemNumber, string? PartNumber);

    public async Task<PosCheckoutResultDto> CheckoutAsync(PosCheckoutDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var warehouse = await _warehouses.Query().FirstOrDefaultAsync(w => w.Id == dto.WarehouseId && !w.IsDeleted, ct)
            ?? throw new InvalidOperationException("Warehouse not found.");
        if (warehouse.BranchId is int branchId && !_company.IsBranchAllowed(branchId))
            throw new InvalidOperationException("Warehouse branch is not allowed for this user.");

        // Authenticated cashiers must have an open shift; in-process tests without user skip the gate.
        CashierShift? shift = null;
        if (_currentUser.CurrentUser is not null)
        {
            shift = await ResolveOpenShiftAsync(dto.ShiftId, ct)
                ?? throw new InvalidOperationException("Open a cashier shift before checkout.");
        }

        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            var existing = await _invoices.Query()
                .Include(i => i.FbrSubmission)
                .FirstOrDefaultAsync(i => i.IdempotencyKey == dto.IdempotencyKey && !i.IsDeleted, ct);
            if (existing is not null)
            {
                return new PosCheckoutResultDto(
                    existing.Id, existing.InvoiceNumber, existing.FbrSubmission?.FbrInvoiceNumber,
                    existing.FbrSubmission?.Status is FbrSubmissionStatus.Success or FbrSubmissionStatus.Stub,
                    existing.FbrSubmission?.Status == FbrSubmissionStatus.Stub,
                    existing.FbrSubmission?.ErrorMessage,
                    existing.GrandTotal, existing.TaxAmount, existing.SubTotal,
                    existing.ChangeDue, existing.PaymentStatus.ToString());
            }
        }

        var settings = await _settings.Query().FirstOrDefaultAsync(s => !s.IsDeleted, ct) ?? new CompanySettings();
        var taxEnabled = await _features.BehaviorEnabledAsync(ConfigKeys.BehTaxEnabled, ct);
        var invoiceNumber = await GenerateInvoiceNumberAsync(settings, ct);
        SalesInvoice? invoice = null;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            invoice = new SalesInvoice
            {
                InvoiceNumber = invoiceNumber,
                PosReference = $"POS-{DateTime.UtcNow:yyyyMMddHHmmss}",
                IdempotencyKey = string.IsNullOrWhiteSpace(dto.IdempotencyKey) ? null : dto.IdempotencyKey.Trim(),
                CustomerId = dto.CustomerId,
                InvoiceDate = DateTime.UtcNow,
                WarehouseId = dto.WarehouseId,
                PaymentStatus = PaymentStatus.Paid,
                BuyerName = dto.Buyer?.Name ?? "Walk-in Customer",
                BuyerNtnCnic = dto.Buyer?.NtnCnic,
                BuyerProvince = dto.Buyer?.Province,
                BuyerAddress = dto.Buyer?.Address,
                BuyerRegistrationType = dto.Buyer?.RegistrationType ?? "Unregistered",
                DiscountAmount = dto.DiscountAmount,
                CashierShiftId = shift?.Id
            };

            decimal subTotal = 0, taxTotal = 0, cogsTotal = 0;
            var lineItems = new List<(Product Product, PosCheckoutLineDto Line, decimal UnitPrice, decimal LineTax, decimal LineTotal, decimal TaxRate)>();

            foreach (var line in dto.Lines)
            {
                var product = await _products.Query()
                    .Include(p => p.InventoryItems)
                    .FirstOrDefaultAsync(p => p.Id == line.ProductId && !p.IsDeleted && p.IsActive, innerCt)
                    ?? throw new InvalidOperationException($"Product {line.ProductId} is not available.");

                var unitPrice = await ResolveUnitPriceAsync(product, line, dto.CustomerId, innerCt);
                await EnsureStockForSaleAsync(product, dto.WarehouseId, line.Quantity, innerCt);

                var taxRate = !taxEnabled
                    ? 0m
                    : product.TaxRatePercent > 0
                        ? product.TaxRatePercent
                        : settings.DefaultTaxRate;
                var lineSub = unitPrice * line.Quantity - line.DiscountAmount;
                var lineTax = lineSub * taxRate / 100m;
                subTotal += lineSub;
                taxTotal += lineTax;
                lineItems.Add((product, line, unitPrice, lineTax, lineSub + lineTax, taxRate));
            }

            invoice.SubTotal = subTotal;
            invoice.TaxAmount = taxTotal;
            invoice.GrandTotal = subTotal + taxTotal - dto.DiscountAmount;

            foreach (var (product, line, unitPrice, lineTax, lineTotal, taxRate) in lineItems)
            {
                invoice.Lines.Add(new SalesInvoiceLine
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Quantity = line.Quantity,
                    UnitPrice = unitPrice,
                    TaxRate = taxRate,
                    TaxAmount = lineTax,
                    LineTotal = lineTotal,
                    HsCode = product.HsCode,
                    UnitOfMeasure = product.Unit
                });
            }

            _invoices.Add(invoice);
            await _unitOfWork.SaveChangesAsync(innerCt);

            var lineList = invoice.Lines.ToList();
            for (var i = 0; i < lineItems.Count; i++)
            {
                var (product, line, _, _, _, _) = lineItems[i];
                var cogs = await DeductForSaleAsync(product, dto.WarehouseId, line.Quantity, invoice.Id, innerCt);
                var unitCost = line.Quantity > 0 ? cogs / line.Quantity : 0;
                lineList[i].UnitCost = unitCost;
                cogsTotal += cogs;
            }
            await _unitOfWork.SaveChangesAsync(innerCt);

            var tenders = BuildTenders(dto, invoice.GrandTotal);
            var (recorded, changeDue, creditAmount, status) = await ApplyTendersAsync(invoice, tenders, innerCt);
            invoice.ChangeDue = changeDue;
            invoice.PaymentStatus = status;
            await _unitOfWork.SaveChangesAsync(innerCt);

            if (_company.CompanyId.HasValue && invoice.GrandTotal > 0)
                await PostSalesGlAsync(invoice, recorded, creditAmount, cogsTotal, innerCt);

            if (dto.HeldSaleId is int heldId)
            {
                var held = await _held.Query().FirstOrDefaultAsync(h => h.Id == heldId && !h.IsDeleted, innerCt);
                if (held is not null)
                {
                    held.Status = HeldSaleStatus.Recalled;
                    held.RecalledAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync(innerCt);
                }
            }
        }, ct);

        if (invoice is null)
            throw new InvalidOperationException("Checkout produced no invoice.");

        // Sale is already committed. FBR must never roll back checkout — catch all failures + enqueue retry.
        var fbrEnabled = await _features.BehaviorEnabledAsync(ConfigKeys.BehFbrEnabled, ct);
        if (!fbrEnabled)
        {
            return new PosCheckoutResultDto(
                invoice.Id, invoice.InvoiceNumber, null,
                false, false, null,
                invoice.GrandTotal, invoice.TaxAmount, invoice.SubTotal,
                invoice.ChangeDue, invoice.PaymentStatus.ToString());
        }

        var fbrRequest = FbrInvoiceBuilder.Build(invoice, invoice.Lines.ToList(), settings, dto.Buyer, dto.ScenarioId, dto.SaleType);
        FbrPostResultDto fbrResult;
        try
        {
            fbrResult = await _fbrService.PostInvoiceAsync(fbrRequest, ct);
        }
        catch (Exception ex)
        {
            fbrResult = FbrPostResultDto.Fail(
                $"FBR post threw after sale commit: {ex.Message}",
                JsonSerializer.Serialize(fbrRequest));
        }

        try
        {
            var submission = new FbrSubmission
            {
                SalesInvoiceId = invoice.Id,
                FbrInvoiceNumber = fbrResult.InvoiceNumber,
                Status = fbrResult.Success
                    ? (fbrResult.WasStubbed ? FbrSubmissionStatus.Stub : FbrSubmissionStatus.Success)
                    : FbrSubmissionStatus.Failed,
                RequestJson = fbrResult.RequestJson ?? JsonSerializer.Serialize(fbrRequest),
                ResponseJson = fbrResult.ResponseJson,
                ErrorMessage = fbrResult.Success ? null : fbrResult.Message,
                SubmittedAt = DateTime.UtcNow
            };
            _fbrSubmissions.Add(submission);
            await _unitOfWork.SaveChangesAsync(ct);
            if (!fbrResult.Success)
                _fbrOutbox.EnqueueFbrRetry(invoice.Id, submission.RequestJson);
        }
        catch
        {
            // Persistence of FBR row failed — still enqueue retry so outbox can rebuild payload.
            if (!fbrResult.Success)
                _fbrOutbox.EnqueueFbrRetry(invoice.Id, fbrResult.RequestJson ?? JsonSerializer.Serialize(fbrRequest));
        }

        return new PosCheckoutResultDto(
            invoice.Id, invoice.InvoiceNumber, fbrResult.InvoiceNumber,
            fbrResult.Success, fbrResult.WasStubbed, fbrResult.Message,
            invoice.GrandTotal, invoice.TaxAmount, invoice.SubTotal,
            invoice.ChangeDue, invoice.PaymentStatus.ToString());
    }

    public async Task<string> GetReceiptHtmlAsync(int salesInvoiceId, string? publicBaseUrl = null, CancellationToken ct = default)
    {
        var invoice = await _invoices.Query()
            .Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Customer)
            .Include(i => i.FbrSubmission)
            .FirstOrDefaultAsync(i => i.Id == salesInvoiceId && !i.IsDeleted, ct)
            ?? throw new InvalidOperationException("Invoice not found.");

        var settings = await _settings.Query().AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        var brandName = !string.IsNullOrWhiteSpace(settings?.CompanyName)
            ? settings!.CompanyName
            : await _features.GetBrandAsync(ConfigKeys.BrandAppName, "Car Auto Parts", ct);
        var logoUrl = ResolveAbsoluteLogoUrl(
            !string.IsNullOrWhiteSpace(settings?.LogoUrl) ? settings!.LogoUrl : settings?.LogoPath,
            publicBaseUrl);
        var footer = !string.IsNullOrWhiteSpace(settings?.InvoiceFooter)
            ? settings!.InvoiceFooter!
            : "Thank you";

        var fbr = invoice.FbrSubmission;
        var fbrPosted = fbr is not null &&
            fbr.Status is FbrSubmissionStatus.Success or FbrSubmissionStatus.Stub &&
            !string.IsNullOrWhiteSpace(fbr.FbrInvoiceNumber);
        var irn = fbrPosted ? fbr!.FbrInvoiceNumber! : null;
        var qrPayload = fbrPosted
            ? $"FBR-IRN:{irn}|TOTAL:{invoice.GrandTotal:0.00}"
            : null;

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>").Append(invoice.InvoiceNumber)
            .Append("</title><style>body{font-family:Segoe UI,Arial,sans-serif;max-width:320px;margin:1rem auto;font-size:13px}h1{font-size:16px;margin:0 0 .35rem}table{width:100%;border-collapse:collapse}td{padding:.2rem 0}.r{text-align:right}.muted{color:#666;font-size:11px}.shop{text-align:center;margin-bottom:.6rem}.shop img{max-height:48px;max-width:180px;object-fit:contain;display:block;margin:0 auto .35rem}.fbr{margin-top:.75rem;text-align:center}.warn{color:#a60;font-size:11px}</style></head><body>");
        sb.Append("<div class='shop'>");
        if (!string.IsNullOrWhiteSpace(logoUrl))
            sb.Append("<img src='").Append(System.Net.WebUtility.HtmlEncode(logoUrl)).Append("' alt='' />");
        sb.Append("<h1>").Append(System.Net.WebUtility.HtmlEncode(brandName)).Append("</h1>");
        if (!string.IsNullOrWhiteSpace(settings?.Address))
            sb.Append("<div class='muted'>").Append(System.Net.WebUtility.HtmlEncode(settings!.Address)).Append("</div>");
        if (!string.IsNullOrWhiteSpace(settings?.City) || !string.IsNullOrWhiteSpace(settings?.Phone))
        {
            sb.Append("<div class='muted'>");
            if (!string.IsNullOrWhiteSpace(settings?.City))
                sb.Append(System.Net.WebUtility.HtmlEncode(settings!.City));
            if (!string.IsNullOrWhiteSpace(settings?.City) && !string.IsNullOrWhiteSpace(settings?.Phone))
                sb.Append(" · ");
            if (!string.IsNullOrWhiteSpace(settings?.Phone))
                sb.Append(System.Net.WebUtility.HtmlEncode(settings!.Phone));
            sb.Append("</div>");
        }
        if (!string.IsNullOrWhiteSpace(settings?.Ntn))
            sb.Append("<div class='muted'>NTN: ").Append(System.Net.WebUtility.HtmlEncode(settings!.Ntn)).Append("</div>");
        sb.Append("</div>");
        sb.Append("<div class='muted'>").Append(invoice.InvoiceNumber).Append(" · ")
            .Append(invoice.InvoiceDate.ToLocalTime().ToString("g")).Append("</div><div>")
            .Append(System.Net.WebUtility.HtmlEncode(invoice.BuyerName ?? invoice.Customer?.Name ?? "Walk-in"))
            .Append("</div><hr/><table>");
        foreach (var line in invoice.Lines)
        {
            sb.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(line.ProductName))
                .Append("<div class='muted'>").Append(line.Quantity.ToString("0.##")).Append(" × ").Append(line.UnitPrice.ToString("N2"))
                .Append("</div></td><td class='r'>").Append(line.LineTotal.ToString("N2")).Append("</td></tr>");
        }
        sb.Append("</table><hr/><table><tr><td>Subtotal</td><td class='r'>").Append(invoice.SubTotal.ToString("N2")).Append("</td></tr>");
        sb.Append("<tr><td>Tax</td><td class='r'>").Append(invoice.TaxAmount.ToString("N2")).Append("</td></tr>");
        sb.Append("<tr><td><strong>Total</strong></td><td class='r'><strong>").Append(invoice.GrandTotal.ToString("N2")).Append("</strong></td></tr>");
        foreach (var p in invoice.Payments)
            sb.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(p.PaymentMethod)).Append("</td><td class='r'>").Append(p.Amount.ToString("N2")).Append("</td></tr>");
        if (invoice.ChangeDue > 0)
            sb.Append("<tr><td>Change</td><td class='r'>").Append(invoice.ChangeDue.ToString("N2")).Append("</td></tr>");
        sb.Append("</table>");

        if (fbrPosted)
        {
            sb.Append("<div class='fbr'><div><strong>FBR IRN</strong></div><div>")
                .Append(System.Net.WebUtility.HtmlEncode(irn))
                .Append("</div>");
            if (fbr!.Status == FbrSubmissionStatus.Stub)
                sb.Append("<div class='warn'>Sandbox / stub</div>");
            sb.Append("<div class='muted' style='margin-top:.4rem;word-break:break-all'>")
                .Append(System.Net.WebUtility.HtmlEncode(qrPayload))
                .Append("</div></div>");
        }
        else if (fbr is not null && fbr.Status == FbrSubmissionStatus.Failed)
        {
            sb.Append("<p class='warn'>FBR pending / failed — reprint after outbox retry posts IRN.</p>");
        }

        sb.Append("<p class='muted'>").Append(System.Net.WebUtility.HtmlEncode(footer))
            .Append("</p><script>window.onload=()=>window.print()</script></body></html>");
        return sb.ToString();
    }

    private static string? ResolveAbsoluteLogoUrl(string? logo, string? publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(logo)) return null;
        var value = logo.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return value;
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
            return value.StartsWith('/') ? null : value;
        return publicBaseUrl.TrimEnd('/') + (value.StartsWith('/') ? value : "/" + value);
    }

    private static List<PosTenderDto> BuildTenders(PosCheckoutDto dto, decimal grandTotal)
    {
        if (dto.Tenders is { Count: > 0 })
            return dto.Tenders.Where(t => t.Amount > 0).Select(t => new PosTenderDto(t.Method.Trim(), t.Amount)).ToList();

        var method = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod.Trim();
        return [new PosTenderDto(method, grandTotal)];
    }

    private async Task<(List<PosTenderDto> Recorded, decimal ChangeDue, decimal CreditAmount, PaymentStatus Status)> ApplyTendersAsync(
        SalesInvoice invoice, List<PosTenderDto> tenders, CancellationToken ct)
    {
        var credit = tenders.Where(t => IsCredit(t.Method)).Sum(t => t.Amount);
        var nonCredit = tenders.Where(t => !IsCredit(t.Method)).ToList();
        var nonCreditSum = nonCredit.Sum(t => t.Amount);

        if (credit > 0 && invoice.CustomerId is null)
            throw new InvalidOperationException("Customer is required for credit tender.");

        if (credit > 0 && invoice.CustomerId is int custId)
        {
            var check = await _salesEnterprise.CheckCreditLimitAsync(custId, credit, ct);
            if (!check.Succeeded || check.Data is null || !check.Data.Approved)
                throw new InvalidOperationException(check.Data?.Message ?? check.Error ?? "Credit limit exceeded.");
        }

        if (nonCreditSum + credit + 0.01m < invoice.GrandTotal)
            throw new InvalidOperationException($"Tenders do not cover total {invoice.GrandTotal:N2}.");

        var dueFromNonCredit = Math.Max(0, invoice.GrandTotal - credit);
        var changeDue = Math.Max(0, nonCreditSum - dueFromNonCredit);

        var recorded = new List<PosTenderDto>();
        var remainingChange = changeDue;
        foreach (var t in nonCredit)
        {
            var amount = t.Amount;
            if (remainingChange > 0 && t.Method.Contains("cash", StringComparison.OrdinalIgnoreCase))
            {
                var reduce = Math.Min(remainingChange, amount);
                amount -= reduce;
                remainingChange -= reduce;
            }
            if (amount > 0)
            {
                recorded.Add(new PosTenderDto(t.Method, amount));
                _payments.Add(new Payment
                {
                    SalesInvoiceId = invoice.Id,
                    Amount = amount,
                    PaymentMethod = t.Method,
                    PaymentDate = DateTime.UtcNow
                });
            }
        }

        var ar = Math.Min(credit, invoice.GrandTotal - recorded.Sum(r => r.Amount));
        if (ar > 0 && invoice.CustomerId is int cid)
        {
            var customer = await _customers.Query().FirstOrDefaultAsync(c => c.Id == cid && !c.IsDeleted, ct);
            if (customer is not null)
                customer.Balance += ar;
        }

        var status = ar <= 0 ? PaymentStatus.Paid
            : recorded.Sum(r => r.Amount) > 0 ? PaymentStatus.Partial
            : PaymentStatus.Pending;

        return (recorded, changeDue, ar, status);
    }

    private async Task PostSalesGlAsync(SalesInvoice invoice, List<PosTenderDto> recorded, decimal creditAmount, decimal cogsTotal, CancellationToken ct)
    {
        var netRevenue = Math.Max(0, invoice.SubTotal - invoice.DiscountAmount);
        var glLines = new List<GlPostingLineRequest>();

        foreach (var t in recorded)
        {
            var key = t.Method.Contains("bank", StringComparison.OrdinalIgnoreCase) ||
                      t.Method.Contains("card", StringComparison.OrdinalIgnoreCase) ||
                      t.Method.Contains("transfer", StringComparison.OrdinalIgnoreCase)
                ? "Bank" : "Cash";
            glLines.Add(new GlPostingLineRequest(key, t.Amount, true, t.Method));
        }

        if (creditAmount > 0)
            glLines.Add(new GlPostingLineRequest("Receivable", creditAmount, true, "AR"));

        var debitSum = glLines.Where(l => l.IsDebit).Sum(l => l.Amount);
        if (debitSum + 0.01m < invoice.GrandTotal)
            glLines.Add(new GlPostingLineRequest("Cash", invoice.GrandTotal - debitSum, true, "Rounding"));

        if (netRevenue > 0)
            glLines.Add(new GlPostingLineRequest("Revenue", netRevenue, false, "Sales"));
        if (invoice.TaxAmount > 0)
            glLines.Add(new GlPostingLineRequest("Tax", invoice.TaxAmount, false, "Sales tax"));

        var creditSum = glLines.Where(l => !l.IsDebit).Sum(l => l.Amount);
        if (creditSum < invoice.GrandTotal)
            glLines.Add(new GlPostingLineRequest("Revenue", invoice.GrandTotal - creditSum, false, "Rounding"));

        if (cogsTotal > 0)
        {
            glLines.Add(new GlPostingLineRequest("Cogs", cogsTotal, true, "COGS"));
            glLines.Add(new GlPostingLineRequest("Inventory", cogsTotal, false, "Inventory"));
        }

        var gl = await _gl.PostDocumentAsync(
            "SalesInvoice", invoice.InvoiceDate, invoice.InvoiceNumber, $"POS {invoice.InvoiceNumber}",
            invoice.Id, glLines, autoPost: true, ct);
        if (!gl.Succeeded)
            throw new InvalidOperationException(gl.Error ?? "GL posting failed.");
    }

    private async Task<CashierShift?> ResolveOpenShiftAsync(int? shiftId, CancellationToken ct)
    {
        var userId = _currentUser.CurrentUser!.Id;
        if (shiftId is int id)
            return await _shifts.Query().FirstOrDefaultAsync(
                s => s.Id == id && s.UserId == userId && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
        return await _shifts.Query().FirstOrDefaultAsync(
            s => s.UserId == userId && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
    }

    private async Task<decimal> ResolveUnitPriceAsync(Product product, PosCheckoutLineDto line, int? customerId, CancellationToken ct)
    {
        if (line.UnitPriceOverride is decimal ov)
        {
            if (_currentUser.CurrentUser is not null && !_currentUser.HasPermission(Permissions.PosPriceOverride))
                throw new InvalidOperationException("Price override permission required.");
            return ov;
        }

        var lookup = await _salesEnterprise.GetPriceForProductAsync(product.Id, customerId, line.Quantity, ct);
        if (lookup.Succeeded && lookup.Data is not null)
            return lookup.Data.UnitPrice;
        return product.SalePrice;
    }

    private async Task EnsureStockForSaleAsync(Product product, int warehouseId, decimal qty, CancellationToken ct)
    {
        var settings = await _settings.Query().AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (settings?.AllowNegativeStock == true)
            return;

        var kit = await _kits.Query().Include(k => k.Components)
            .FirstOrDefaultAsync(k => k.ParentProductId == product.Id && !k.IsDeleted, ct);
        if (kit is { Components.Count: > 0 })
        {
            foreach (var c in kit.Components.Where(x => !x.IsDeleted))
            {
                var need = c.Quantity * qty;
                var ensure = await _atp.EnsureAvailableAsync(c.ComponentProductId, warehouseId, need, ct);
                if (!ensure.Succeeded)
                    throw new InvalidOperationException($"Insufficient kit component stock (product {c.ComponentProductId}).");
            }
            return;
        }

        var ok = await _atp.EnsureAvailableAsync(product.Id, warehouseId, qty, ct);
        if (!ok.Succeeded)
            throw new InvalidOperationException($"Insufficient stock for {product.Name}.");
    }

    private async Task<decimal> DeductForSaleAsync(Product product, int warehouseId, decimal qty, int invoiceId, CancellationToken ct)
    {
        var kit = await _kits.Query().Include(k => k.Components)
            .FirstOrDefaultAsync(k => k.ParentProductId == product.Id && !k.IsDeleted, ct);
        if (kit is { Components.Count: > 0 })
        {
            decimal cogs = 0;
            foreach (var c in kit.Components.Where(x => !x.IsDeleted))
            {
                var need = c.Quantity * qty;
                var comp = await _products.Query().FirstAsync(p => p.Id == c.ComponentProductId, ct);
                var deduct = await _inventory.DeductStockAsync(comp.Id, warehouseId, need, nameof(SalesInvoice), invoiceId, ct);
                if (!deduct.Succeeded)
                    throw new InvalidOperationException(deduct.Error ?? "Kit component stock deduction failed.");
                var issued = deduct.Data;
                cogs += (issued > 0 ? issued : (comp.CostPrice > 0 ? comp.CostPrice : comp.PurchasePrice)) * need;
            }
            return cogs;
        }

        var result = await _inventory.DeductStockAsync(product.Id, warehouseId, qty, nameof(SalesInvoice), invoiceId, ct);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Error ?? "Stock deduction failed.");
        var unit = result.Data;
        if (unit <= 0)
            unit = product.CostPrice > 0 ? product.CostPrice : product.PurchasePrice;
        return unit * qty;
    }

    private static bool IsCredit(string method) =>
        method.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
        method.Contains("account", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("AR", StringComparison.OrdinalIgnoreCase);

    private async Task<string> GenerateInvoiceNumberAsync(CompanySettings settings, CancellationToken ct)
    {
        var prefix = settings.InvoicePrefix ?? "INV";
        var count = await _invoices.Query().CountAsync(ct);
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}";
    }
}
