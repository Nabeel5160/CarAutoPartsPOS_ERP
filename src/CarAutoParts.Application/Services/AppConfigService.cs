using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CarAutoParts.Application.Services;

public interface IAppConfigService
{
    Task<AppConfigDto> GetAsync(CancellationToken ct = default);
    Task<PublicAppConfigDto> GetPublicAsync(CancellationToken ct = default);
    Task<Result<AppConfigDto>> UpdateAsync(AppConfigUpdateRequest request, CancellationToken ct = default);
    void InvalidateCache();
}

public interface IFeatureGate
{
    Task<bool> EnabledAsync(string scope, string key, CancellationToken ct = default);
    Task<bool> ModuleEnabledAsync(string moduleKey, CancellationToken ct = default);
    Task<bool> BehaviorEnabledAsync(string behaviorKey, CancellationToken ct = default);
    Task<FieldConfigDto> GetFieldAsync(string fieldKey, CancellationToken ct = default);
    Task<string> GetBrandAsync(string brandKey, string fallback, CancellationToken ct = default);
}

public sealed class AppConfigService : IAppConfigService, IFeatureGate
{
    private const string CacheKey = "appconfig:resolved";
    private readonly IRepository<AppConfigEntry> _entries;
    private readonly IRepository<CompanySettings> _settings;
    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;

    public AppConfigService(
        IRepository<AppConfigEntry> entries,
        IRepository<CompanySettings> settings,
        IUnitOfWork uow,
        IMemoryCache cache)
    {
        _entries = entries;
        _settings = settings;
        _uow = uow;
        _cache = cache;
    }

    public async Task<AppConfigDto> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out AppConfigDto? cached) && cached is not null)
            return cached;

        var resolved = await ResolveAsync(ct);
        _cache.Set(CacheKey, resolved, TimeSpan.FromMinutes(5));
        return resolved;
    }

    public async Task<PublicAppConfigDto> GetPublicAsync(CancellationToken ct = default)
    {
        var full = await GetAsync(ct);
        var labels = full.Labels.TryGetValue("en", out var en)
            ? en
            : (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();
        return new PublicAppConfigDto(full.Branding, "en", labels);
    }

    public async Task<Result<AppConfigDto>> UpdateAsync(AppConfigUpdateRequest request, CancellationToken ct = default)
    {
        var settings = await _settings.Query().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (settings is null)
        {
            settings = new CompanySettings();
            _settings.Add(settings);
            await _uow.SaveChangesAsync(ct);
        }

        var vertical = VerticalProfiles.Normalize(request.VerticalKey ?? settings.VerticalKey);
        if (request.VerticalKey is not null && !VerticalProfiles.IsKnown(request.VerticalKey) &&
            !string.Equals(request.VerticalKey, settings.VerticalKey, StringComparison.OrdinalIgnoreCase))
            return Result<AppConfigDto>.Failure($"Unknown vertical '{request.VerticalKey}'.");

        settings.VerticalKey = vertical;
        settings.UpdatedAt = DateTime.UtcNow;

        if (request.ApplyPresetDefaults)
        {
            await ClearOverridesAsync(ct);
            var brand = VerticalProfiles.DefaultBrand(vertical, settings.CompanyName);
            settings.CompanyName = brand.GetValueOrDefault(ConfigKeys.BrandAppName, settings.CompanyName);
            if (brand.TryGetValue(ConfigKeys.BrandLogoUrl, out var logo) && !string.IsNullOrWhiteSpace(logo))
                settings.LogoUrl = logo;
            _settings.Update(settings);
            await _uow.SaveChangesAsync(ct);
            InvalidateCache();
            return Result<AppConfigDto>.Success(await GetAsync(ct));
        }

        var errors = new List<string>();
        if (request.Modules is not null)
        {
            foreach (var (key, value) in request.Modules)
            {
                if (!VerticalProfiles.KnownModuleKeys.Contains(key))
                {
                    errors.Add($"Unknown module key '{key}'.");
                    continue;
                }
                await UpsertAsync(ConfigScopes.Module, key, null, value ? "true" : "false", ct);
            }
        }

        if (request.Fields is not null)
        {
            foreach (var (key, field) in request.Fields)
            {
                if (!VerticalProfiles.KnownFieldKeys.Contains(key))
                {
                    errors.Add($"Unknown field key '{key}'.");
                    continue;
                }
                await UpsertAsync(ConfigScopes.Field, key, null, VerticalProfiles.SerializeField(field), ct);
            }
        }

        if (request.Behaviors is not null)
        {
            foreach (var (key, value) in request.Behaviors)
            {
                if (!VerticalProfiles.KnownBehaviorKeys.Contains(key))
                {
                    errors.Add($"Unknown behavior key '{key}'.");
                    continue;
                }
                await UpsertAsync(ConfigScopes.Behavior, key, null, value, ct);
            }
        }

        if (request.Brand is not null)
        {
            foreach (var (key, value) in request.Brand)
            {
                if (!VerticalProfiles.KnownBrandKeys.Contains(key))
                {
                    errors.Add($"Unknown brand key '{key}'.");
                    continue;
                }
                await UpsertAsync(ConfigScopes.Brand, key, null, value ?? "", ct);
                if (key.Equals(ConfigKeys.BrandAppName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                    settings.CompanyName = value;
                if (key.Equals(ConfigKeys.BrandLogoUrl, StringComparison.OrdinalIgnoreCase))
                    settings.LogoUrl = string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        if (request.Labels is not null)
        {
            foreach (var (culture, map) in request.Labels)
            {
                foreach (var (key, value) in map)
                    await UpsertAsync(ConfigScopes.Label, key, culture, value ?? "", ct);
            }
        }

        if (errors.Count > 0)
            return Result<AppConfigDto>.Failure(string.Join(" ", errors));

        _settings.Update(settings);
        await _uow.SaveChangesAsync(ct);
        InvalidateCache();
        return Result<AppConfigDto>.Success(await GetAsync(ct));
    }

    public void InvalidateCache() => _cache.Remove(CacheKey);

    public async Task<bool> EnabledAsync(string scope, string key, CancellationToken ct = default)
    {
        var cfg = await GetAsync(ct);
        return scope.ToLowerInvariant() switch
        {
            ConfigScopes.Module => cfg.Modules.TryGetValue(key, out var m) && m,
            ConfigScopes.Behavior => cfg.Behaviors.TryGetValue(key, out var b) &&
                                     (string.Equals(b, "true", StringComparison.OrdinalIgnoreCase) ||
                                      (!string.Equals(b, "false", StringComparison.OrdinalIgnoreCase) &&
                                       !string.IsNullOrWhiteSpace(b) &&
                                       key is ConfigKeys.BehCurrency or ConfigKeys.BehDecimals)),
            _ => false
        };
    }

    public Task<bool> ModuleEnabledAsync(string moduleKey, CancellationToken ct = default) =>
        EnabledAsync(ConfigScopes.Module, moduleKey, ct);

    public async Task<bool> BehaviorEnabledAsync(string behaviorKey, CancellationToken ct = default)
    {
        var cfg = await GetAsync(ct);
        return cfg.Behaviors.TryGetValue(behaviorKey, out var b) &&
               string.Equals(b, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<FieldConfigDto> GetFieldAsync(string fieldKey, CancellationToken ct = default)
    {
        var cfg = await GetAsync(ct);
        return cfg.Fields.TryGetValue(fieldKey, out var f)
            ? f
            : new FieldConfigDto(false, false, fieldKey);
    }

    public async Task<string> GetBrandAsync(string brandKey, string fallback, CancellationToken ct = default)
    {
        var cfg = await GetAsync(ct);
        return brandKey switch
        {
            ConfigKeys.BrandAppName => cfg.Branding.AppName,
            ConfigKeys.BrandShortName => cfg.Branding.ShortName,
            ConfigKeys.BrandAccentWord => cfg.Branding.AccentWord,
            ConfigKeys.BrandLogoUrl => cfg.Branding.LogoUrl ?? fallback,
            ConfigKeys.BrandTheme => cfg.Branding.Theme,
            ConfigKeys.BrandAccent => cfg.Branding.Accent,
            _ => fallback
        };
    }

    private async Task<AppConfigDto> ResolveAsync(CancellationToken ct)
    {
        var settings = await _settings.Query().AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        var vertical = VerticalProfiles.Normalize(settings?.VerticalKey);
        var modules = VerticalProfiles.DefaultModules(vertical);
        var fields = VerticalProfiles.DefaultFields(vertical);
        var behaviors = VerticalProfiles.DefaultBehaviors(vertical);
        var brand = VerticalProfiles.DefaultBrand(vertical, settings?.CompanyName);
        var labels = VerticalProfiles.DefaultLabels(vertical);

        if (!string.IsNullOrWhiteSpace(settings?.LogoUrl))
            brand[ConfigKeys.BrandLogoUrl] = settings.LogoUrl!;
        if (!string.IsNullOrWhiteSpace(settings?.CompanyName))
            brand[ConfigKeys.BrandAppName] = settings.CompanyName;

        var overrides = await _entries.Query().AsNoTracking()
            .Where(e => !e.IsDeleted)
            .ToListAsync(ct);

        foreach (var entry in overrides)
        {
            switch (entry.Scope.ToLowerInvariant())
            {
                case ConfigScopes.Module when VerticalProfiles.KnownModuleKeys.Contains(entry.Key):
                    modules[entry.Key] = IsTruthy(entry.Value);
                    break;
                case ConfigScopes.Field when VerticalProfiles.KnownFieldKeys.Contains(entry.Key):
                    fields[entry.Key] = VerticalProfiles.DeserializeField(entry.Value,
                        fields.GetValueOrDefault(entry.Key) ?? new FieldConfigDto(false, false, entry.Key));
                    break;
                case ConfigScopes.Behavior when VerticalProfiles.KnownBehaviorKeys.Contains(entry.Key):
                    behaviors[entry.Key] = entry.Value;
                    break;
                case ConfigScopes.Brand when VerticalProfiles.KnownBrandKeys.Contains(entry.Key):
                    brand[entry.Key] = entry.Value;
                    break;
                case ConfigScopes.Label:
                {
                    var culture = string.IsNullOrWhiteSpace(entry.Culture) ? "en" : entry.Culture!;
                    if (!labels.TryGetValue(culture, out var map))
                    {
                        map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        labels[culture] = map;
                    }
                    map[entry.Key] = entry.Value;
                    break;
                }
            }
        }

        var branding = new BrandingDto(
            brand.GetValueOrDefault(ConfigKeys.BrandAppName, "Car Auto Parts"),
            brand.GetValueOrDefault(ConfigKeys.BrandShortName, "Car Auto"),
            brand.GetValueOrDefault(ConfigKeys.BrandAccentWord, "Parts"),
            string.IsNullOrWhiteSpace(brand.GetValueOrDefault(ConfigKeys.BrandLogoUrl))
                ? null
                : brand[ConfigKeys.BrandLogoUrl],
            brand.GetValueOrDefault(ConfigKeys.BrandTheme, "dark"),
            brand.GetValueOrDefault(ConfigKeys.BrandAccent, "amber"),
            vertical);

        var labelMaps = labels.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyDictionary<string, string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);

        return new AppConfigDto(
            vertical,
            branding,
            modules,
            fields,
            behaviors,
            labelMaps,
            VerticalProfiles.All);
    }

    private async Task UpsertAsync(string scope, string key, string? culture, string value, CancellationToken ct)
    {
        var existing = await _entries.Query()
            .FirstOrDefaultAsync(e => !e.IsDeleted && e.Scope == scope && e.Key == key &&
                                      ((e.Culture == null && culture == null) || e.Culture == culture), ct);
        if (existing is null)
        {
            _entries.Add(new AppConfigEntry
            {
                Scope = scope,
                Key = key,
                Culture = culture,
                Value = value,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
            _entries.Update(existing);
        }
    }

    private async Task ClearOverridesAsync(CancellationToken ct)
    {
        var all = await _entries.Query().Where(e => !e.IsDeleted).ToListAsync(ct);
        foreach (var e in all)
        {
            e.IsDeleted = true;
            e.UpdatedAt = DateTime.UtcNow;
            _entries.Update(e);
        }
        await _uow.SaveChangesAsync(ct);
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
}
