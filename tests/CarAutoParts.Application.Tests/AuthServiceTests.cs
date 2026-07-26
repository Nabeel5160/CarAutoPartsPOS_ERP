using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsLoginResult()
    {
        await using var db = TestDbContextFactory.Create();
        var password = "admin123";
        var user = new AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = "Administrator",
            Email = "admin@test.local",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var users = new Repository<AppUser>(db);
        var currentUser = new CurrentUserService();
        var unitOfWork = new UnitOfWork(db);
        var auth = new AuthService(users, currentUser, unitOfWork);

        var result = await auth.LoginAsync("admin", password);

        result.Should().NotBeNull();
        result!.Username.Should().Be("admin");
        result.MustChangePassword.Should().BeFalse();
        currentUser.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_ClearsMustChangePassword()
    {
        await using var db = TestDbContextFactory.Create();
        var user = new AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            DisplayName = "Administrator",
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var auth = new AuthService(
            new Repository<AppUser>(db),
            new CurrentUserService(),
            new UnitOfWork(db));

        var result = await auth.ChangePasswordAsync(user.Id, "admin123", "NewPass1!");
        result.Succeeded.Should().BeTrue();
        (await db.Users.SingleAsync()).MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsNull()
    {
        await using var db = TestDbContextFactory.Create();
        db.Users.Add(new AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            DisplayName = "Administrator",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var auth = new AuthService(
            new Repository<AppUser>(db),
            new CurrentUserService(),
            new UnitOfWork(db));

        var result = await auth.LoginAsync("admin", "wrong");

        result.Should().BeNull();
    }
}
