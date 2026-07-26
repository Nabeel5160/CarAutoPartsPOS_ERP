using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace CarAutoParts.Web.Auth;

public sealed class PermissionService
{
    private readonly AuthenticationStateProvider _authState;

    public PermissionService(AuthenticationStateProvider authState) => _authState = authState;

    public async Task<bool> HasAsync(string permission)
    {
        var state = await _authState.GetAuthenticationStateAsync();
        return state.User.Claims.Any(c =>
            c.Type is "permission" &&
            string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<string>> GetAllAsync()
    {
        var state = await _authState.GetAuthenticationStateAsync();
        return state.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string> DisplayNameAsync()
    {
        var state = await _authState.GetAuthenticationStateAsync();
        return state.User.FindFirst("display_name")?.Value
               ?? state.User.Identity?.Name
               ?? "User";
    }
}
