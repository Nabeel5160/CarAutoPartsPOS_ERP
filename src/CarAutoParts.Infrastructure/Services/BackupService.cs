using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupService> _logger;

    public BackupService(ApplicationDbContext db, IConfiguration configuration, ILogger<BackupService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<string>> CreateBackupAsync(bool isAutomatic, CancellationToken ct = default)
    {
        var connectionString = GetConnectionString();
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
            return Result<string>.Failure("Database name not found in connection string.");

        var backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CarAutoParts",
            "Backups");
        Directory.CreateDirectory(backupDir);

        var fileName = $"{databaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        var filePath = Path.Combine(backupDir, fileName);

        var history = new Domain.Entities.BackupHistory
        {
            FilePath = filePath,
            BackupType = isAutomatic ? BackupType.Automatic : BackupType.Manual,
            BackupDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            var sql = $"BACKUP DATABASE [{databaseName}] TO DISK = @path WITH FORMAT, INIT, NAME = @name, SKIP, NOREWIND, NOUNLOAD, STATS = 10";
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@path", filePath);
            command.Parameters.AddWithValue("@name", $"CarAutoParts-{databaseName}-Backup");
            command.CommandTimeout = 300;
            await command.ExecuteNonQueryAsync(ct);

            var fileInfo = new FileInfo(filePath);
            history.FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;
            history.IsSuccessful = true;

            _db.BackupHistories.Add(history);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Database backup created at {Path}", filePath);
            return Result<string>.Success(filePath);
        }
        catch (Exception ex)
        {
            history.IsSuccessful = false;
            history.ErrorMessage = ex.Message;
            _db.BackupHistories.Add(history);
            await _db.SaveChangesAsync(ct);

            _logger.LogError(ex, "Database backup failed");
            return Result<string>.Failure($"Backup failed: {ex.Message}");
        }
    }

    public async Task<Result> RestoreBackupAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return Result.Failure("Backup file not found.");

        var connectionString = GetConnectionString();
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
            return Result.Failure("Database name not found in connection string.");

        try
        {
            var masterConnection = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;
            await using var connection = new SqlConnection(masterConnection);
            await connection.OpenAsync(ct);

            var sql = $@"
ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [{databaseName}] FROM DISK = @path WITH REPLACE;
ALTER DATABASE [{databaseName}] SET MULTI_USER;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@path", filePath);
            command.CommandTimeout = 600;
            await command.ExecuteNonQueryAsync(ct);

            _logger.LogInformation("Database restored from {Path}", filePath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database restore failed");
            return Result.Failure($"Restore failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<Application.DTOs.Settings.BackupHistoryDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        return await _db.BackupHistories
            .AsNoTracking()
            .OrderByDescending(b => b.BackupDate)
            .Select(b => new Application.DTOs.Settings.BackupHistoryDto(
                b.Id,
                b.FilePath,
                b.FileSizeBytes,
                b.BackupType.ToString(),
                b.IsSuccessful,
                b.ErrorMessage,
                b.BackupDate))
            .ToListAsync(ct);
    }

    private string GetConnectionString()
        => _configuration.GetConnectionString("DefaultConnection")
           ?? "Server=(localdb)\\MSSQLLocalDB;Database=CarAutoPartsDb;Trusted_Connection=True;TrustServerCertificate=True";
}
