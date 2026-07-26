using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;

namespace CarAutoParts.Application.Services;

/// <summary>Scoped holder for the authenticated user and permissions.</summary>
public class CurrentUserService : ICurrentUserService
{
    private AppUser? _currentUser;
    private IReadOnlyList<string> _permissions = Array.Empty<string>();

    /// <inheritdoc />
    public AppUser? CurrentUser => _currentUser;

    /// <inheritdoc />
    public IReadOnlyList<string> Permissions => _permissions;

    /// <inheritdoc />
    public bool IsAuthenticated => _currentUser != null;

    /// <inheritdoc />
    public void SetUser(AppUser user, IReadOnlyList<string> permissions)
    {
        _currentUser = user;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public void Clear()
    {
        _currentUser = null;
        _permissions = Array.Empty<string>();
    }

    /// <inheritdoc />
    public bool HasPermission(string permissionCode) =>
        _permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Permission checks against the current user context.</summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUserService _currentUser;

    public AuthorizationService(ICurrentUserService currentUser) => _currentUser = currentUser;

    /// <inheritdoc />
    public bool HasPermission(string permissionCode) => _currentUser.HasPermission(permissionCode);

    /// <inheritdoc />
    public Task EnsurePermissionAsync(string permissionCode, CancellationToken ct = default)
    {
        if (!_currentUser.HasPermission(permissionCode))
            throw new UnauthorizedAccessException($"Missing permission: {permissionCode}");
        return Task.CompletedTask;
    }
}
