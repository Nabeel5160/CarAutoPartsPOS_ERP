using System.Text.RegularExpressions;

namespace CarAutoParts.Application.Security;

/// <summary>Password policy for M4 security hardening.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static bool IsValid(string? password, out string? error)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
        {
            error = $"Password must be at least {MinLength} characters.";
            return false;
        }

        if (!Regex.IsMatch(password, "[A-Za-z]"))
        {
            error = "Password must contain a letter.";
            return false;
        }

        if (!Regex.IsMatch(password, "[0-9]"))
        {
            error = "Password must contain a digit.";
            return false;
        }

        error = null;
        return true;
    }
}
