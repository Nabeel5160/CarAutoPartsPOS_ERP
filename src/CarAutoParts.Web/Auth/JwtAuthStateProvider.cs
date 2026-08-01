using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Blazored.LocalStorage;
using CarAutoParts.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace CarAutoParts.Web.Auth;

public sealed class JwtAuthStateProvider : AuthenticationStateProvider
{
    public const string TokenKey = "cap.accessToken";
    public const string UserKey = "cap.user";

    private readonly ILocalStorageService _storage;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public JwtAuthStateProvider(ILocalStorageService storage) => _storage = storage;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _storage.GetItemAsStringAsync(TokenKey);
            if (string.IsNullOrWhiteSpace(token))
                return new AuthenticationState(_anonymous);

            token = token.Trim('"');
            var principal = CreatePrincipal(token, await _storage.GetItemAsync<LoginUserDto>(UserKey));
            return new AuthenticationState(principal);
        }
        catch
        {
            // LocalStorage/JS may not be ready during very early boot — do not block the UI.
            return new AuthenticationState(_anonymous);
        }
    }

    public async Task MarkUserAsAuthenticatedAsync(ApiLoginResponse login)
    {
        if (string.IsNullOrWhiteSpace(login.AccessToken) || login.User is null)
            throw new InvalidOperationException("Cannot authenticate without access token.");
        await _storage.SetItemAsStringAsync(TokenKey, login.AccessToken);
        await _storage.SetItemAsync(UserKey, login.User);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(
            CreatePrincipal(login.AccessToken, login.User))));
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await _storage.RemoveItemAsync(TokenKey);
        await _storage.RemoveItemAsync(UserKey);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    public async Task ClearMustChangePasswordAsync()
    {
        var user = await GetUserAsync();
        if (user is null || !user.MustChangePassword) return;
        user.MustChangePassword = false;
        await _storage.SetItemAsync(UserKey, user);
        var token = await GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CreatePrincipal(token, user))));
    }

    public async Task<string?> GetTokenAsync()
    {
        var token = await _storage.GetItemAsStringAsync(TokenKey);
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim('"');
    }

    public async Task<LoginUserDto?> GetUserAsync() =>
        await _storage.GetItemAsync<LoginUserDto>(UserKey);

    private static ClaimsPrincipal CreatePrincipal(string token, LoginUserDto? user)
    {
        var identity = new ClaimsIdentity("jwt");
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            identity.AddClaims(jwt.Claims);
        }
        catch
        {
            // ignore malformed token
        }

        if (user is not null)
        {
            identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));
            identity.AddClaim(new Claim("display_name", user.DisplayName));
            foreach (var role in user.Roles)
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            foreach (var permission in user.Permissions)
                identity.AddClaim(new Claim("permission", permission));
        }

        return new ClaimsPrincipal(identity);
    }
}
