using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Infrastructure.Data.Seed;

public class DataSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(ApplicationDbContext db, ILogger<DataSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);
        await EnsurePermissionsAsync(ct);
        await EnforceDefaultAdminPasswordChangeAsync(ct);
        await ClearDemoUserForcePasswordAsync(ct);
        await EnsureDefaultUserBranchesAsync(ct);

        if (await _db.Users.AnyAsync(ct))
        {
            _logger.LogInformation("Database already seeded.");
            return;
        }

        _logger.LogInformation("Seeding database...");

        var roles = await SeedRolesAsync(ct);
        await SeedAdminUserAsync(roles["Admin"], ct);
        await SeedCompanySettingsAsync(ct);
        await SeedBrandsAsync(ct);
        await SeedWarehouseAsync(ct);
        await SeedCategoriesAsync(ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Database seeding completed.");
    }

    private async Task EnforceDefaultAdminPasswordChangeAsync(CancellationToken ct)
    {
        var admin = await _db.Users.FirstOrDefaultAsync(u => u.Username == "admin" && !u.IsDeleted, ct);
        if (admin is null || admin.MustChangePassword)
            return;

        // Force change while still on the well-known demo password.
        if (BCrypt.Net.BCrypt.Verify("admin123", admin.PasswordHash))
        {
            admin.MustChangePassword = true;
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Admin still uses default password; MustChangePassword enforced.");
        }
    }

    /// <summary>
    /// Demo counter users must not inherit force-password friction (admin-only on admin123).
    /// Uses List for EF Contains (avoid array → ReadOnlySpan binding).
    /// </summary>
    private async Task ClearDemoUserForcePasswordAsync(CancellationToken ct)
    {
        var demoUsernames = new List<string> { "cashier", "sales", "manager", "inventory", "accountant" };
        var users = await _db.Users
            .Where(u => demoUsernames.Contains(u.Username) && !u.IsDeleted && u.MustChangePassword)
            .ToListAsync(ct);
        if (users.Count == 0)
            return;

        foreach (var u in users)
            u.MustChangePassword = false;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Cleared MustChangePassword on {Count} demo non-admin users.", users.Count);
    }

    private async Task EnsurePermissionsAsync(CancellationToken ct)
    {
        var existing = await _db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = false;
        foreach (var (code, name, module) in PermissionDefinitions.All)
        {
            if (existingSet.Contains(code)) continue;
            _db.Permissions.Add(new Permission
            {
                Code = code,
                Name = name,
                Module = module,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            });
            added = true;
        }

        if (added)
            await _db.SaveChangesAsync(ct);

        await SyncRoleTemplatesAsync(ct);
    }

    /// <summary>Ensure all role templates exist and have the latest permission set.</summary>
    private async Task SyncRoleTemplatesAsync(CancellationToken ct)
    {
        var permissions = await _db.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.OrdinalIgnoreCase, ct);
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = "Full system access",
            ["Manager"] = "Store manager access",
            ["SalesUser"] = "POS and sales access",
            ["InventoryUser"] = "Inventory and purchasing access",
            ["Cashier"] = "Counter cashier (POS, no price override)",
            ["Accountant"] = "Finance and reports (no POS checkout)"
        };

        foreach (var (roleName, codes) in PermissionDefinitions.RoleTemplates)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
            if (role is null)
            {
                role = new Role
                {
                    Name = roleName,
                    Description = descriptions.GetValueOrDefault(roleName, roleName),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                };
                _db.Roles.Add(role);
                await _db.SaveChangesAsync(ct);
            }

            var current = await _db.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.Permission.Code)
                .ToListAsync(ct);
            var currentSet = current.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var changed = false;
            foreach (var code in codes)
            {
                if (currentSet.Contains(code)) continue;
                if (!permissions.TryGetValue(code, out var permission)) continue;
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                });
                changed = true;
            }
            if (changed)
                await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<Dictionary<string, Role>> SeedRolesAsync(CancellationToken ct)
    {
        await SyncRoleTemplatesAsync(ct);
        return await _db.Roles.ToDictionaryAsync(r => r.Name, ct);
    }

    private async Task SeedAdminUserAsync(Role adminRole, CancellationToken ct)
    {
        var admin = new AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            DisplayName = "System Administrator",
            Email = "admin@carautoparts.local",
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
        _db.Users.Add(admin);
        _db.UserRoles.Add(new UserRole
        {
            User = admin,
            Role = adminRole,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedBrandsAsync(CancellationToken ct)
    {
        var vertical = await ResolveVerticalKeyAsync(ct);
        var pack = VerticalSeedPacks.For(vertical);
        foreach (var name in pack.Brands)
        {
            _db.Brands.Add(new Brand
            {
                Name = name,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedWarehouseAsync(CancellationToken ct)
    {
        _db.Warehouses.Add(new Warehouse
        {
            Name = "Main Warehouse",
            Address = "Industrial Area, Phase 2",
            City = "Lahore",
            ContactPerson = "Warehouse Manager",
            PhoneNumber = "+92-300-0000000",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedCompanySettingsAsync(CancellationToken ct)
    {
        var vertical = Environment.GetEnvironmentVariable("CAP_VERTICAL") ?? "auto-parts";
        _db.CompanySettings.Add(new CompanySettings
        {
            CompanyName = VerticalSeedPacks.DefaultCompanyName(vertical),
            VerticalKey = vertical,
            Address = "Main Boulevard, Gulberg III",
            City = "Lahore",
            Phone = "+92-42-0000000",
            Email = "info@local",
            Ntn = "0000000-0",
            Strn = "0000000000000",
            PosId = "POS-001",
            DefaultTaxRate = 18m,
            InvoicePrefix = "INV",
            InvoiceFooter = "Thank you for your business!",
            Theme = "Light",
            AutoBackupEnabled = true,
            AutoBackupIntervalHours = 24,
            FbrUseSandbox = true,
            FbrTimeoutSeconds = 60,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        var vertical = await ResolveVerticalKeyAsync(ct);
        var pack = VerticalSeedPacks.For(vertical);

        foreach (var (name, description) in pack.Categories)
        {
            _db.Categories.Add(new Category
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> ResolveVerticalKeyAsync(CancellationToken ct)
    {
        var settings = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        return settings?.VerticalKey ?? Environment.GetEnvironmentVariable("CAP_VERTICAL") ?? "auto-parts";
    }

    /// <summary>
    /// Assigns the company default branch to non-admin users with no ACL rows.
    /// Admin / platform users keep empty ACL (JWT grants all branches).
    /// </summary>
    private async Task EnsureDefaultUserBranchesAsync(CancellationToken ct)
    {
        var defaultBranchId = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive && !b.IsDeleted && b.IsDefault)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync(ct)
            ?? await _db.Branches.AsNoTracking()
                .Where(b => b.IsActive && !b.IsDeleted)
                .OrderBy(b => b.Id)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct);

        if (defaultBranchId is null)
            return;

        var usersNeedingAcl = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.IsActive)
            .Where(u => !u.UserRoles.Any(ur => ur.Role.Name == "Admin"))
            .Where(u => !u.UserBranches.Any(ub => !ub.IsDeleted))
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (usersNeedingAcl.Count == 0)
            return;

        foreach (var userId in usersNeedingAcl)
        {
            _db.UserBranches.Add(new UserBranch
            {
                UserId = userId,
                BranchId = defaultBranchId.Value,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Assigned default branch ACL to {Count} users.", usersNeedingAcl.Count);
    }
}
