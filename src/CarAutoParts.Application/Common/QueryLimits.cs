namespace CarAutoParts.Application.Common;

/// <summary>Phase 19 — shared caps for list/report/POS query shaping.</summary>
public static class QueryLimits
{
    /// <summary>Max page size for paged list APIs (<see cref="QueryableExtensions.ToPagedResultAsync"/>).</summary>
    public const int MaxPageSize = 500;

    /// <summary>Default page size when callers omit it.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>POS exact barcode/SKU/OEM/part match Take().</summary>
    public const int PosExactMatchTake = 50;

    /// <summary>POS Contains / fitment browse Take().</summary>
    public const int PosSoftSearchTake = 100;

    /// <summary>Interactive day-range reports (JSON grids) — keeps latency under budget.</summary>
    public const int MaxInteractiveReportDays = 93;

    /// <summary>Excel/PDF export date span — wider than interactive, still bounded.</summary>
    public const int MaxExportReportDays = 366;

    /// <summary>Stock movements / FBR register row caps (already applied in ReportService).</summary>
    public const int MaxMovementRows = 5000;

    public const int MaxFbrRegisterRows = 1000;
}

/// <summary>Validates and normalizes report date ranges against <see cref="QueryLimits"/>.</summary>
public static class ReportDateRange
{
    /// <summary>
    /// Ensures <paramref name="from"/> ≤ <paramref name="to"/> and the inclusive day span
    /// does not exceed <paramref name="maxDays"/>.
    /// </summary>
    public static Result<(DateTime From, DateTime To)> Validate(DateTime from, DateTime to, int maxDays)
    {
        if (maxDays < 1)
            return Result<(DateTime, DateTime)>.Failure("maxDays must be at least 1.");

        var start = from.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(from.Date, DateTimeKind.Utc)
            : from.Date;
        var end = to.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(to.Date, DateTimeKind.Utc)
            : to.Date;

        // Inclusive end-of-day for same-day / range filters that use <= to
        if (to.TimeOfDay == TimeSpan.Zero && end == end.Date)
            end = end.Date.AddDays(1).AddTicks(-1);

        if (end.Date < start.Date)
            return Result<(DateTime, DateTime)>.Failure("To date must be on or after From date.");

        var inclusiveDays = (end.Date - start.Date).TotalDays + 1;
        if (inclusiveDays > maxDays)
        {
            return Result<(DateTime, DateTime)>.Failure(
                $"Date range cannot exceed {maxDays} days (requested {inclusiveDays:0}). Narrow the filter and try again.");
        }

        return Result<(DateTime, DateTime)>.Success((start, end));
    }

    public static Result<(DateTime From, DateTime To)> ValidateInteractive(DateTime from, DateTime to)
        => Validate(from, to, QueryLimits.MaxInteractiveReportDays);

    public static Result<(DateTime From, DateTime To)> ValidateExport(DateTime from, DateTime to)
        => Validate(from, to, QueryLimits.MaxExportReportDays);
}
