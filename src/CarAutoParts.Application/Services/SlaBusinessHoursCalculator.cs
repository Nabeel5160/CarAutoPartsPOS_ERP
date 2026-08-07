using System.Globalization;
using System.Text.Json;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.Services;

/// <summary>Computes business-hours elapsed seconds between two UTC instants.</summary>
public static class SlaBusinessHoursCalculator
{
    private sealed record Interval(int Dow, TimeSpan Start, TimeSpan End);

    public static int ElapsedBusinessSeconds(DateTime utcFrom, DateTime utcTo, BusinessCalendar? calendar)
    {
        if (utcTo <= utcFrom) return 0;
        if (calendar is null || string.IsNullOrWhiteSpace(calendar.WorkIntervalsJson))
            return (int)Math.Max(0, (utcTo - utcFrom).TotalSeconds);

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            try { tz = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time"); }
            catch { return (int)Math.Max(0, (utcTo - utcFrom).TotalSeconds); }
        }
        catch (InvalidTimeZoneException)
        {
            return (int)Math.Max(0, (utcTo - utcFrom).TotalSeconds);
        }

        var intervals = ParseIntervals(calendar.WorkIntervalsJson);
        if (intervals.Count == 0)
            return (int)Math.Max(0, (utcTo - utcFrom).TotalSeconds);

        var holidays = ParseHolidays(calendar.HolidaysJson);
        var localFrom = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcFrom, DateTimeKind.Utc), tz);
        var localTo = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcTo, DateTimeKind.Utc), tz);

        var total = 0.0;
        var cursor = localFrom;
        while (cursor < localTo)
        {
            var dayEnd = cursor.Date.AddDays(1);
            var sliceEnd = localTo < dayEnd ? localTo : dayEnd;
            var dateKey = cursor.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!holidays.Contains(dateKey))
            {
                var dow = (int)cursor.DayOfWeek; // 0=Sun .. 6=Sat
                foreach (var iv in intervals.Where(i => i.Dow == dow))
                {
                    var open = cursor.Date + iv.Start;
                    var close = cursor.Date + iv.End;
                    var start = cursor > open ? cursor : open;
                    var end = sliceEnd < close ? sliceEnd : close;
                    if (end > start)
                        total += (end - start).TotalSeconds;
                }
            }

            cursor = sliceEnd;
        }

        return (int)Math.Max(0, Math.Floor(total));
    }

    public static int LiveElapsedSeconds(SlaTimer timer, DateTime utcNow, SlaCalendarMode mode, BusinessCalendar? calendar)
    {
        var baseElapsed = timer.ElapsedSeconds;
        if (timer.Status is not SlaTimerStatus.Running || timer.ActiveSince is null)
            return baseElapsed;

        var segment = mode == SlaCalendarMode.BusinessHours
            ? ElapsedBusinessSeconds(timer.ActiveSince.Value, utcNow, calendar)
            : (int)Math.Max(0, (utcNow - timer.ActiveSince.Value).TotalSeconds);
        return baseElapsed + segment;
    }

    private static List<Interval> ParseIntervals(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<Interval>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var dow = el.GetProperty("dow").GetInt32();
                var start = TimeSpan.Parse(el.GetProperty("start").GetString()!, CultureInfo.InvariantCulture);
                var end = TimeSpan.Parse(el.GetProperty("end").GetString()!, CultureInfo.InvariantCulture);
                list.Add(new Interval(dow, start, end));
            }
            return list;
        }
        catch
        {
            return [];
        }
    }

    private static HashSet<string> ParseHolidays(string json)
    {
        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(json) ?? [];
            return arr.ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
    }

    public static string DefaultMonSatJson() =>
        """[{"dow":1,"start":"09:00","end":"18:00"},{"dow":2,"start":"09:00","end":"18:00"},{"dow":3,"start":"09:00","end":"18:00"},{"dow":4,"start":"09:00","end":"18:00"},{"dow":5,"start":"09:00","end":"18:00"},{"dow":6,"start":"09:00","end":"18:00"}]""";
}
