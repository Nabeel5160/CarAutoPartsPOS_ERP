using CarAutoParts.Application.Interfaces;
using CarAutoParts.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Infrastructure.Services;

public class BackupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupBackgroundService> _logger;

    public BackupBackgroundService(IServiceScopeFactory scopeFactory, ILogger<BackupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunBackupIfDueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic backup check failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task RunBackupIfDueAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

        var settings = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null || !settings.AutoBackupEnabled || settings.AutoBackupIntervalHours <= 0)
            return;

        var lastBackup = await db.BackupHistories
            .AsNoTracking()
            .Where(b => b.IsSuccessful && b.BackupType == Domain.Enums.BackupType.Automatic)
            .OrderByDescending(b => b.BackupDate)
            .FirstOrDefaultAsync(ct);

        var dueAt = lastBackup?.BackupDate.AddHours(settings.AutoBackupIntervalHours) ?? DateTime.MinValue;
        if (DateTime.UtcNow < dueAt)
            return;

        _logger.LogInformation("Starting scheduled database backup");
        var result = await backupService.CreateBackupAsync(isAutomatic: true, ct);
        if (!result.Succeeded)
            _logger.LogWarning("Scheduled backup failed: {Error}", result.Error);
    }
}
