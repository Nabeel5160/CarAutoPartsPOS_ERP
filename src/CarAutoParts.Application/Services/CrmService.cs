using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Crm;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public interface ICrmService
{
    Task<PagedResult<LeadDto>> GetLeadsAsync(QuerySpec query, CancellationToken ct = default);
    Task<LeadDto?> GetLeadByIdAsync(int id, CancellationToken ct = default);
    Task<Result<LeadDto>> CreateLeadAsync(LeadCreateDto dto, CancellationToken ct = default);
    Task<Result<LeadDto>> UpdateLeadAsync(int id, LeadUpdateDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<LeadDuplicateDto>> FindDuplicatesAsync(string? phone, string? email, string? name, int? excludeLeadId = null, CancellationToken ct = default);
    Task<Result<LeadDto>> ConvertLeadToCustomerAsync(int id, int? existingCustomerId = null, CancellationToken ct = default);
    Task<Result<OpportunityDto>> ConvertLeadToOpportunityAsync(int id, ConvertLeadToOpportunityDto dto, CancellationToken ct = default);

    Task<PagedResult<CrmActivityDto>> GetActivitiesAsync(QuerySpec query, CancellationToken ct = default);
    Task<Result<CrmActivityDto>> CreateActivityAsync(CrmActivityCreateDto dto, CancellationToken ct = default);
    Task<Result<CrmActivityDto>> CompleteActivityAsync(int id, bool createNext = false, int nextDueDays = 7, CancellationToken ct = default);
    Task<Result> DeleteActivityAsync(int id, CancellationToken ct = default);

    Task<PagedResult<OpportunityDto>> GetOpportunitiesAsync(QuerySpec query, CancellationToken ct = default);
    Task<OpportunityDto?> GetOpportunityByIdAsync(int id, CancellationToken ct = default);
    Task<Result<OpportunityDto>> CreateOpportunityAsync(OpportunityCreateDto dto, CancellationToken ct = default);
    Task<Result<OpportunityDto>> UpdateOpportunityAsync(int id, OpportunityUpdateDto dto, CancellationToken ct = default);
    Task<Result<OpportunityDto>> ChangeOpportunityStageAsync(int id, OpportunityStageChangeDto dto, CancellationToken ct = default);
    Task<Result<OpportunityDto>> LinkQuotationAsync(int id, int quotationId, CancellationToken ct = default);
    Task<IReadOnlyList<OpportunityStageHistoryDto>> GetStageHistoryAsync(int opportunityId, CancellationToken ct = default);
    Task<CrmPipelineDashboardDto> GetPipelineDashboardAsync(CancellationToken ct = default);

    Task<Customer360Dto?> GetCustomer360Async(int customerId, CancellationToken ct = default);

    Task<IReadOnlyList<CrmAssignmentRuleDto>> GetAssignmentRulesAsync(CancellationToken ct = default);
    Task<Result<CrmAssignmentRuleDto>> UpsertAssignmentRuleAsync(CrmAssignmentRuleDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<CrmEmailTemplateDto>> GetEmailTemplatesAsync(CancellationToken ct = default);
    Task<Result<CrmEmailTemplateDto>> UpsertEmailTemplateAsync(CrmEmailTemplateDto dto, CancellationToken ct = default);
}

public sealed class CrmService : ICrmService
{
    private readonly IRepository<Lead> _leads;
    private readonly IRepository<CrmActivity> _activities;
    private readonly IRepository<Opportunity> _opportunities;
    private readonly IRepository<OpportunityStageHistory> _stageHistory;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesOrder> _orders;
    private readonly IRepository<SalesReturn> _returns;
    private readonly IRepository<CrmAssignmentRule> _rules;
    private readonly IRepository<CrmEmailTemplate> _templates;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _user;
    private readonly INotificationService _notifications;

    public CrmService(
        IRepository<Lead> leads,
        IRepository<CrmActivity> activities,
        IRepository<Opportunity> opportunities,
        IRepository<OpportunityStageHistory> stageHistory,
        IRepository<Customer> customers,
        IRepository<SalesInvoice> invoices,
        IRepository<SalesOrder> orders,
        IRepository<SalesReturn> returns,
        IRepository<CrmAssignmentRule> rules,
        IRepository<CrmEmailTemplate> templates,
        IUnitOfWork uow,
        ICurrentCompanyContext company,
        ICurrentUserService user,
        INotificationService notifications)
    {
        _leads = leads;
        _activities = activities;
        _opportunities = opportunities;
        _stageHistory = stageHistory;
        _customers = customers;
        _invoices = invoices;
        _orders = orders;
        _returns = returns;
        _rules = rules;
        _templates = templates;
        _uow = uow;
        _company = company;
        _user = user;
        _notifications = notifications;
    }

    private int RequireCompanyId() =>
        _company.CompanyId is int id && id > 0 ? id : throw new InvalidOperationException("Company context is required.");

    private int? TryCompanyId() => _company.CompanyId is int id && id > 0 ? id : null;

    public async Task<PagedResult<LeadDto>> GetLeadsAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _leads.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Name.Contains(s) || (x.Phone != null && x.Phone.Contains(s)) || (x.Email != null && x.Email.Contains(s)));
        }

        if (query.Filters.TryGetValue("status", out var statusObj) && statusObj is not null &&
            Enum.TryParse<LeadStatus>(statusObj.ToString(), true, out var status))
            q = q.Where(x => x.Status == status);

        if (query.Filters.TryGetValue("source", out var sourceObj) && sourceObj is string src && !string.IsNullOrWhiteSpace(src))
            q = q.Where(x => x.Source == src);

        if (query.Filters.TryGetValue("ownerUserId", out var ownerObj) && int.TryParse(ownerObj?.ToString(), out var ownerId))
            q = q.Where(x => x.OwnerUserId == ownerId);

        q = q.OrderByDescending(x => x.CreatedAt);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        var activityCounts = await _activities.Query().AsNoTracking()
            .Where(a => a.LeadId != null && paged.Items.Select(l => l.Id).Contains(a.LeadId.Value))
            .GroupBy(a => a.LeadId!.Value)
            .Select(g => new { LeadId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LeadId, x => x.Count, ct);

        return new PagedResult<LeadDto>
        {
            Items = paged.Items.Select(l => MapLead(l, activityCounts.GetValueOrDefault(l.Id))).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<LeadDto?> GetLeadByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _leads.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        var count = await _activities.Query().CountAsync(a => a.LeadId == id, ct);
        return MapLead(entity, count);
    }

    public async Task<Result<LeadDto>> CreateLeadAsync(LeadCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<LeadDto>.Failure("Lead name is required.");
        if (string.IsNullOrWhiteSpace(dto.Source))
            return Result<LeadDto>.Failure("Source is required.");

        var companyId = TryCompanyId();
        if (companyId is null)
            return Result<LeadDto>.Failure("Company context is required.");

        if (!dto.ConfirmDuplicate)
        {
            var dups = await FindDuplicatesAsync(dto.Phone, dto.Email, dto.Name, null, ct);
            if (dups.Count > 0)
                return Result<LeadDto>.Failure($"Possible duplicates found ({dups.Count}). Pass ConfirmDuplicate=true to proceed.");
        }

        var ownerId = dto.OwnerUserId ?? await ResolveOwnerAsync(dto.Source, ct);

        var entity = new Lead
        {
            CompanyId = companyId.Value,
            Name = dto.Name.Trim(),
            Phone = Norm(dto.Phone),
            Email = Norm(dto.Email),
            Source = dto.Source.Trim(),
            Notes = Norm(dto.Notes),
            OwnerUserId = ownerId,
            Status = LeadStatus.New,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.CurrentUser?.Username ?? "system"
        };

        _leads.Add(entity);
        await _uow.SaveChangesAsync(ct);
        return Result<LeadDto>.Success(MapLead(entity, 0));
    }

    public async Task<Result<LeadDto>> UpdateLeadAsync(int id, LeadUpdateDto dto, CancellationToken ct = default)
    {
        var entity = await _leads.Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result<LeadDto>.Failure("Lead not found.");
        if (entity.Status == LeadStatus.Converted && dto.Status != LeadStatus.Converted)
            return Result<LeadDto>.Failure("Converted leads cannot change status.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<LeadDto>.Failure("Lead name is required.");
        if (string.IsNullOrWhiteSpace(dto.Source))
            return Result<LeadDto>.Failure("Source is required.");
        if (dto.Status == LeadStatus.Lost && string.IsNullOrWhiteSpace(dto.LostReason))
            return Result<LeadDto>.Failure("Lost reason is required when marking a lead as Lost.");
        if (dto.Status == LeadStatus.Converted && entity.ConvertedCustomerId is null)
            return Result<LeadDto>.Failure("Use convert-customer to convert a lead.");

        if (!dto.ConfirmDuplicate)
        {
            var dups = await FindDuplicatesAsync(dto.Phone, dto.Email, dto.Name, id, ct);
            if (dups.Count > 0)
                return Result<LeadDto>.Failure($"Possible duplicates found ({dups.Count}). Pass ConfirmDuplicate=true to proceed.");
        }

        entity.Name = dto.Name.Trim();
        entity.Phone = Norm(dto.Phone);
        entity.Email = Norm(dto.Email);
        entity.Source = dto.Source.Trim();
        entity.Notes = Norm(dto.Notes);
        entity.OwnerUserId = dto.OwnerUserId;
        entity.Status = dto.Status;
        entity.LostReason = dto.Status == LeadStatus.Lost ? dto.LostReason?.Trim() : null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _user.CurrentUser?.Username;

        await _uow.SaveChangesAsync(ct);
        var count = await _activities.Query().CountAsync(a => a.LeadId == id, ct);
        return Result<LeadDto>.Success(MapLead(entity, count));
    }

    public async Task<IReadOnlyList<LeadDuplicateDto>> FindDuplicatesAsync(
        string? phone, string? email, string? name, int? excludeLeadId = null, CancellationToken ct = default)
    {
        var results = new List<LeadDuplicateDto>();
        var q = _leads.Query().AsNoTracking().Where(l => l.Status != LeadStatus.Converted);
        if (excludeLeadId is int ex) q = q.Where(l => l.Id != ex);

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var p = phone.Trim();
            results.AddRange(await q.Where(l => l.Phone == p).Select(l => new LeadDuplicateDto(l.Id, l.Name, l.Phone, l.Email, l.Status, "lead-phone")).ToListAsync(ct));
            results.AddRange(await _customers.Query().AsNoTracking().Where(c => c.Phone == p)
                .Select(c => new LeadDuplicateDto(c.Id, c.Name, c.Phone, c.Email, LeadStatus.Converted, "customer-phone")).ToListAsync(ct));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var e = email.Trim();
            results.AddRange(await q.Where(l => l.Email == e).Select(l => new LeadDuplicateDto(l.Id, l.Name, l.Phone, l.Email, l.Status, "lead-email")).ToListAsync(ct));
            results.AddRange(await _customers.Query().AsNoTracking().Where(c => c.Email == e)
                .Select(c => new LeadDuplicateDto(c.Id, c.Name, c.Phone, c.Email, LeadStatus.Converted, "customer-email")).ToListAsync(ct));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name.Trim();
            results.AddRange(await q.Where(l => l.Name == n).Select(l => new LeadDuplicateDto(l.Id, l.Name, l.Phone, l.Email, l.Status, "lead-name")).ToListAsync(ct));
        }

        return results.GroupBy(r => (r.Kind, r.Id)).Select(g => g.First()).ToList();
    }

    public async Task<Result<LeadDto>> ConvertLeadToCustomerAsync(int id, int? existingCustomerId = null, CancellationToken ct = default)
    {
        var lead = await _leads.Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null) return Result<LeadDto>.Failure("Lead not found.");
        if (lead.Status == LeadStatus.Converted && lead.ConvertedCustomerId is int existing)
        {
            var count = await _activities.Query().CountAsync(a => a.LeadId == id, ct);
            return Result<LeadDto>.Success(MapLead(lead, count));
        }

        Customer customer;
        if (existingCustomerId is int cid)
        {
            customer = await _customers.Query().FirstOrDefaultAsync(c => c.Id == cid, ct)
                ?? throw new InvalidOperationException("Customer not found.");
        }
        else
        {
            customer = new Customer
            {
                Name = lead.Name,
                Phone = lead.Phone,
                Email = lead.Email,
                CustomerType = CustomerType.Regular,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _user.CurrentUser?.Username ?? "system"
            };
            _customers.Add(customer);
            await _uow.SaveChangesAsync(ct);
        }

        lead.ConvertedCustomerId = customer.Id;
        lead.Status = LeadStatus.Converted;
        lead.UpdatedAt = DateTime.UtcNow;
        lead.UpdatedBy = _user.CurrentUser?.Username;
        await _uow.SaveChangesAsync(ct);

        await CreateFollowUpTaskAsync(lead.Id, customer.Id, "Follow up after lead conversion", ct);

        var actCount = await _activities.Query().CountAsync(a => a.LeadId == id, ct);
        return Result<LeadDto>.Success(MapLead(lead, actCount));
    }

    public async Task<Result<OpportunityDto>> ConvertLeadToOpportunityAsync(int id, ConvertLeadToOpportunityDto dto, CancellationToken ct = default)
    {
        var lead = await _leads.Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null) return Result<OpportunityDto>.Failure("Lead not found.");

        var companyId = TryCompanyId();
        if (companyId is null) return Result<OpportunityDto>.Failure("Company context is required.");

        var opp = new Opportunity
        {
            CompanyId = companyId.Value,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? $"{lead.Name} deal" : dto.Name.Trim(),
            LeadId = lead.Id,
            CustomerId = lead.ConvertedCustomerId,
            Stage = OpportunityStage.Prospect,
            Value = dto.Value,
            Probability = DefaultProbability(OpportunityStage.Prospect),
            ExpectedCloseDate = dto.ExpectedCloseDate,
            StageChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.CurrentUser?.Username ?? "system"
        };
        _opportunities.Add(opp);
        await _uow.SaveChangesAsync(ct);
        await AddStageHistoryAsync(opp, OpportunityStage.Prospect, OpportunityStage.Prospect, "Created from lead", ct);
        return Result<OpportunityDto>.Success(MapOpp(opp));
    }

    public async Task<PagedResult<CrmActivityDto>> GetActivitiesAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _activities.Query().AsNoTracking();

        if (query.Filters.TryGetValue("leadId", out var leadObj) && int.TryParse(leadObj?.ToString(), out var leadId))
            q = q.Where(a => a.LeadId == leadId);
        if (query.Filters.TryGetValue("customerId", out var custObj) && int.TryParse(custObj?.ToString(), out var customerId))
            q = q.Where(a => a.CustomerId == customerId);
        if (query.Filters.TryGetValue("myDay", out var myDay) && myDay is true or "true")
        {
            var uid = _user.CurrentUser?.Id;
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            q = q.Where(a => a.CompletedAt == null && a.AssignedToUserId == uid && a.DueAt != null && a.DueAt < tomorrow.AddDays(1));
        }
        if (query.Filters.TryGetValue("overdue", out var overdue) && overdue is true or "true")
        {
            var now = DateTime.UtcNow;
            q = q.Where(a => a.CompletedAt == null && a.DueAt != null && a.DueAt < now);
        }

        q = q.OrderByDescending(x => x.DueAt ?? x.CreatedAt);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<CrmActivityDto>
        {
            Items = paged.Items.Select(MapActivity).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<Result<CrmActivityDto>> CreateActivityAsync(CrmActivityCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Subject))
            return Result<CrmActivityDto>.Failure("Subject is required.");
        var companyId = TryCompanyId();
        if (companyId is null) return Result<CrmActivityDto>.Failure("Company context is required.");

        var entity = new CrmActivity
        {
            CompanyId = companyId.Value,
            Type = dto.Type,
            Subject = dto.Subject.Trim(),
            DueAt = dto.DueAt,
            LeadId = dto.LeadId,
            CustomerId = dto.CustomerId,
            AssignedToUserId = dto.AssignedToUserId ?? _user.CurrentUser?.Id,
            Notes = Norm(dto.Notes),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.CurrentUser?.Username ?? "system"
        };
        _activities.Add(entity);
        await _uow.SaveChangesAsync(ct);

        if (entity.AssignedToUserId is int)
        {
            await _notifications.CreateNotificationAsync(
                NotificationType.Success,
                "CRM task assigned",
                entity.Subject,
                "CrmActivity",
                entity.Id,
                ct);
        }

        return Result<CrmActivityDto>.Success(MapActivity(entity));
    }

    public async Task<Result<CrmActivityDto>> CompleteActivityAsync(int id, bool createNext = false, int nextDueDays = 7, CancellationToken ct = default)
    {
        var entity = await _activities.Query().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return Result<CrmActivityDto>.Failure("Activity not found.");
        entity.CompletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _user.CurrentUser?.Username;
        await _uow.SaveChangesAsync(ct);

        if (createNext)
        {
            await CreateActivityAsync(new CrmActivityCreateDto(
                entity.Type,
                $"Follow-up: {entity.Subject}",
                DateTime.UtcNow.AddDays(Math.Max(1, nextDueDays)),
                entity.LeadId,
                entity.CustomerId,
                entity.AssignedToUserId,
                "Auto follow-up"), ct);
        }

        return Result<CrmActivityDto>.Success(MapActivity(entity));
    }

    public async Task<Result> DeleteActivityAsync(int id, CancellationToken ct = default)
    {
        var entity = await _activities.Query().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return Result.Failure("Activity not found.");
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<PagedResult<OpportunityDto>> GetOpportunitiesAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _opportunities.Query().AsNoTracking();
        if (query.Filters.TryGetValue("stage", out var stageObj) && Enum.TryParse<OpportunityStage>(stageObj?.ToString(), true, out var stage))
            q = q.Where(o => o.Stage == stage);
        if (query.Filters.TryGetValue("leadId", out var leadObj) && int.TryParse(leadObj?.ToString(), out var leadId))
            q = q.Where(o => o.LeadId == leadId);
        if (query.Filters.TryGetValue("customerId", out var custObj) && int.TryParse(custObj?.ToString(), out var customerId))
            q = q.Where(o => o.CustomerId == customerId);
        if (query.Filters.TryGetValue("minValue", out var minObj) && decimal.TryParse(minObj?.ToString(), out var minVal))
            q = q.Where(o => o.Value >= minVal);

        q = q.OrderByDescending(x => x.CreatedAt);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<OpportunityDto>
        {
            Items = paged.Items.Select(MapOpp).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<OpportunityDto?> GetOpportunityByIdAsync(int id, CancellationToken ct = default)
    {
        var o = await _opportunities.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is null ? null : MapOpp(o);
    }

    public async Task<Result<OpportunityDto>> CreateOpportunityAsync(OpportunityCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<OpportunityDto>.Failure("Name is required.");
        var companyId = TryCompanyId();
        if (companyId is null) return Result<OpportunityDto>.Failure("Company context is required.");

        var stage = OpportunityStage.Prospect;
        var opp = new Opportunity
        {
            CompanyId = companyId.Value,
            Name = dto.Name.Trim(),
            LeadId = dto.LeadId,
            CustomerId = dto.CustomerId,
            Stage = stage,
            Value = dto.Value,
            Probability = dto.Probability > 0 ? Math.Clamp(dto.Probability, 0, 100) : DefaultProbability(stage),
            ExpectedCloseDate = dto.ExpectedCloseDate,
            StageChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.CurrentUser?.Username ?? "system"
        };
        _opportunities.Add(opp);
        await _uow.SaveChangesAsync(ct);
        await AddStageHistoryAsync(opp, stage, stage, "Created", ct);
        return Result<OpportunityDto>.Success(MapOpp(opp));
    }

    public async Task<Result<OpportunityDto>> UpdateOpportunityAsync(int id, OpportunityUpdateDto dto, CancellationToken ct = default)
    {
        var opp = await _opportunities.Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (opp is null) return Result<OpportunityDto>.Failure("Opportunity not found.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<OpportunityDto>.Failure("Name is required.");

        opp.Name = dto.Name.Trim();
        opp.LeadId = dto.LeadId;
        opp.CustomerId = dto.CustomerId;
        opp.Value = dto.Value;
        opp.Probability = Math.Clamp(dto.Probability, 0, 100);
        opp.ExpectedCloseDate = dto.ExpectedCloseDate;
        opp.UpdatedAt = DateTime.UtcNow;
        opp.UpdatedBy = _user.CurrentUser?.Username;
        await _uow.SaveChangesAsync(ct);
        return Result<OpportunityDto>.Success(MapOpp(opp));
    }

    public async Task<Result<OpportunityDto>> ChangeOpportunityStageAsync(int id, OpportunityStageChangeDto dto, CancellationToken ct = default)
    {
        var opp = await _opportunities.Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (opp is null) return Result<OpportunityDto>.Failure("Opportunity not found.");
        if (dto.Stage == OpportunityStage.Won && string.IsNullOrWhiteSpace(dto.WinReason))
            return Result<OpportunityDto>.Failure("Win reason is required.");
        if (dto.Stage == OpportunityStage.Lost && string.IsNullOrWhiteSpace(dto.LostReason))
            return Result<OpportunityDto>.Failure("Lost reason is required.");

        var from = opp.Stage;
        opp.Stage = dto.Stage;
        opp.Probability = DefaultProbability(dto.Stage);
        opp.WinReason = dto.Stage == OpportunityStage.Won ? dto.WinReason?.Trim() : opp.WinReason;
        opp.LostReason = dto.Stage == OpportunityStage.Lost ? dto.LostReason?.Trim() : opp.LostReason;
        opp.StageChangedAt = DateTime.UtcNow;
        opp.UpdatedAt = DateTime.UtcNow;
        opp.UpdatedBy = _user.CurrentUser?.Username;
        await _uow.SaveChangesAsync(ct);
        await AddStageHistoryAsync(opp, from, dto.Stage, dto.Note, ct);

        if (dto.Stage == OpportunityStage.Quoted)
            await CreateFollowUpTaskAsync(opp.LeadId, opp.CustomerId, $"Follow up quoted deal: {opp.Name}", ct);

        return Result<OpportunityDto>.Success(MapOpp(opp));
    }

    public async Task<Result<OpportunityDto>> LinkQuotationAsync(int id, int quotationId, CancellationToken ct = default)
    {
        var opp = await _opportunities.Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (opp is null) return Result<OpportunityDto>.Failure("Opportunity not found.");
        opp.QuotationId = quotationId;
        if (opp.Stage is OpportunityStage.Prospect)
        {
            var from = opp.Stage;
            opp.Stage = OpportunityStage.Quoted;
            opp.Probability = DefaultProbability(OpportunityStage.Quoted);
            opp.StageChangedAt = DateTime.UtcNow;
            await AddStageHistoryAsync(opp, from, OpportunityStage.Quoted, $"Linked quotation #{quotationId}", ct);
        }
        opp.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return Result<OpportunityDto>.Success(MapOpp(opp));
    }

    public async Task<IReadOnlyList<OpportunityStageHistoryDto>> GetStageHistoryAsync(int opportunityId, CancellationToken ct = default) =>
        await _stageHistory.Query().AsNoTracking()
            .Where(h => h.OpportunityId == opportunityId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new OpportunityStageHistoryDto(h.Id, h.FromStage, h.ToStage, h.ChangedBy, h.ChangedAt, h.Note))
            .ToListAsync(ct);

    public async Task<CrmPipelineDashboardDto> GetPipelineDashboardAsync(CancellationToken ct = default)
    {
        var openStages = new[] { OpportunityStage.Prospect, OpportunityStage.Quoted, OpportunityStage.Negotiation };
        var all = await _opportunities.Query().AsNoTracking().ToListAsync(ct);
        var open = all.Where(o => openStages.Contains(o.Stage)).ToList();
        var won = all.Count(o => o.Stage == OpportunityStage.Won);
        var lost = all.Count(o => o.Stage == OpportunityStage.Lost);
        var decided = won + lost;
        var byStage = Enum.GetValues<OpportunityStage>()
            .Select(s =>
            {
                var items = all.Where(o => o.Stage == s).ToList();
                return new CrmStageBucketDto(s, items.Count, items.Sum(i => i.Value), items.Sum(i => i.Value * i.Probability / 100m));
            })
            .ToList();

        return new CrmPipelineDashboardDto(
            open.Sum(o => o.Value),
            open.Sum(o => o.Value * o.Probability / 100m),
            open.Count,
            won,
            lost,
            decided == 0 ? 0 : (double)won / decided,
            byStage);
    }

    public async Task<Customer360Dto?> GetCustomer360Async(int customerId, CancellationToken ct = default)
    {
        var customer = await _customers.Query().AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null) return null;

        var leads = await _leads.Query().AsNoTracking().Where(l => l.ConvertedCustomerId == customerId).ToListAsync(ct);
        var activities = await _activities.Query().AsNoTracking().Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.DueAt ?? a.CreatedAt).Take(50).ToListAsync(ct);
        var opps = await _opportunities.Query().AsNoTracking().Where(o => o.CustomerId == customerId).ToListAsync(ct);

        var invoices = await _invoices.Query().AsNoTracking().Where(i => i.CustomerId == customerId)
            .OrderByDescending(i => i.InvoiceDate).Take(20).ToListAsync(ct);
        var orders = await _orders.Query().AsNoTracking().Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDate).Take(20).ToListAsync(ct);
        var returns = await _returns.Query().AsNoTracking().Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.ReturnDate).Take(20).ToListAsync(ct);

        var profitability = 0m;
        try
        {
            var margins = await _invoices.Query().AsNoTracking()
                .Where(i => i.CustomerId == customerId)
                .SelectMany(i => i.Lines.Select(l => l.LineTotal - (l.UnitCost * l.Quantity)))
                .ToListAsync(ct);
            profitability = margins.Sum();
        }
        catch
        {
            profitability = invoices.Sum(i => i.GrandTotal) * 0.25m;
        }

        return new Customer360Dto(
            customer.Id,
            customer.Name,
            customer.Phone,
            customer.Email,
            customer.CreditLimit,
            customer.Balance,
            customer.Balance,
            profitability,
            leads.Select(l => MapLead(l, 0)).ToList(),
            activities.Select(MapActivity).ToList(),
            opps.Select(MapOpp).ToList(),
            invoices.Select(i => new Customer360DocDto(i.Id, i.InvoiceNumber, i.InvoiceDate, i.GrandTotal, i.PaymentStatus.ToString())).ToList(),
            orders.Select(o => new Customer360DocDto(o.Id, o.OrderNumber, o.OrderDate, o.GrandTotal, o.Status.ToString())).ToList(),
            returns.Select(r => new Customer360DocDto(r.Id, r.ReturnNumber, r.ReturnDate, r.GrandTotal, r.Status.ToString())).ToList());
    }

    public async Task<IReadOnlyList<CrmAssignmentRuleDto>> GetAssignmentRulesAsync(CancellationToken ct = default) =>
        await _rules.Query().AsNoTracking()
            .OrderByDescending(r => r.IsDefault)
            .Select(r => new CrmAssignmentRuleDto(r.Id, r.Source, r.OwnerUserId, r.IsDefault, r.IsActive))
            .ToListAsync(ct);

    public async Task<Result<CrmAssignmentRuleDto>> UpsertAssignmentRuleAsync(CrmAssignmentRuleDto dto, CancellationToken ct = default)
    {
        var companyId = TryCompanyId();
        if (companyId is null) return Result<CrmAssignmentRuleDto>.Failure("Company context is required.");

        CrmAssignmentRule entity;
        if (dto.Id > 0)
        {
            entity = await _rules.Query().FirstOrDefaultAsync(r => r.Id == dto.Id, ct)
                ?? throw new InvalidOperationException("Rule not found.");
            entity.Source = Norm(dto.Source);
            entity.OwnerUserId = dto.OwnerUserId;
            entity.IsDefault = dto.IsDefault;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            entity = new CrmAssignmentRule
            {
                CompanyId = companyId.Value,
                Source = Norm(dto.Source),
                OwnerUserId = dto.OwnerUserId,
                IsDefault = dto.IsDefault,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _user.CurrentUser?.Username ?? "system"
            };
            _rules.Add(entity);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<CrmAssignmentRuleDto>.Success(new CrmAssignmentRuleDto(entity.Id, entity.Source, entity.OwnerUserId, entity.IsDefault, entity.IsActive));
    }

    public async Task<IReadOnlyList<CrmEmailTemplateDto>> GetEmailTemplatesAsync(CancellationToken ct = default) =>
        await _templates.Query().AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new CrmEmailTemplateDto(t.Id, t.Name, t.Subject, t.Body, t.IsActive))
            .ToListAsync(ct);

    public async Task<Result<CrmEmailTemplateDto>> UpsertEmailTemplateAsync(CrmEmailTemplateDto dto, CancellationToken ct = default)
    {
        var companyId = TryCompanyId();
        if (companyId is null) return Result<CrmEmailTemplateDto>.Failure("Company context is required.");
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Subject))
            return Result<CrmEmailTemplateDto>.Failure("Name and subject are required.");

        CrmEmailTemplate entity;
        if (dto.Id > 0)
        {
            entity = await _templates.Query().FirstOrDefaultAsync(t => t.Id == dto.Id, ct)
                ?? throw new InvalidOperationException("Template not found.");
            entity.Name = dto.Name.Trim();
            entity.Subject = dto.Subject.Trim();
            entity.Body = dto.Body ?? "";
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            entity = new CrmEmailTemplate
            {
                CompanyId = companyId.Value,
                Name = dto.Name.Trim(),
                Subject = dto.Subject.Trim(),
                Body = dto.Body ?? "",
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _user.CurrentUser?.Username ?? "system"
            };
            _templates.Add(entity);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<CrmEmailTemplateDto>.Success(new CrmEmailTemplateDto(entity.Id, entity.Name, entity.Subject, entity.Body, entity.IsActive));
    }

    private async Task<int?> ResolveOwnerAsync(string? source, CancellationToken ct)
    {
        var rules = await _rules.Query().AsNoTracking().Where(r => r.IsActive).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(source))
        {
            var match = rules.FirstOrDefault(r => string.Equals(r.Source, source, StringComparison.OrdinalIgnoreCase));
            if (match?.OwnerUserId is int oid) return oid;
        }
        return rules.FirstOrDefault(r => r.IsDefault)?.OwnerUserId;
    }

    private async Task CreateFollowUpTaskAsync(int? leadId, int? customerId, string subject, CancellationToken ct)
    {
        var companyId = TryCompanyId();
        if (companyId is null) return;
        _activities.Add(new CrmActivity
        {
            CompanyId = companyId.Value,
            Type = CrmActivityType.Task,
            Subject = subject,
            DueAt = DateTime.UtcNow.AddDays(3),
            LeadId = leadId,
            CustomerId = customerId,
            AssignedToUserId = _user.CurrentUser?.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "crm-automation"
        });
        await _uow.SaveChangesAsync(ct);
        await _notifications.CreateNotificationAsync(NotificationType.Success, "CRM follow-up", subject, "CrmActivity", null, ct);
    }

    private async Task AddStageHistoryAsync(Opportunity opp, OpportunityStage from, OpportunityStage to, string? note, CancellationToken ct)
    {
        _stageHistory.Add(new OpportunityStageHistory
        {
            CompanyId = opp.CompanyId,
            OpportunityId = opp.Id,
            FromStage = from,
            ToStage = to,
            ChangedBy = _user.CurrentUser?.Username ?? "system",
            ChangedAt = DateTime.UtcNow,
            Note = note,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.CurrentUser?.Username ?? "system"
        });
        await _uow.SaveChangesAsync(ct);
    }

    private static int DefaultProbability(OpportunityStage stage) => stage switch
    {
        OpportunityStage.Prospect => 10,
        OpportunityStage.Quoted => 40,
        OpportunityStage.Negotiation => 60,
        OpportunityStage.Won => 100,
        OpportunityStage.Lost => 0,
        _ => 10
    };

    private static string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static LeadDto MapLead(Lead x, int activityCount) =>
        new(x.Id, x.Name, x.Phone, x.Email, x.Source, x.Status, x.Notes, x.LostReason, x.OwnerUserId, x.ConvertedCustomerId, x.CreatedAt,
            ComputeScore(x, activityCount));

    private static int ComputeScore(Lead x, int activityCount)
    {
        var score = x.Status switch
        {
            LeadStatus.New => 10,
            LeadStatus.Contacted => 30,
            LeadStatus.Qualified => 60,
            LeadStatus.Lost => 0,
            LeadStatus.Converted => 100,
            _ => 10
        };
        score += Math.Min(30, activityCount * 5);
        if (!string.IsNullOrWhiteSpace(x.Source)) score += 5;
        if (!string.IsNullOrWhiteSpace(x.Phone)) score += 5;
        return Math.Clamp(score, 0, 100);
    }

    private static CrmActivityDto MapActivity(CrmActivity a) =>
        new(a.Id, a.Type, a.Subject, a.DueAt, a.CompletedAt, a.LeadId, a.CustomerId, a.AssignedToUserId, a.Notes, a.AttachmentPath, a.AttachmentName);

    private static OpportunityDto MapOpp(Opportunity o) =>
        new(o.Id, o.Name, o.LeadId, o.CustomerId, o.Stage, o.Value, o.Probability, o.Value * o.Probability / 100m,
            o.ExpectedCloseDate, o.QuotationId, o.LostReason, o.WinReason, o.StageChangedAt);
}
