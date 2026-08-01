using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public record ApprovalPolicyDto(int Id, string DocumentType, decimal MinAmount, string RequiredPermission, bool IsActive, string? Notes);
public record ApprovalPolicyUpsertDto(string DocumentType, decimal MinAmount, string RequiredPermission, bool IsActive, string? Notes);
public record ApprovalRequestDto(
    int Id, string DocumentType, int DocumentId, string? DocumentNumber, decimal Amount,
    string Status, string RequestedByUserName, DateTime RequestedAt,
    string? DecidedByUserName, DateTime? DecidedAt, string? DecisionNotes);

public interface IApprovalWorkflowService
{
    /// <summary>
    /// Returns Success if caller may proceed immediately.
    /// Returns Failure with code PendingApproval if a request was queued.
    /// </summary>
    Task<Result> EnsureApprovedOrQueueAsync(
        string documentType,
        int documentId,
        string? documentNumber,
        decimal amount,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApprovalPolicyDto>> ListPoliciesAsync(CancellationToken ct = default);
    Task<Result<ApprovalPolicyDto>> UpsertPolicyAsync(int? id, ApprovalPolicyUpsertDto dto, CancellationToken ct = default);
    Task<Result> DeletePolicyAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalRequestDto>> ListPendingAsync(CancellationToken ct = default);
    Task<Result> DecideAsync(int requestId, bool approve, string? notes, CancellationToken ct = default);
}

public sealed class ApprovalWorkflowService : IApprovalWorkflowService
{
    public const string PendingCode = "PendingApproval";

    private readonly IRepository<ApprovalPolicy> _policies;
    private readonly IRepository<ApprovalRequest> _requests;
    private readonly ICurrentUserService _user;
    private readonly ICurrentCompanyContext _company;
    private readonly IUnitOfWork _uow;
    private readonly IMoneyAuditService _moneyAudit;
    private readonly INotificationService _notifications;

    public ApprovalWorkflowService(
        IRepository<ApprovalPolicy> policies,
        IRepository<ApprovalRequest> requests,
        ICurrentUserService user,
        ICurrentCompanyContext company,
        IUnitOfWork uow,
        IMoneyAuditService moneyAudit,
        INotificationService notifications)
    {
        _policies = policies;
        _requests = requests;
        _user = user;
        _company = company;
        _uow = uow;
        _moneyAudit = moneyAudit;
        _notifications = notifications;
    }

    public async Task<Result> EnsureApprovedOrQueueAsync(
        string documentType,
        int documentId,
        string? documentNumber,
        decimal amount,
        CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result.Success(); // non-tenant tests skip matrix

        var policy = await _policies.Query()
            .Where(p => !p.IsDeleted && p.IsActive && p.DocumentType == documentType && amount >= p.MinAmount)
            .OrderByDescending(p => p.MinAmount)
            .FirstOrDefaultAsync(ct);

        if (policy is null)
            return Result.Success();

        var priorApproved = await _requests.Query()
            .AnyAsync(r =>
                !r.IsDeleted && r.DocumentType == documentType && r.DocumentId == documentId
                && r.Status == ApprovalRequestStatus.Approved, ct);
        if (priorApproved)
            return Result.Success();

        // Only ApprovalsManage can bypass the matrix without a prior approval record.
        if (_user.HasPermission(Permissions.ApprovalsManage))
        {
            await _moneyAudit.RecordAsync(AuditAction.Approve, documentType, documentId,
                $"Bypassed policy {policy.Id} via approvals.manage", ct: ct);
            return Result.Success();
        }

        var existing = await _requests.Query()
            .FirstOrDefaultAsync(r =>
                !r.IsDeleted && r.DocumentType == documentType && r.DocumentId == documentId
                && r.Status == ApprovalRequestStatus.Pending, ct);
        if (existing is not null)
            return Result.Failure($"{PendingCode}: Approval already pending (request #{existing.Id}).");

        var req = new ApprovalRequest
        {
            CompanyId = _company.CompanyId.Value,
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentNumber = documentNumber,
            Amount = amount,
            Status = ApprovalRequestStatus.Pending,
            RequestedByUserId = _user.CurrentUser?.Id ?? 0,
            RequestedByUserName = _user.CurrentUser?.Username ?? "system",
            RequestedAt = DateTime.UtcNow,
            PolicyId = policy.Id
        };
        _requests.Add(req);
        await _uow.SaveChangesAsync(ct);

        await _notifications.CreateNotificationAsync(
            NotificationType.PurchaseAlert,
            "Approval required",
            $"{documentType} {documentNumber ?? documentId.ToString()} amount {amount:N0} needs approval.",
            documentType, documentId, ct);

        await _moneyAudit.RecordAsync(AuditAction.Update, nameof(ApprovalRequest), req.Id,
            $"Queued approval for {documentType} #{documentId}", ct: ct);

        return Result.Failure($"{PendingCode}: Submitted for approval (request #{req.Id}).");
    }

    public async Task<IReadOnlyList<ApprovalPolicyDto>> ListPoliciesAsync(CancellationToken ct = default)
    {
        var rows = await _policies.Query().Where(p => !p.IsDeleted).OrderBy(p => p.DocumentType).ThenBy(p => p.MinAmount).ToListAsync(ct);
        return rows.Select(MapPolicy).ToList();
    }

    public async Task<Result<ApprovalPolicyDto>> UpsertPolicyAsync(int? id, ApprovalPolicyUpsertDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.DocumentType))
            return Result<ApprovalPolicyDto>.Failure("DocumentType is required.");
        if (string.IsNullOrWhiteSpace(dto.RequiredPermission))
            return Result<ApprovalPolicyDto>.Failure("RequiredPermission is required.");
        if (!_company.CompanyId.HasValue)
            return Result<ApprovalPolicyDto>.Failure("Company context required.");

        ApprovalPolicy entity;
        if (id is int existingId)
        {
            entity = await _policies.Query().FirstOrDefaultAsync(p => p.Id == existingId && !p.IsDeleted, ct)
                ?? throw new InvalidOperationException("Policy not found.");
            entity.DocumentType = dto.DocumentType.Trim();
            entity.MinAmount = dto.MinAmount;
            entity.RequiredPermission = dto.RequiredPermission.Trim();
            entity.IsActive = dto.IsActive;
            entity.Notes = dto.Notes;
            entity.UpdatedAt = DateTime.UtcNow;
            _policies.Update(entity);
        }
        else
        {
            entity = new ApprovalPolicy
            {
                CompanyId = _company.CompanyId.Value,
                DocumentType = dto.DocumentType.Trim(),
                MinAmount = dto.MinAmount,
                RequiredPermission = dto.RequiredPermission.Trim(),
                IsActive = dto.IsActive,
                Notes = dto.Notes
            };
            _policies.Add(entity);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<ApprovalPolicyDto>.Success(MapPolicy(entity));
    }

    public async Task<Result> DeletePolicyAsync(int id, CancellationToken ct = default)
    {
        var entity = await _policies.Query().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        if (entity is null) return Result.Failure("Policy not found.");
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _policies.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<ApprovalRequestDto>> ListPendingAsync(CancellationToken ct = default)
    {
        var rows = await _requests.Query()
            .Where(r => !r.IsDeleted && r.Status == ApprovalRequestStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(ct);
        return rows.Select(MapRequest).ToList();
    }

    public async Task<Result> DecideAsync(int requestId, bool approve, string? notes, CancellationToken ct = default)
    {
        if (!_user.HasPermission(Permissions.ApprovalsDecide) && !_user.HasPermission(Permissions.ApprovalsManage))
            return Result.Failure("Missing approvals.decide permission.");

        var req = await _requests.Query().Include(r => r.Policy)
            .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted, ct);
        if (req is null) return Result.Failure("Request not found.");
        if (req.Status != ApprovalRequestStatus.Pending)
            return Result.Failure("Request is not pending.");

        if (req.Policy is not null
            && !_user.HasPermission(req.Policy.RequiredPermission)
            && !_user.HasPermission(Permissions.ApprovalsManage))
            return Result.Failure($"Missing permission {req.Policy.RequiredPermission}.");

        if (req.RequestedByUserId == (_user.CurrentUser?.Id ?? -1) && !_user.HasPermission(Permissions.ApprovalsManage))
            return Result.Failure("Requester cannot decide their own approval request.");

        req.Status = approve ? ApprovalRequestStatus.Approved : ApprovalRequestStatus.Rejected;
        req.DecidedByUserId = _user.CurrentUser?.Id;
        req.DecidedByUserName = _user.CurrentUser?.Username;
        req.DecidedAt = DateTime.UtcNow;
        req.DecisionNotes = notes;
        req.UpdatedAt = DateTime.UtcNow;
        _requests.Update(req);
        await _uow.SaveChangesAsync(ct);

        await _moneyAudit.RecordAsync(
            approve ? AuditAction.Approve : AuditAction.Reject,
            req.DocumentType,
            req.DocumentId,
            $"{(approve ? "Approved" : "Rejected")} request #{req.Id}",
            ct: ct);

        return Result.Success();
    }

    private static ApprovalPolicyDto MapPolicy(ApprovalPolicy p) =>
        new(p.Id, p.DocumentType, p.MinAmount, p.RequiredPermission, p.IsActive, p.Notes);

    private static ApprovalRequestDto MapRequest(ApprovalRequest r) =>
        new(r.Id, r.DocumentType, r.DocumentId, r.DocumentNumber, r.Amount, r.Status.ToString(),
            r.RequestedByUserName, r.RequestedAt, r.DecidedByUserName, r.DecidedAt, r.DecisionNotes);
}
