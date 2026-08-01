using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Finance;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public interface IDocumentVoidService
{
    Task<Result> VoidJournalAsync(int journalId, string? reason, CancellationToken ct = default);
    Task<Result> VoidSalesInvoiceAsync(int invoiceId, string? reason, CancellationToken ct = default);
    Task<Result> VoidPurchaseInvoiceAsync(int purchaseInvoiceId, string? reason, CancellationToken ct = default);
}

public sealed class DocumentVoidService : IDocumentVoidService
{
    private readonly IRepository<JournalEntry> _journals;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<PurchaseInvoice> _purchaseInvoices;
    private readonly IRepository<Supplier> _suppliers;
    private readonly ICurrentUserService _user;
    private readonly ICurrentCompanyContext _company;
    private readonly IUnitOfWork _uow;
    private readonly IMoneyAuditService _moneyAudit;
    private readonly IGlPostingService _gl;
    private readonly IAccountingPeriodService _periods;

    public DocumentVoidService(
        IRepository<JournalEntry> journals,
        IRepository<SalesInvoice> invoices,
        IRepository<Payment> payments,
        IRepository<PurchaseInvoice> purchaseInvoices,
        IRepository<Supplier> suppliers,
        ICurrentUserService user,
        ICurrentCompanyContext company,
        IUnitOfWork uow,
        IMoneyAuditService moneyAudit,
        IGlPostingService gl,
        IAccountingPeriodService periods)
    {
        _journals = journals;
        _invoices = invoices;
        _payments = payments;
        _purchaseInvoices = purchaseInvoices;
        _suppliers = suppliers;
        _user = user;
        _company = company;
        _uow = uow;
        _moneyAudit = moneyAudit;
        _gl = gl;
        _periods = periods;
    }

    public async Task<Result> VoidJournalAsync(int journalId, string? reason, CancellationToken ct = default)
    {
        if (!_user.HasPermission(Permissions.FinanceVoid) && !_user.HasPermission(Permissions.FinanceManage))
            return Result.Failure("Missing finance.void permission.");

        var journal = await _journals.Query().Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == journalId && !j.IsDeleted, ct);
        if (journal is null) return Result.Failure("Journal not found.");
        if (journal.Status == JournalStatus.Voided)
            return Result.Failure("Journal already voided.");
        if (journal.Status != JournalStatus.Posted)
            return Result.Failure("Only posted journals can be voided.");

        // Reversing entry: swap debit/credit via GlPostingService lines using account ids directly —
        // use document type JournalVoid with mirrored amounts on same accounts through raw journal clone.
        var reverse = new JournalEntry
        {
            CompanyId = journal.CompanyId,
            JournalNumber = $"{journal.JournalNumber}-R",
            JournalDate = DateTime.UtcNow.Date,
            Reference = journal.JournalNumber,
            Description = $"Reversal of {journal.JournalNumber}: {reason}",
            Status = JournalStatus.Draft,
            SourceDocumentType = "JournalVoid",
            SourceDocumentId = journal.Id
        };
        foreach (var line in journal.Lines)
        {
            reverse.Lines.Add(new JournalLine
            {
                CompanyId = journal.CompanyId,
                AccountId = line.AccountId,
                CostCenterId = line.CostCenterId,
                Description = line.Description,
                Debit = line.Credit,
                Credit = line.Debit
            });
        }

        try
        {
            var periodResult = await _periods.EnsureOpenAsync(reverse.JournalDate, ct);
            if (!periodResult.Succeeded)
                return Result.Failure(periodResult.Error ?? "Period locked.");
            reverse.Post(periodResult.Data!);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }

        journal.Void(reason);
        _journals.Add(reverse);
        _journals.Update(journal);
        await _uow.SaveChangesAsync(ct);

        await _moneyAudit.RecordAsync(AuditAction.Void, nameof(JournalEntry), journal.Id,
            $"Voided {journal.JournalNumber}; reversing {reverse.JournalNumber}",
            new { journal.Status }, new { reverse.JournalNumber, reason }, ct);
        return Result.Success();
    }

    public async Task<Result> VoidSalesInvoiceAsync(int invoiceId, string? reason, CancellationToken ct = default)
    {
        if (!_user.HasPermission(Permissions.FinanceVoid) && !_user.HasPermission(Permissions.SalesView))
            return Result.Failure("Missing finance.void permission.");

        var invoice = await _invoices.Query()
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted, ct);
        if (invoice is null) return Result.Failure("Invoice not found.");
        if (invoice.IsVoided) return Result.Failure("Invoice already voided.");

        // Soft-void invoice + payments; post reversing SalesInvoice GL if company context present.
        if (_company.CompanyId.HasValue && invoice.GrandTotal > 0)
        {
            var lines = new List<GlPostingLineRequest>
            {
                new("Revenue", invoice.SubTotal > 0 ? invoice.SubTotal : invoice.GrandTotal, true, "Void revenue"),
                new("Cash", invoice.GrandTotal, false, "Void cash/AR")
            };
            if (invoice.TaxAmount > 0)
            {
                lines.Clear();
                lines.Add(new("Revenue", invoice.SubTotal, true, "Void revenue"));
                lines.Add(new("Tax", invoice.TaxAmount, true, "Void tax"));
                lines.Add(new("Cash", invoice.GrandTotal, false, "Void tender"));
            }

            var gl = await _gl.PostDocumentAsync(
                "SalesInvoice",
                DateTime.UtcNow.Date,
                invoice.InvoiceNumber,
                $"Void {invoice.InvoiceNumber}: {reason}",
                invoice.Id,
                lines,
                autoPost: true,
                ct);
            if (!gl.Succeeded)
                return Result.Failure(gl.Error ?? "Void GL failed.");
            invoice.VoidJournalEntryId = gl.Data!.JournalId;
        }

        invoice.IsVoided = true;
        invoice.VoidedAt = DateTime.UtcNow;
        invoice.VoidedBy = _user.CurrentUser?.Username;
        invoice.VoidReason = reason;
        invoice.UpdatedAt = DateTime.UtcNow;
        foreach (var p in invoice.Payments)
        {
            p.IsDeleted = true;
            p.UpdatedAt = DateTime.UtcNow;
            _payments.Update(p);
        }
        _invoices.Update(invoice);
        await _uow.SaveChangesAsync(ct);

        await _moneyAudit.RecordAsync(AuditAction.Void, nameof(SalesInvoice), invoice.Id,
            $"Voided {invoice.InvoiceNumber}", ct: ct);
        return Result.Success();
    }

    public async Task<Result> VoidPurchaseInvoiceAsync(int purchaseInvoiceId, string? reason, CancellationToken ct = default)
    {
        if (!_user.HasPermission(Permissions.FinanceVoid) && !_user.HasPermission(Permissions.FinanceManage))
            return Result.Failure("Missing finance.void permission.");

        var invoice = await _purchaseInvoices.Query()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == purchaseInvoiceId && !i.IsDeleted, ct);
        if (invoice is null) return Result.Failure("Purchase invoice not found.");
        if (invoice.Status == PurchaseInvoiceStatus.Cancelled)
            return Result.Failure("Purchase invoice already voided/cancelled.");
        if (invoice.Status != PurchaseInvoiceStatus.Posted)
            return Result.Failure("Only posted purchase invoices can be voided.");

        if (_company.CompanyId.HasValue && invoice.GrandTotal > 0)
        {
            var debitKey = invoice.GoodsReceiptNoteId.HasValue ? "GrnClearing" : "Inventory";
            var exTax = invoice.GrandTotal - invoice.TaxAmount;
            if (exTax < 0) exTax = 0;
            var lines = new List<GlPostingLineRequest>
            {
                new("Payable", invoice.GrandTotal, true, $"Void PI {invoice.InvoiceNumber} AP"),
                new(debitKey, exTax > 0 ? exTax : invoice.GrandTotal, false, $"Void PI {invoice.InvoiceNumber} {debitKey}")
            };
            if (invoice.TaxAmount > 0)
                lines.Insert(1, new("Tax", invoice.TaxAmount, false, $"Void PI {invoice.InvoiceNumber} Tax"));

            var gl = await _gl.PostDocumentAsync(
                "PurchaseInvoice",
                DateTime.UtcNow.Date,
                invoice.InvoiceNumber,
                $"Void {invoice.InvoiceNumber}: {reason}",
                invoice.Id,
                lines,
                autoPost: true,
                ct);
            if (!gl.Succeeded)
                return Result.Failure(gl.Error ?? "Void AP GL failed.");
        }

        var supplier = await _suppliers.Query().FirstOrDefaultAsync(s => s.Id == invoice.SupplierId && !s.IsDeleted, ct);
        if (supplier is not null)
        {
            supplier.Balance -= invoice.GrandTotal;
            supplier.UpdatedAt = DateTime.UtcNow;
            _suppliers.Update(supplier);
        }

        invoice.Status = PurchaseInvoiceStatus.Cancelled;
        invoice.MatchNotes = string.IsNullOrWhiteSpace(reason)
            ? $"Voided {DateTime.UtcNow:u}"
            : $"Voided: {reason}";
        invoice.UpdatedAt = DateTime.UtcNow;
        _purchaseInvoices.Update(invoice);
        await _uow.SaveChangesAsync(ct);

        await _moneyAudit.RecordAsync(AuditAction.Void, nameof(PurchaseInvoice), invoice.Id,
            $"Voided {invoice.InvoiceNumber}", ct: ct);
        return Result.Success();
    }
}
