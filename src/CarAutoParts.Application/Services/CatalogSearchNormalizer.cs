namespace CarAutoParts.Application.Services;

/// <summary>Scanner/paste normalization for barcode / SKU / OEM POS search.</summary>
public static class CatalogSearchNormalizer
{
    /// <summary>
    /// Builds exact-match candidates: trimmed input, leading-zero stripped (numeric),
    /// and common left-padded widths when scanners drop leading zeros.
    /// </summary>
    public static IReadOnlyList<string> BuildExactCandidates(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        var s = raw.Trim();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { s };

        if (s.All(char.IsDigit))
        {
            var stripped = s.TrimStart('0');
            if (string.IsNullOrEmpty(stripped))
                stripped = "0";
            set.Add(stripped);

            // Scanners often drop or add leading zeros — try every pad width up to 14.
            for (var width = stripped.Length + 1; width <= 14; width++)
                set.Add(stripped.PadLeft(width, '0'));
        }

        return set.ToList();
    }

    public static string NormalizePaste(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
}
