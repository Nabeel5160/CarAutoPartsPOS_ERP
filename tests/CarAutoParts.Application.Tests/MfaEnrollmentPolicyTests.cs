using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Security;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;

namespace CarAutoParts.Application.Tests;

public class MfaEnrollmentPolicyTests
{
    [Fact]
    public void Cashier_with_pos_checkout_is_not_forced_to_enroll()
    {
        string[] cashier =
        [
            Permissions.PosCheckout,
            Permissions.PosHold,
            Permissions.PosShift,
            Permissions.SalesView
        ];

        MfaEnrollmentPolicy.IsCounterCashierPath(cashier).Should().BeTrue();
        MfaEnrollmentPolicy.MustEnroll(false, false, cashier).Should().BeFalse();
        MfaEnrollmentPolicy.PostLoginHome(cashier).Should().Be("/pos");
    }

    [Fact]
    public void SalesUser_with_checkout_without_finance_manage_goes_to_pos()
    {
        string[] sales =
        [
            Permissions.PosCheckout,
            Permissions.PosPriceOverride,
            Permissions.SalesView
        ];

        MfaEnrollmentPolicy.MustEnroll(false, false, sales).Should().BeFalse();
        MfaEnrollmentPolicy.PostLoginHome(sales).Should().Be("/pos");
    }

    [Fact]
    public void Admin_finance_manage_must_enroll_when_mfa_off()
    {
        string[] admin = [Permissions.UsersManage, Permissions.FinanceManage, Permissions.PosCheckout];
        MfaEnrollmentPolicy.IsCounterCashierPath(admin).Should().BeFalse();
        MfaEnrollmentPolicy.MustEnroll(false, false, admin).Should().BeTrue();
        MfaEnrollmentPolicy.PostLoginHome(admin).Should().Be("/");
    }

    [Fact]
    public void MfaEnforced_forces_even_for_cashier()
    {
        string[] cashier = [Permissions.PosCheckout];
        MfaEnrollmentPolicy.MustEnroll(false, mfaEnforced: true, cashier).Should().BeTrue();
    }

    [Fact]
    public void Already_enabled_mfa_never_must_enroll()
    {
        MfaEnrollmentPolicy.MustEnroll(true, true, [Permissions.FinanceManage]).Should().BeFalse();
    }
}

public class AuthServiceMfaRoutingTests
{
    [Fact]
    public async Task Login_cashier_MustEnrollMfa_false()
    {
        await using var db = TestDbContextFactory.Create();
        var perm = new Permission
        {
            Code = Permissions.PosCheckout,
            Name = "POS",
            Module = "POS",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        var role = new Role
        {
            Name = "Cashier",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
            RolePermissions = [new RolePermission { Permission = perm, CreatedAt = DateTime.UtcNow, CreatedBy = "test" }]
        };
        var user = new AppUser
        {
            Username = "cashier",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("cashier123"),
            DisplayName = "Counter",
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
            UserRoles = [new UserRole { Role = role, CreatedAt = DateTime.UtcNow, CreatedBy = "test" }]
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var auth = new AuthService(
            new Repository<AppUser>(db),
            new CurrentUserService(),
            new UnitOfWork(db));

        var result = await auth.LoginAsync("cashier", "cashier123");
        result.Should().NotBeNull();
        result!.MustEnrollMfa.Should().BeFalse();
        result.MustChangePassword.Should().BeFalse();
        result.Permissions.Should().Contain(Permissions.PosCheckout);
    }

    [Fact]
    public async Task Login_admin_with_finance_MustEnrollMfa_true()
    {
        await using var db = TestDbContextFactory.Create();
        var perm = new Permission
        {
            Code = Permissions.FinanceManage,
            Name = "Finance",
            Module = "Finance",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        var role = new Role
        {
            Name = "Admin",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
            RolePermissions = [new RolePermission { Permission = perm, CreatedAt = DateTime.UtcNow, CreatedBy = "test" }]
        };
        var user = new AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            DisplayName = "Admin",
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
            UserRoles = [new UserRole { Role = role, CreatedAt = DateTime.UtcNow, CreatedBy = "test" }]
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var auth = new AuthService(
            new Repository<AppUser>(db),
            new CurrentUserService(),
            new UnitOfWork(db));

        var result = await auth.LoginAsync("admin", "admin123");
        result.Should().NotBeNull();
        result!.MustEnrollMfa.Should().BeTrue();
        result.MustChangePassword.Should().BeTrue();
    }
}
