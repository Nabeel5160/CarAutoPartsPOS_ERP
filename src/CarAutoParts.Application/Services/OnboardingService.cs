using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public record OnboardingStepDto(string Key, string Title, bool Done, string? Href, string? Hint);
public record OnboardingStatusDto(bool IsComplete, DateTime? SetupCompletedAt, IReadOnlyList<OnboardingStepDto> Steps);
public record CompleteOnboardingDto(
    string CompanyName,
    string? Ntn,
    string? Strn,
    string? City,
    string? Address,
    string? Phone,
    string? Email,
    string? PosId,
    decimal DefaultTaxRate = 18m,
    bool FbrUseSandbox = true,
    string DefaultValuationMethod = "Fifo",
    string VerticalKey = "auto-parts");

public interface IOnboardingService
{
    Task<OnboardingStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<Result> CompleteAsync(CompleteOnboardingDto dto, CancellationToken ct = default);
}

public sealed class OnboardingService : IOnboardingService
{
    private readonly IRepository<CompanySettings> _settings;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IRepository<AccountMapping> _maps;
    private readonly IRepository<AppUser> _users;
    private readonly IRepository<Till> _tills;
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly IFeatureGate _features;
    private readonly IAppConfigService _appConfig;
    private readonly ICurrentCompanyContext _company;

    public OnboardingService(
        IRepository<CompanySettings> settings,
        IRepository<Warehouse> warehouses,
        IRepository<FiscalYear> fiscalYears,
        IRepository<AccountMapping> maps,
        IRepository<AppUser> users,
        IRepository<Till> tills,
        IRepository<Branch> branches,
        IUnitOfWork uow,
        ICurrentUserService user,
        IFeatureGate features,
        IAppConfigService appConfig,
        ICurrentCompanyContext company)
    {
        _settings = settings;
        _warehouses = warehouses;
        _fiscalYears = fiscalYears;
        _maps = maps;
        _users = users;
        _tills = tills;
        _branches = branches;
        _uow = uow;
        _user = user;
        _features = features;
        _appConfig = appConfig;
        _company = company;
    }

    public async Task<OnboardingStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await _settings.Query().AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        var complete = settings?.SetupCompletedAt != null;
        var hasWarehouse = await _warehouses.Query().AnyAsync(w => !w.IsDeleted, ct);
        var hasFy = await _fiscalYears.Query().AnyAsync(f => !f.IsDeleted, ct);
        var hasMaps = await _maps.Query().AnyAsync(m => !m.IsDeleted, ct);
        var hasExtraUsers = await _users.Query().CountAsync(u => !u.IsDeleted && u.Username != "admin", ct) > 0;
        var hasTill = await _tills.Query().AnyAsync(t => !t.IsDeleted && t.IsActive, ct);
        var fbrOn = await _features.BehaviorEnabledAsync(ConfigKeys.BehFbrEnabled, ct);
        var verticalSet = !string.IsNullOrWhiteSpace(settings?.VerticalKey);

        var steps = new List<OnboardingStepDto>
        {
            new("vertical", "Business type", verticalSet, "/onboarding", "Auto parts, bike parts, or general retail"),
            new("company", "Company profile", !string.IsNullOrWhiteSpace(settings?.CompanyName) && !string.IsNullOrWhiteSpace(settings?.Ntn), "/onboarding", "Name, NTN, city, tax rate — then add logo under Settings"),
            new("fiscal", "Fiscal year / periods", hasFy, "/periods", "Jul–Jun FY seeded by platform"),
            new("warehouse", "Default warehouse", hasWarehouse, "/warehouses", "At least one warehouse"),
            new("till", "First till", hasTill, "/onboarding", "Counter till for open shift (created on finish if missing)"),
            new("coa", "Chart of accounts maps", hasMaps, "/account-maps", "Sales/GRN/AP/Payment maps"),
            new("opening", "Opening balances (optional)", settings?.OpeningBalanceDate != null, "/opening-balances", "Cutover pack when going live"),
            new("users", "Team users", hasExtraUsers, "/users", "Apply Cashier / Accountant templates"),
        };
        if (fbrOn)
            steps.Add(new("fbr", "FBR sandbox", settings?.FbrUseSandbox == true || complete, "/settings", "Sandbox first — see docs/FBR-PRODUCTION.md before prod token"));

        return new OnboardingStatusDto(complete, settings?.SetupCompletedAt, steps);
    }

    public async Task<Result> CompleteAsync(CompleteOnboardingDto dto, CancellationToken ct = default)
    {
        if (!_user.HasPermission(Permissions.SettingsManage) && !_user.HasPermission(Permissions.PlatformManage))
            return Result.Failure("Settings manage permission required.");

        if (string.IsNullOrWhiteSpace(dto.CompanyName))
            return Result.Failure("Company name is required.");

        var entity = await _settings.Query().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (entity is null)
        {
            entity = new CompanySettings();
            _settings.Add(entity);
        }

        var vertical = VerticalProfiles.Normalize(dto.VerticalKey);
        var verticalChanged = !string.Equals(entity.VerticalKey, vertical, StringComparison.OrdinalIgnoreCase);

        entity.CompanyName = dto.CompanyName.Trim();
        entity.Ntn = dto.Ntn?.Trim();
        entity.Strn = dto.Strn?.Trim();
        entity.City = dto.City?.Trim();
        entity.Address = dto.Address?.Trim();
        entity.Phone = dto.Phone?.Trim();
        entity.Email = dto.Email?.Trim();
        entity.PosId = dto.PosId?.Trim();
        entity.DefaultTaxRate = dto.DefaultTaxRate > 0 ? dto.DefaultTaxRate : 18m;
        entity.FbrUseSandbox = dto.FbrUseSandbox;
        entity.VerticalKey = vertical;
        entity.DefaultValuationMethod = Enum.TryParse<Domain.Enums.ValuationMethod>(dto.DefaultValuationMethod, true, out var vm)
            ? vm
            : Domain.Enums.ValuationMethod.Fifo;
        entity.SetupCompletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        _settings.Update(entity);
        await EnsureFirstTillAsync(ct);
        await _uow.SaveChangesAsync(ct);

        if (verticalChanged)
        {
            await _appConfig.UpdateAsync(new AppConfigUpdateRequest(
                vertical, ApplyPresetDefaults: true, null, null, null, null, null), ct);
        }

        return Result.Success();
    }

    /// <summary>Auto-parts single-branch path: ensure TILL-01 exists so POS can open a shift without SQL.</summary>
    private async Task EnsureFirstTillAsync(CancellationToken ct)
    {
        if (await _tills.Query().AnyAsync(t => !t.IsDeleted && t.IsActive, ct))
            return;

        var companyId = _company.CompanyId
            ?? await _branches.Query().Where(b => !b.IsDeleted).Select(b => (int?)b.CompanyId).FirstOrDefaultAsync(ct)
            ?? 0;
        if (companyId <= 0) return;

        var branch = await _branches.Query()
            .Where(b => !b.IsDeleted && b.CompanyId == companyId)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.Id)
            .FirstOrDefaultAsync(ct);
        if (branch is null) return;

        var warehouseId = await _warehouses.Query()
            .Where(w => !w.IsDeleted && (w.BranchId == null || w.BranchId == branch.Id))
            .OrderBy(w => w.Id)
            .Select(w => (int?)w.Id)
            .FirstOrDefaultAsync(ct);

        _tills.Add(new Till
        {
            CompanyId = companyId,
            BranchId = branch.Id,
            Code = "TILL-01",
            Name = "Front Counter",
            WarehouseId = warehouseId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.CurrentUser?.Username ?? "onboarding"
        });
    }
}
