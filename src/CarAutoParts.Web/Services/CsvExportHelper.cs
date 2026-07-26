using System.Text;

namespace CarAutoParts.Web.Services;

public static class CsvExportHelper
{
    public static byte[] ToUtf8Bytes(string csv) => Encoding.UTF8.GetBytes(csv);

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
