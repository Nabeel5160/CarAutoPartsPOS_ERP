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

        if (await _db.Users.AnyAsync(ct))
        {
            _logger.LogInformation("Database already seeded.");
            return;
        }

        _logger.LogInformation("Seeding database...");

        var roles = await SeedRolesAsync(ct);
        await SeedAdminUserAsync(roles["Admin"], ct);
        await SeedBrandsAsync(ct);
        await SeedWarehouseAsync(ct);
        await SeedCompanySettingsAsync(ct);
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
        {
            await _db.SaveChangesAsync(ct);
            // Grant new permissions to Admin role when present
            var admin = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", ct);
            if (admin is not null)
            {
                var adminCodes = await _db.RolePermissions
                    .Where(rp => rp.RoleId == admin.Id)
                    .Select(rp => rp.Permission.Code)
                    .ToListAsync(ct);
                var adminSet = adminCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var permissions = await _db.Permissions.ToDictionaryAsync(p => p.Code, ct);
                foreach (var code in PermissionDefinitions.Admin)
                {
                    if (adminSet.Contains(code)) continue;
                    if (!permissions.TryGetValue(code, out var permission)) continue;
                    _db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = admin.Id,
                        PermissionId = permission.Id,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "system"
                    });
                }
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task<Dictionary<string, Role>> SeedRolesAsync(CancellationToken ct)
    {
        var roleDefs = new Dictionary<string, (string Description, string[] Permissions)>
        {
            ["Admin"] = ("Full system access", PermissionDefinitions.Admin),
            ["Manager"] = ("Store manager access", PermissionDefinitions.Manager),
            ["SalesUser"] = ("POS and sales access", PermissionDefinitions.SalesUser),
            ["InventoryUser"] = ("Inventory and purchasing access", PermissionDefinitions.InventoryUser)
        };

        var permissions = await _db.Permissions.ToDictionaryAsync(p => p.Code, ct);
        var roles = new Dictionary<string, Role>();

        foreach (var (name, (description, codes)) in roleDefs)
        {
            var role = new Role
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };
            _db.Roles.Add(role);

            foreach (var code in codes)
            {
                if (permissions.TryGetValue(code, out var permission))
                {
                    _db.RolePermissions.Add(new RolePermission
                    {
                        Role = role,
                        Permission = permission,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "system"
                    });
                }
            }

            roles[name] = role;
        }

        await _db.SaveChangesAsync(ct);
        return roles;
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
        var brands = new[] { "Toyota", "Honda", "Suzuki", "Hyundai", "Kia", "Nissan", "BMW", "Audi" };
        foreach (var name in brands)
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
        _db.CompanySettings.Add(new CompanySettings
        {
            CompanyName = "Car Auto Parts",
            Address = "Main Boulevard, Gulberg III",
            City = "Lahore",
            Phone = "+92-42-0000000",
            Email = "info@carautoparts.local",
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
        var categories = new[]
        {
            ("Engine Parts", "Engine components and assemblies"),
            ("Brake System", "Brake pads, discs, and fluids"),
            ("Electrical", "Batteries, alternators, and wiring"),
            ("Suspension", "Shocks, struts, and bushings"),
            ("Filters", "Oil, air, and fuel filters"),
            ("Body Parts", "Panels, bumpers, and mirrors"),
            ("Transmission", "Gears, clutches, and fluids"),
            ("Cooling System", "Radiators, hoses, and thermostats")
        };

        foreach (var (name, description) in categories)
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
}
