using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Infrastructure.Tests;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}

public class ProductRepositoryTests
{
    [Fact]
    public async Task Add_And_Query_ReturnsProduct()
    {
        await using var db = TestDbContextFactory.Create();
        var repo = new Repository<Product>(db);

        var product = new Product
        {
            Name = "Air Filter",
            Sku = "AF-100",
            CategoryId = 1,
            BrandId = 1,
            Unit = "PCS",
            PurchasePrice = 40,
            SalePrice = 65,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };

        repo.Add(product);
        await db.SaveChangesAsync();

        var loaded = await repo.Query().FirstOrDefaultAsync(p => p.Sku == "AF-100");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Air Filter");
    }

    [Fact]
    public async Task SoftDeletedProducts_AreFilteredByDefaultQuery()
    {
        await using var db = TestDbContextFactory.Create();
        var repo = new Repository<Product>(db);

        repo.Add(new Product
        {
            Name = "Deleted Part",
            Sku = "DEL-1",
            CategoryId = 1,
            BrandId = 1,
            Unit = "PCS",
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var count = await repo.Query().CountAsync();

        count.Should().Be(0);
    }
}
