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
        if (data is not null && !data.MfaRequired && !string.IsNullOrWhiteSpace(data.AccessToken))
        {
            try
            {
                await _auth.MarkUserAsAuthenticatedAsync(data);
            }
            catch (Exception ex)
            {
                return (null, "Could not store session: " + ex.Message);
            }
        }
        return (data, error);
    }

    public async Task<(ApiLoginResponse? Data, string? Error)> VerifyMfaAsync(string mfaTicket, string code)
    {
        var (data, error, _) = await _api.PostAsync<ApiLoginResponse>("/api/auth/mfa/verify", new { MfaTicket = mfaTicket, Code = code });
        if (data is not null && !string.IsNullOrWhiteSpace(data.AccessToken))
            await _auth.MarkUserAsAuthenticatedAsync(data);
        return (data, error);
    }

    public Task<(MfaEnrollDto? Data, string? Error, int Status)> BeginMfaEnrollAsync() =>
        _api.PostAsync<MfaEnrollDto>("/api/auth/mfa/enroll/begin", new { });

    public Task<(bool Ok, string? Error, int Status)> ConfirmMfaEnrollAsync(string code) =>
        _api.PostAsync("/api/auth/mfa/enroll/confirm", new { Code = code });

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

public sealed class MfaEnrollDto
{
    public string Secret { get; set; } = "";
    public string OtpAuthUri { get; set; } = "";
    public List<string> BackupCodes { get; set; } = [];
}
