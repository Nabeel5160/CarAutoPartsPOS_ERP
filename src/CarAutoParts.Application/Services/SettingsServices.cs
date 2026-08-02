using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.DTOs.Settings;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Company settings read and update.</summary>
public class SettingsService : ISettingsService
{
    private readonly IRepository<CompanySettings> _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAppConfigService _appConfig;

    public SettingsService(
        IRepository<CompanySettings> settings,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IAppConfigService appConfig)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _appConfig = appConfig;
    }

    /// <inheritdoc />
    public async Task<CompanySettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var entity = await GetOrCreateSettingsAsync(ct);
        if (MigrateLogoFields(entity))
        {
            _settings.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        return _mapper.Map<CompanySettingsDto>(entity);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateSettingsAsync(CompanySettingsDto dto, CancellationToken ct = default)
    {
        var entity = await GetOrCreateSettingsAsync(ct);
        entity.CompanyName = dto.CompanyName;
        entity.Address = dto.Address;
        entity.City = dto.City;
        entity.Phone = dto.Phone;
        entity.Email = dto.Email;
        entity.Ntn = dto.Ntn;
        entity.Strn = dto.Strn;
        entity.PosId = dto.PosId;
        entity.DefaultTaxRate = dto.DefaultTaxRate;
        entity.InvoicePrefix = dto.InvoicePrefix;
        entity.InvoiceFooter = dto.InvoiceFooter;
        entity.PrinterName = dto.PrinterName;
        entity.Theme = dto.Theme;
        entity.AutoBackupEnabled = dto.AutoBackupEnabled;
        entity.AutoBackupIntervalHours = dto.AutoBackupIntervalHours;
        entity.FbrUseSandbox = dto.FbrUseSandbox;
        entity.FbrTimeoutSeconds = dto.FbrTimeoutSeconds;
        entity.GrnOverReceivePercent = dto.GrnOverReceivePercent;
        entity.GrnUnderReceiveAllowed = dto.GrnUnderReceiveAllowed;
        entity.ThreeWayQtyTolerancePercent = dto.ThreeWayQtyTolerancePercent;
        entity.ThreeWayPriceTolerancePercent = dto.ThreeWayPriceTolerancePercent;
        entity.AllowNegativeStock = dto.AllowNegativeStock;
        entity.DefaultValuationMethod = Enum.TryParse<ValuationMethod>(dto.DefaultValuationMethod, true, out var vm)
            ? vm
            : ValuationMethod.Average;
        entity.OpeningBalanceDate = dto.OpeningBalanceDate?.Date;
        entity.VerticalKey = VerticalProfiles.Normalize(dto.VerticalKey);

        // Prefer LogoUrl; keep LogoPath in sync when client sends either.
        var logoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim();
        var logoPath = string.IsNullOrWhiteSpace(dto.LogoPath) ? null : dto.LogoPath.Trim();
        if (logoUrl is null && logoPath is not null)
            logoUrl = logoPath;
        if (logoPath is null && logoUrl is not null)
            logoPath = logoUrl;
        entity.LogoUrl = logoUrl;
        entity.LogoPath = logoPath;
        entity.UpdatedAt = DateTime.UtcNow;

        _settings.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        _appConfig.InvalidateCache();
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<string>> SetLogoAsync(string logoUrl, string? logoPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return Result<string>.Failure("Logo URL is required.");

        var entity = await GetOrCreateSettingsAsync(ct);
        var url = logoUrl.Trim();
        var path = string.IsNullOrWhiteSpace(logoPath) ? url : logoPath.Trim();
        entity.LogoUrl = url;
        entity.LogoPath = path;
        entity.UpdatedAt = DateTime.UtcNow;
        _settings.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        _appConfig.InvalidateCache();
        return Result<string>.Success(url);
    }

    /// <inheritdoc />
    public async Task<Result<string?>> ClearLogoAsync(CancellationToken ct = default)
    {
        var entity = await GetOrCreateSettingsAsync(ct);
        var previous = entity.LogoPath ?? entity.LogoUrl;
        entity.LogoUrl = null;
        entity.LogoPath = null;
        entity.UpdatedAt = DateTime.UtcNow;
        _settings.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        _appConfig.InvalidateCache();
        return Result<string?>.Success(previous);
    }

    /// <summary>If LogoUrl is empty but LogoPath holds a usable web path/URL, copy it across.</summary>
    private static bool MigrateLogoFields(CompanySettings entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.LogoUrl))
        {
            if (string.IsNullOrWhiteSpace(entity.LogoPath))
            {
                entity.LogoPath = entity.LogoUrl;
                entity.UpdatedAt = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        if (string.IsNullOrWhiteSpace(entity.LogoPath))
            return false;

        var path = entity.LogoPath.Trim();
        if (path.StartsWith('/') || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            entity.LogoUrl = path;
            entity.UpdatedAt = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    private async Task<CompanySettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var entity = await _settings.Query().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (entity != null) return entity;

        entity = new CompanySettings();
        _settings.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity;
    }
}
