using System.Security.Cryptography;
using System.Text;

namespace CarAutoParts.Application.Security;

/// <summary>RFC 6238 TOTP (HMAC-SHA1, 30s step, 6 digits) without external packages.</summary>
public static class TotpHelper
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private static readonly char[] Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    public static string GenerateSecret(int byteLength = 20)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return ToBase32(bytes);
    }

    public static string BuildOtpAuthUri(string secret, string accountName, string issuer = "CarAutoParts")
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var iss = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secret}&issuer={iss}&digits={Digits}&period={StepSeconds}";
    }

    public static bool VerifyCode(string base32Secret, string code, int windowSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits || !code.All(char.IsDigit))
            return false;

        var key = FromBase32(base32Secret);
        var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        for (var w = -windowSteps; w <= windowSteps; w++)
        {
            if (ComputeTotp(key, timestep + w) == code)
                return true;
        }
        return false;
    }

    public static IReadOnlyList<string> GenerateBackupCodes(int count = 8)
    {
        var list = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var n = RandomNumberGenerator.GetInt32(0, 100_000_000);
            list.Add(n.ToString("D8"));
        }
        return list;
    }

    private static string ComputeTotp(byte[] key, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);
        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString($"D{Digits}");
    }

    private static string ToBase32(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = data[0], next = 1, bitsLeft = 8;
        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length)
                {
                    buffer <<= 8;
                    buffer |= data[next++] & 0xFF;
                    bitsLeft += 8;
                }
                else
                {
                    var pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft += pad;
                }
            }
            var index = (buffer >> (bitsLeft - 5)) & 0x1F;
            bitsLeft -= 5;
            sb.Append(Base32Alphabet[index]);
        }
        return sb.ToString();
    }

    private static byte[] FromBase32(string input)
    {
        var cleaned = input.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", "");
        var output = new List<byte>(cleaned.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var c in cleaned)
        {
            var val = Array.IndexOf(Base32Alphabet, c);
            if (val < 0) throw new FormatException("Invalid Base32 secret.");
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return output.ToArray();
    }
}
