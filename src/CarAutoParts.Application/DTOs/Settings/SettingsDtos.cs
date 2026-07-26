namespace CarAutoParts.Application.DTOs.Settings;

/// <summary>Company and application settings.</summary>
public record CompanySettingsDto(
    int Id,
    string CompanyName,
    string? LogoPath,
    string? Address,
    string? City,
    string? Phone,
    string? Email,
    string? Ntn,
    string? Strn,
    string? PosId,
    decimal DefaultTaxRate,
    string? InvoicePrefix,
    string? InvoiceFooter,
    string? PrinterName,
    string Theme,
    bool AutoBackupEnabled,
    int AutoBackupIntervalHours,
    bool FbrUseSandbox,
    int FbrTimeoutSeconds,
    decimal GrnOverReceivePercent = 0,
    bool GrnUnderReceiveAllowed = true,
    decimal ThreeWayQtyTolerancePercent = 0,
    decimal ThreeWayPriceTolerancePercent = 0);

/// <summary>Database backup history entry.</summary>
public record BackupHistoryDto(
    int Id,
    string FilePath,
    long FileSizeBytes,
    string BackupType,
    bool IsSuccessful,
    string? ErrorMessage,
    DateTime BackupDate);
