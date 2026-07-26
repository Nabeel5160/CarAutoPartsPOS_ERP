using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using FluentAssertions;

namespace CarAutoParts.Application.Tests;

public class AuthorizationServiceTests
{
    [Fact]
    public async Task EnsurePermissionAsync_WhenUserHasPermission_DoesNotThrow()
    {
        var currentUser = new CurrentUserService();
        currentUser.SetUser(new AppUser { Username = "admin", DisplayName = "Admin" },
            [Permissions.ProductsView]);

        var authz = new AuthorizationService(currentUser);

        var act = () => authz.EnsurePermissionAsync(Permissions.ProductsView);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsurePermissionAsync_WhenUserLacksPermission_ThrowsUnauthorized()
    {
        var currentUser = new CurrentUserService();
        currentUser.SetUser(new AppUser { Username = "user", DisplayName = "User" }, []);

        var authz = new AuthorizationService(currentUser);

        var act = () => authz.EnsurePermissionAsync(Permissions.UsersView);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public void HasPermission_IsCaseInsensitive()
    {
        var currentUser = new CurrentUserService();
        currentUser.SetUser(new AppUser { Username = "admin", DisplayName = "Admin" },
            ["products.view"]);

        var authz = new AuthorizationService(currentUser);

        authz.HasPermission(Permissions.ProductsView).Should().BeTrue();
    }
}
