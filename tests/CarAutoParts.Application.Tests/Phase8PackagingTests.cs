using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Data.Seed;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class Phase8PackagingTests
{
    [Fact]
    public async Task Onboarding_complete_sets_SetupCompletedAt()
    {
        await using var db = TestDbContextFactory.Create();
        db.CompanySettings.Add(new CompanySettings { CompanyName = "Old", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = new CurrentUserService();
        user.SetUser(new AppUser { Id = 1, Username = "admin", DisplayName = "Admin" },
            [Application.Constants.Permissions.SettingsManage]);

        var company = new CurrentCompanyContext();
        var svc = new OnboardingService(
            new Repository<CompanySettings>(db),
            new Repository<Warehouse>(db),
            new Repository<FiscalYear>(db),
            new Repository<AccountMapping>(db),
            new Repository<AppUser>(db),
            new Repository<Till>(db),
            new Repository<Branch>(db),
            new UnitOfWork(db),
            user,
            PosCheckoutServiceTests.CreateFeatureGate(),
            CreateAppConfig(db),
            company);

        var statusBefore = await svc.GetStatusAsync();
        statusBefore.IsComplete.Should().BeFalse();

        var result = await svc.CompleteAsync(new CompleteOnboardingDto(
            "Demo Mart", "0786909", null, "Karachi", "Shop 1", null, null, "1001", 18m, true, "Fifo", "auto-parts"));
        result.Succeeded.Should().BeTrue();

        var settings = await db.CompanySettings.SingleAsync();
        settings.SetupCompletedAt.Should().NotBeNull();
        settings.CompanyName.Should().Be("Demo Mart");
        settings.DefaultValuationMethod.Should().Be(Domain.Enums.ValuationMethod.Fifo);
        settings.VerticalKey.Should().Be("auto-parts");

        (await svc.GetStatusAsync()).IsComplete.Should().BeTrue();
    }

    [Fact]
    public void RoleTemplates_include_Cashier_and_Accountant()
    {
        PermissionDefinitions.RoleTemplates.Should().ContainKey("Cashier");
        PermissionDefinitions.RoleTemplates.Should().ContainKey("Accountant");
        PermissionDefinitions.Cashier.Should().NotContain(Application.Constants.Permissions.PosPriceOverride);
        PermissionDefinitions.Cashier.Should().Contain(Application.Constants.Permissions.PosCheckout);
        PermissionDefinitions.Accountant.Should().Contain(Application.Constants.Permissions.FinanceView);
        PermissionDefinitions.Accountant.Should().NotContain(Application.Constants.Permissions.PosCheckout);
    }

    private static IAppConfigService CreateAppConfig(ApplicationDbContext db)
    {
        var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        return new AppConfigService(
            new Repository<AppConfigEntry>(db),
            new Repository<CompanySettings>(db),
            new UnitOfWork(db),
            cache);
    }
}
