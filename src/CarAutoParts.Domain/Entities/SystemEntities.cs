using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

public class InventoryTransfer : BaseEntity
{
    public string TransferNumber { get; set; } = string.Empty;
    public int FromWarehouseId { get; set; }
    public Warehouse FromWarehouse { get; set; } = null!;
    public int ToWarehouseId { get; set; }
    public Warehouse ToWarehouse { get; set; } = null!;
    public TransferStatus Status { get; set; } = TransferStatus.Draft;
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public ICollection<InventoryTransferLine> Lines { get; set; } = new List<InventoryTransferLine>();
}

public class InventoryTransferLine : BaseEntity
{
    public int InventoryTransferId { get; set; }
    public InventoryTransfer InventoryTransfer { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    /// <summary>Unit cost captured at ship (average/FIFO issue cost).</summary>
    public decimal ShippedUnitCost { get; set; }
    public int? FromLocationId { get; set; }
    public WarehouseLocation? FromLocation { get; set; }
    public int? ToLocationId { get; set; }
    public WarehouseLocation? ToLocation { get; set; }
    public bool IsPicked { get; set; }
}

public class AppUser : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    /// <summary>When true, client must change password before using the app.</summary>
    public bool MustChangePassword { get; set; }
    /// <summary>TOTP enrolled and required at login.</summary>
    public bool MfaEnabled { get; set; }
    /// <summary>Company policy: privileged user must enroll MFA.</summary>
    public bool MfaEnforced { get; set; }
    /// <summary>Base32 TOTP secret (protect at rest in production).</summary>
    public string? MfaSecret { get; set; }
    /// <summary>JSON array of BCrypt-hashed one-time backup codes.</summary>
    public string? MfaBackupCodesHashJson { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserBranch> UserBranches { get; set; } = new List<UserBranch>();
}

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Module { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class UserRole : BaseEntity
{
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

/// <summary>User ↔ branch ACL (Phase 9).</summary>
public class UserBranch : BaseEntity
{
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public bool IsDefault { get; set; }
}

public class RolePermission : BaseEntity
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

public class AuditLog : BaseEntity
{
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? UserName { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}

public class AppNotification : BaseEntity
{
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
}

public class CompanySettings : BaseEntity
{
    public string CompanyName { get; set; } = "Car Auto Parts";
    public string? LogoPath { get; set; }
    /// <summary>Public URL or path for logo used in branding (preferred over LogoPath when set).</summary>
    public string? LogoUrl { get; set; }
    /// <summary>Business vertical preset key: auto-parts | bike-parts | general-retail.</summary>
    public string VerticalKey { get; set; } = "auto-parts";
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Ntn { get; set; }
    public string? Strn { get; set; }
    public string? PosId { get; set; }
    public decimal DefaultTaxRate { get; set; } = 18m;
    public string? InvoicePrefix { get; set; } = "INV";
    public string? InvoiceFooter { get; set; }
    public string? PrinterName { get; set; }
    public string? DatabaseConnectionString { get; set; }
    public string Theme { get; set; } = "Light";
    public bool AutoBackupEnabled { get; set; }
    public int AutoBackupIntervalHours { get; set; } = 24;
    public string? FbrBearerToken { get; set; }
    public bool FbrUseSandbox { get; set; } = true;
    public int FbrTimeoutSeconds { get; set; } = 60;
    /// <summary>Allowed over-receive vs PO ordered qty as percent (0 = block any over).</summary>
    public decimal GrnOverReceivePercent { get; set; }
    public bool GrnUnderReceiveAllowed { get; set; } = true;
    public decimal ThreeWayQtyTolerancePercent { get; set; }
    public decimal ThreeWayPriceTolerancePercent { get; set; }
    /// <summary>When false (default), stock deductions fail if quantity would go below zero.</summary>
    public bool AllowNegativeStock { get; set; }
    /// <summary>Default costing for newly created inventory items.</summary>
    public ValuationMethod DefaultValuationMethod { get; set; } = ValuationMethod.Average;
    /// <summary>Go-live cutover date; GL posts before this date are blocked except OpeningBalance docs.</summary>
    public DateTime? OpeningBalanceDate { get; set; }
    /// <summary>When set, first-run onboarding wizard is complete.</summary>
    public DateTime? SetupCompletedAt { get; set; }
}

/// <summary>Per-install config override (module/field/behavior/label/brand) layered over vertical presets.</summary>
public class AppConfigEntry : BaseEntity
{
    /// <summary>Family: module | field | behavior | label | brand.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>Key within scope, e.g. sales.fbr, product.oem, fbr.enabled, Nav_POS, appName.</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Culture for label overrides (en, ur); null for non-label scopes.</summary>
    public string? Culture { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class BackupHistory : BaseEntity
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public BackupType BackupType { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime BackupDate { get; set; } = DateTime.UtcNow;
}
