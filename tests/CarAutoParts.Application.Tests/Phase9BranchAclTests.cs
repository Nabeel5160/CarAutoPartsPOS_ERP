using AutoMapper;
using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Application.DTOs.Transfers;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Mapping;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class Phase9BranchAclTests
{
    [Fact]
    public async Task Transfer_Create_Denied_When_Source_Branch_Not_Allowed()
    {
        await using var db = TestDbContextFactory.Create();
        db.Warehouses.AddRange(
            new Warehouse { Id = 1, Name = "A", BranchId = 1, CompanyId = 1, CreatedAt = DateTime.UtcNow },
            new Warehouse { Id = 2, Name = "B", BranchId = 2, CompanyId = 1, CreatedAt = DateTime.UtcNow });
        db.Categories.Add(new Category { Id = 1, Name = "C", CreatedAt = DateTime.UtcNow });
        db.Products.Add(new Product
        {
            Id = 1, Name = "P", Sku = "P1", CategoryId = 1, Unit = "PCS",
            PurchasePrice = 1, CostPrice = 1, SalePrice = 2, IsActive = true, CompanyId = 1, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, 2, [2]); // only branch 2

        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();
        var svc = new TransferService(
            new Repository<InventoryTransfer>(db),
            new Repository<Warehouse>(db),
            new Repository<Product>(db),
            Mock.Of<IInventoryService>(),
            new CurrentUserService(),
            company,
            Mock.Of<IGlPostingService>(),
            new UnitOfWork(db),
            mapper,
            Mock.Of<IApprovalWorkflowService>(),
            Mock.Of<IMoneyAuditService>());

        var result = await svc.CreateAsync(new TransferCreateDto(1, 2, null, [new TransferLineDto(1, null, 1)]));
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public async Task UserService_Persists_Branch_Acl()
    {
        await using var db = TestDbContextFactory.Create();
        db.Branches.Add(new Branch
        {
            Id = 1, CompanyId = 1, Code = "HO", Name = "HO", IsActive = true, IsDefault = true, CreatedAt = DateTime.UtcNow
        });
        db.Roles.Add(new Role { Id = 1, Name = "Manager", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();
        var validator = new Mock<IValidator<UserCreateDto>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<UserCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        var svc = new UserService(
            new Repository<AppUser>(db),
            new Repository<Role>(db),
            new Repository<UserRole>(db),
            new Repository<UserBranch>(db),
            new Repository<Branch>(db),
            new UnitOfWork(db),
            mapper,
            validator.Object);

        var created = await svc.CreateAsync(new UserCreateDto(
            "clerk", "Clerk123!", "Clerk", null, true, [1], [1], 1));
        created.Succeeded.Should().BeTrue();
        created.Data!.BranchIds.Should().ContainSingle().Which.Should().Be(1);
        created.Data.DefaultBranchId.Should().Be(1);

        (await db.UserBranches.CountAsync(ub => ub.UserId == created.Data.Id && ub.BranchId == 1 && ub.IsDefault))
            .Should().Be(1);
    }

    [Fact]
    public async Task TrialBalance_Filters_By_CostCenter_Branch_And_Denies_Disallowed()
    {
        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options, company);

        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "T", CurrencyCode = "PKR", IsActive = true });
        db.Branches.AddRange(
            new Branch { Id = 1, CompanyId = 1, Code = "A", Name = "A", IsActive = true, IsDefault = true },
            new Branch { Id = 2, CompanyId = 1, Code = "B", Name = "B", IsActive = true });
        db.CostCenters.AddRange(
            new CostCenter { Id = 1, CompanyId = 1, BranchId = 1, Code = "CC1", Name = "CC1", IsActive = true },
            new CostCenter { Id = 2, CompanyId = 1, BranchId = 2, Code = "CC2", Name = "CC2", IsActive = true });
        db.GlAccounts.Add(new GlAccount
        {
            Id = 1, CompanyId = 1, Code = "1100", Name = "Cash", AccountType = AccountType.Asset,
            IsPostable = true, IsActive = true
        });

        var je = new JournalEntry
        {
            Id = 1,
            CompanyId = 1,
            JournalNumber = "JV-1",
            JournalDate = new DateTime(2026, 7, 1),
            Status = JournalStatus.Posted,
            SourceDocumentType = "Manual"
        };
        je.Lines.Add(new JournalLine
        {
            CompanyId = 1, AccountId = 1, CostCenterId = 1, Debit = 40, Credit = 0
        });
        je.Lines.Add(new JournalLine
        {
            CompanyId = 1, AccountId = 1, CostCenterId = 2, Debit = 60, Credit = 0
        });
        db.JournalEntries.Add(je);
        await db.SaveChangesAsync();

        var reports = new FinancialReportService(new EnterpriseDbAdapter(db), company);

        var denied = await reports.TrialBalanceAsync(new DateTime(2026, 7, 31), branchId: 2);
        denied.Succeeded.Should().BeFalse();

        var filtered = await reports.TrialBalanceAsync(new DateTime(2026, 7, 31), branchId: 1);
        filtered.Succeeded.Should().BeTrue();
        filtered.Data!.TotalDebit.Should().Be(40m);

        var all = await reports.TrialBalanceAsync(new DateTime(2026, 7, 31));
        all.Succeeded.Should().BeTrue();
        all.Data!.TotalDebit.Should().Be(100m);
    }

    [Fact]
    public void CurrentCompanyContext_IsBranchAllowed_Respects_Acl()
    {
        var ctx = new CurrentCompanyContext();
        ctx.Set(1, 1, [1, 3]);
        ctx.IsBranchAllowed(1).Should().BeTrue();
        ctx.IsBranchAllowed(2).Should().BeFalse();
        ctx.IsBranchAllowed(3).Should().BeTrue();
    }
}
