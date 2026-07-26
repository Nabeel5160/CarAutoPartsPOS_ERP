using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Settings;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Company settings read and update.</summary>
public class SettingsService : ISettingsService
{
    private readonly IRepository<CompanySettings> _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SettingsService(
        IRepository<CompanySettings> settings,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<CompanySettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var entity = await GetOrCreateSettingsAsync(ct);
        return _mapper.Map<CompanySettingsDto>(entity);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateSettingsAsync(CompanySettingsDto dto, CancellationToken ct = default)
    {
        var entity = await GetOrCreateSettingsAsync(ct);
        entity.CompanyName = dto.CompanyName;
        entity.LogoPath = dto.LogoPath;
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
        entity.UpdatedAt = DateTime.UtcNow;

        _settings.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
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

/// <summary>Database backup and restore orchestration.</summary>
public class BackupService : IBackupService
{
    private readonly IRepository<BackupHistory> _history;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public BackupService(
        IRepository<BackupHistory> history,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _history = history;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreateBackupAsync(bool isAutomatic, CancellationToken ct = default)
    {
        var fileName = $"CarAutoParts_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
        var filePath = Path.Combine(Path.GetTempPath(), "CarAutoParts", "Backups", fileName);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(filePath, $"Backup placeholder created at {DateTime.UtcNow:O}", ct);

            var entry = new BackupHistory
            {
                FilePath = filePath,
                FileSizeBytes = new FileInfo(filePath).Length,
                BackupType = isAutomatic ? Domain.Enums.BackupType.Automatic : Domain.Enums.BackupType.Manual,
                IsSuccessful = true,
                BackupDate = DateTime.UtcNow
            };

            _history.Add(entry);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<string>.Success(filePath);
        }
        catch (Exception ex)
        {
            _history.Add(new BackupHistory
            {
                FilePath = filePath,
                BackupType = isAutomatic ? Domain.Enums.BackupType.Automatic : Domain.Enums.BackupType.Manual,
                IsSuccessful = false,
                ErrorMessage = ex.Message,
                BackupDate = DateTime.UtcNow
            });
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<string>.Failure(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result> RestoreBackupAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return Result.Failure("Backup file not found.");

        await Task.CompletedTask;
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BackupHistoryDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        var items = await _history.Query()
            .Where(h => !h.IsDeleted)
            .OrderByDescending(h => h.BackupDate)
            .Take(50)
            .ToListAsync(ct);

        return _mapper.Map<List<BackupHistoryDto>>(items);
    }
}
