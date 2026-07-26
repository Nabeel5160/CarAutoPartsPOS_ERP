using CarAutoParts.Domain.Entities;

namespace CarAutoParts.Infrastructure.Services;

public class CurrentUserService : Application.Interfaces.ICurrentUserService
{
    public AppUser? CurrentUser { get; private set; }
    public IReadOnlyList<string> Permissions { get; private set; } = Array.Empty<string>();
    public bool IsAuthenticated => CurrentUser is not null;

    public void SetUser(AppUser user, IReadOnlyList<string> permissions)
    {
        CurrentUser = user;
        Permissions = permissions;
    }

    public void Clear()
    {
        CurrentUser = null;
        Permissions = Array.Empty<string>();
    }

    public bool HasPermission(string permissionCode)
        => Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
}
