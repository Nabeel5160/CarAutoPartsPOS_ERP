using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Security;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CarAutoParts.Application.Services;

public record MfaEnrollDto(string Secret, string OtpAuthUri, IReadOnlyList<string> BackupCodes);
public record MfaStatusDto(bool MfaEnabled, bool MfaEnforced, bool MustEnroll);

public interface IMfaService
{
    bool ShouldEnforceMfa(IReadOnlyList<string> permissions);
    Task<MfaStatusDto> GetStatusAsync(int userId, CancellationToken ct = default);
    Task<Result<MfaEnrollDto>> BeginEnrollAsync(int userId, CancellationToken ct = default);
    Task<Result> ConfirmEnrollAsync(int userId, string code, CancellationToken ct = default);
    Task<Result> DisableAsync(int userId, string password, string codeOrBackup, CancellationToken ct = default);
    Task<Result> AdminResetAsync(int userId, CancellationToken ct = default);
    string IssueMfaTicket(int userId);
    bool TryConsumeMfaTicket(string ticket, out int userId);
    Task<Result> VerifyLoginCodeAsync(int userId, string codeOrBackup, CancellationToken ct = default);
}

public sealed class MfaService : IMfaService
{
    private readonly IRepository<AppUser> _users;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;

    // Pending enroll secrets keyed by userId until confirmed
    private static readonly MemoryCacheEntryOptions EnrollOpts = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
    private static readonly MemoryCacheEntryOptions TicketOpts = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

    public MfaService(
        IRepository<AppUser> users,
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IMemoryCache cache)
    {
        _users = users;
        _uow = uow;
        _currentUser = currentUser;
        _cache = cache;
    }

    public bool ShouldEnforceMfa(IReadOnlyList<string> permissions) =>
        MfaEnrollmentPolicy.MustEnroll(mfaEnabled: false, mfaEnforced: false, permissions);

    public async Task<MfaStatusDto> GetStatusAsync(int userId, CancellationToken ct = default)
    {
        var user = await _users.Query().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
            ?? throw new InvalidOperationException("User not found.");
        var perms = _currentUser.Permissions;
        var mustEnroll = MfaEnrollmentPolicy.MustEnroll(user.MfaEnabled, user.MfaEnforced, perms);
        var policyEnforced = user.MfaEnforced
            || (!MfaEnrollmentPolicy.IsCounterCashierPath(perms) && MfaEnrollmentPolicy.IsPrivileged(perms));
        return new MfaStatusDto(user.MfaEnabled, policyEnforced, mustEnroll);
    }

    public async Task<Result<MfaEnrollDto>> BeginEnrollAsync(int userId, CancellationToken ct = default)
    {
        var user = await _users.Query().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null) return Result<MfaEnrollDto>.Failure("User not found.");

        var secret = TotpHelper.GenerateSecret();
        var backup = TotpHelper.GenerateBackupCodes();
        _cache.Set(EnrollKey(userId), (secret, backup), EnrollOpts);

        var uri = TotpHelper.BuildOtpAuthUri(secret, user.Username);
        return Result<MfaEnrollDto>.Success(new MfaEnrollDto(secret, uri, backup));
    }

    public async Task<Result> ConfirmEnrollAsync(int userId, string code, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(EnrollKey(userId), out (string Secret, IReadOnlyList<string> Backup) pending))
            return Result.Failure("No enrollment in progress. Start enroll again.");

        if (!TotpHelper.VerifyCode(pending.Secret, code))
            return Result.Failure("Invalid authenticator code.");

        var user = await _users.Query().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null) return Result.Failure("User not found.");

        user.MfaSecret = pending.Secret;
        user.MfaEnabled = true;
        user.MfaEnforced = true;
        user.MfaBackupCodesHashJson = JsonSerializer.Serialize(
            pending.Backup.Select(BCrypt.Net.BCrypt.HashPassword).ToList());
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        _cache.Remove(EnrollKey(userId));
        return Result.Success();
    }

    public async Task<Result> DisableAsync(int userId, string password, string codeOrBackup, CancellationToken ct = default)
    {
        var user = await _users.Query().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null) return Result.Failure("User not found.");
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return Result.Failure("Password incorrect.");
        if (!await VerifyCodeOrBackupAsync(user, codeOrBackup, consumeBackup: true, ct))
            return Result.Failure("Invalid MFA code.");

        user.MfaEnabled = false;
        user.MfaSecret = null;
        user.MfaBackupCodesHashJson = null;
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> AdminResetAsync(int userId, CancellationToken ct = default)
    {
        if (!_currentUser.HasPermission(Permissions.MfaManage) && !_currentUser.HasPermission(Permissions.UsersManage))
            return Result.Failure("Missing auth.mfa.manage permission.");

        var user = await _users.Query().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null) return Result.Failure("User not found.");

        user.MfaEnabled = false;
        user.MfaSecret = null;
        user.MfaBackupCodesHashJson = null;
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public string IssueMfaTicket(int userId)
    {
        var ticket = Guid.NewGuid().ToString("N");
        _cache.Set(TicketKey(ticket), userId, TicketOpts);
        return ticket;
    }

    public bool TryConsumeMfaTicket(string ticket, out int userId)
    {
        userId = 0;
        if (string.IsNullOrWhiteSpace(ticket)) return false;
        if (!_cache.TryGetValue(TicketKey(ticket), out int id)) return false;
        _cache.Remove(TicketKey(ticket));
        userId = id;
        return true;
    }

    public async Task<Result> VerifyLoginCodeAsync(int userId, string codeOrBackup, CancellationToken ct = default)
    {
        var user = await _users.Query().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null) return Result.Failure("User not found.");
        if (!user.MfaEnabled || string.IsNullOrEmpty(user.MfaSecret))
            return Result.Failure("MFA is not enabled for this user.");
        if (!await VerifyCodeOrBackupAsync(user, codeOrBackup, consumeBackup: true, ct))
            return Result.Failure("Invalid MFA code.");
        return Result.Success();
    }

    private async Task<bool> VerifyCodeOrBackupAsync(AppUser user, string codeOrBackup, bool consumeBackup, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(user.MfaSecret) && TotpHelper.VerifyCode(user.MfaSecret, codeOrBackup.Trim()))
            return true;

        if (string.IsNullOrWhiteSpace(user.MfaBackupCodesHashJson))
            return false;

        var hashes = JsonSerializer.Deserialize<List<string>>(user.MfaBackupCodesHashJson) ?? [];
        for (var i = 0; i < hashes.Count; i++)
        {
            if (!BCrypt.Net.BCrypt.Verify(codeOrBackup.Trim(), hashes[i])) continue;
            if (consumeBackup)
            {
                hashes.RemoveAt(i);
                user.MfaBackupCodesHashJson = JsonSerializer.Serialize(hashes);
                _users.Update(user);
                await _uow.SaveChangesAsync(ct);
            }
            return true;
        }
        return false;
    }

    private static string EnrollKey(int userId) => $"mfa-enroll:{userId}";
    private static string TicketKey(string ticket) => $"mfa-ticket:{ticket}";
}
