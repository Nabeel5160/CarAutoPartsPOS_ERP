using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.Username).HasMaxLength(50).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(100);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasIndex(r => r.Name).IsUnique();
        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(200);
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Module).HasMaxLength(50);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();

        builder.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
        builder.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
    }
}

public class UserBranchConfiguration : IEntityTypeConfiguration<UserBranch>
{
    public void Configure(EntityTypeBuilder<UserBranch> builder)
    {
        builder.ToTable("UserBranches");
        builder.HasIndex(ub => new { ub.UserId, ub.BranchId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(ub => ub.User).WithMany(u => u.UserBranches).HasForeignKey(ub => ub.UserId);
        builder.HasOne(ub => ub.Branch).WithMany().HasForeignKey(ub => ub.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();

        builder.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
        builder.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.UserName).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(50);
    }
}

public class AppNotificationConfiguration : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> builder)
    {
        builder.ToTable("Notifications");
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.RelatedEntityType).HasMaxLength(100);
    }
}

public class CompanySettingsConfiguration : IEntityTypeConfiguration<CompanySettings>
{
    public void Configure(EntityTypeBuilder<CompanySettings> builder)
    {
        builder.ToTable("CompanySettings");
        builder.Property(s => s.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.LogoPath).HasMaxLength(500);
        builder.Property(s => s.LogoUrl).HasMaxLength(500);
        builder.Property(s => s.VerticalKey).HasMaxLength(40).IsRequired();
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Email).HasMaxLength(100);
        builder.Property(s => s.Ntn).HasMaxLength(20);
        builder.Property(s => s.Strn).HasMaxLength(20);
        builder.Property(s => s.PosId).HasMaxLength(20);
        builder.Property(s => s.DefaultTaxRate).HasPrecision(5, 2);
        builder.Property(s => s.InvoicePrefix).HasMaxLength(10);
        builder.Property(s => s.PrinterName).HasMaxLength(100);
        builder.Property(s => s.Theme).HasMaxLength(20);
        builder.Property(s => s.GrnOverReceivePercent).HasPrecision(9, 4);
        builder.Property(s => s.ThreeWayQtyTolerancePercent).HasPrecision(9, 4);
        builder.Property(s => s.ThreeWayPriceTolerancePercent).HasPrecision(9, 4);
    }
}

public class AppConfigEntryConfiguration : IEntityTypeConfiguration<AppConfigEntry>
{
    public void Configure(EntityTypeBuilder<AppConfigEntry> builder)
    {
        builder.ToTable("AppConfigEntries");
        builder.Property(e => e.Scope).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Key).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Culture).HasMaxLength(10);
        builder.Property(e => e.Value).HasMaxLength(2000).IsRequired();
        builder.HasIndex(e => new { e.Scope, e.Key, e.Culture })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class BackupHistoryConfiguration : IEntityTypeConfiguration<BackupHistory>
{
    public void Configure(EntityTypeBuilder<BackupHistory> builder)
    {
        builder.ToTable("BackupHistories");
        builder.Property(b => b.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(b => b.ErrorMessage).HasMaxLength(2000);
    }
}
