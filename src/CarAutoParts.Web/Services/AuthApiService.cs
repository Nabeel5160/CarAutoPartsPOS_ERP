using CarAutoParts.Web.Auth;
using CarAutoParts.Web.Models;

namespace CarAutoParts.Web.Services;

public sealed class AuthApiService
{
    private readonly ApiClient _api;
    private readonly JwtAuthStateProvider _auth;

    public AuthApiService(ApiClient api, JwtAuthStateProvider auth)
    {
        _api = api;
        _auth = auth;
    }

    public async Task<(ApiLoginResponse? Data, string? Error)> LoginAsync(string username, string password)
    {
        var (data, error, _) = await _api.PostAsync<ApiLoginResponse>("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
        if (data is not null)
            await _auth.MarkUserAsAuthenticatedAsync(data);
        return (data, error);
    }

    public async Task LogoutAsync()
    {
        await _api.PostAsync("/api/auth/logout", new { });
        await _auth.MarkUserAsLoggedOutAsync();
    }

    public Task<(MeDto? Data, string? Error, int Status)> MeAsync() =>
        _api.GetAsync<MeDto>("/api/auth/me");

    public async Task<(bool Ok, string? Error, int Status)> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var result = await _api.PostAsync("/api/v1/auth/change-password", new { CurrentPassword = currentPassword, NewPassword = newPassword });
        if (result.Ok)
            await _auth.ClearMustChangePasswordAsync();
        return result;
    }
}
