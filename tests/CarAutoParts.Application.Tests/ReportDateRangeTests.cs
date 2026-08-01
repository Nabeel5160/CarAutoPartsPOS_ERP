using CarAutoParts.Application.Common;
using FluentAssertions;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class ReportDateRangeTests
{
    [Fact]
    public void Validate_accepts_same_day()
    {
        var day = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);
        var result = ReportDateRange.Validate(day, day, maxDays: 93);
        result.Succeeded.Should().BeTrue();
        result.Data.From.Date.Should().Be(day.Date);
        result.Data.To.TimeOfDay.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void ValidateInteractive_rejects_over_93_days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(93); // 94 inclusive days
        var result = ReportDateRange.ValidateInteractive(from, to);
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("93");
    }

    [Fact]
    public void ValidateInteractive_accepts_93_inclusive_days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(92); // 93 inclusive
        var result = ReportDateRange.ValidateInteractive(from, to);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ValidateExport_allows_up_to_366_days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(365);
        ReportDateRange.ValidateExport(from, to).Succeeded.Should().BeTrue();
        ReportDateRange.ValidateExport(from, from.AddDays(366)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Validate_rejects_inverted_range()
    {
        var result = ReportDateRange.Validate(
            new DateTime(2026, 7, 10),
            new DateTime(2026, 7, 1),
            31);
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("on or after");
    }

    [Fact]
    public void QueryLimits_page_size_matches_extension_clamp()
    {
        QueryLimits.MaxPageSize.Should().Be(500);
        QueryLimits.PosExactMatchTake.Should().Be(50);
        QueryLimits.PosSoftSearchTake.Should().Be(100);
        QueryLimits.MaxInteractiveReportDays.Should().Be(93);
        QueryLimits.MaxExportReportDays.Should().Be(366);
    }
}
