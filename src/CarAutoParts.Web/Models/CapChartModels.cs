namespace CarAutoParts.Web.Models;

/// <summary>Serializable Apache ECharts payload for <c>capCharts.render</c>.</summary>
public sealed class CapChartSpec
{
    public string Type { get; set; } = "bar";
    public IReadOnlyList<string> Labels { get; set; } = [];
    public IReadOnlyList<CapChartDataset> Datasets { get; set; } = [];
    public bool Stacked { get; set; }
    public string? IndexAxis { get; set; }
    public string? StackId { get; set; }

    /// <summary>Category axis for bar3D (X).</summary>
    public IReadOnlyList<string>? XCategories { get; set; }

    /// <summary>Category axis for bar3D (Y).</summary>
    public IReadOnlyList<string>? YCategories { get; set; }

    public IReadOnlyList<CapChartPoint3D>? Points3D { get; set; }
    public IReadOnlyList<CapChartBubblePoint>? Bubbles { get; set; }
    public IReadOnlyList<CapChartFrame>? Frames { get; set; }
    public CapChartPlayback? Playback { get; set; }

    /// <summary>When true, bar3D camera auto-rotates (default off).</summary>
    public bool AutoRotate { get; set; }

    public bool HasRenderableData()
    {
        if (Frames is { Count: > 0 })
            return Frames.Any(f =>
                (f.Datasets is { Count: > 0 } && f.Datasets.Any(d => d.Data.Count > 0))
                || (f.Points3D is { Count: > 0 })
                || (f.Labels is { Count: > 0 }));

        if (Points3D is { Count: > 0 }) return true;
        if (Bubbles is { Count: > 0 }) return true;
        if (Labels.Count == 0 || Datasets.Count == 0) return false;
        return Datasets.Any(d => d.Data.Count > 0);
    }
}

public sealed class CapChartDataset
{
    public string Label { get; set; } = string.Empty;
    public IReadOnlyList<decimal> Data { get; set; } = [];
    public string? ColorRole { get; set; }
    public bool Fill { get; set; }
    public bool? Tension { get; set; }
}

public sealed class CapChartFrame
{
    public string Label { get; set; } = string.Empty;
    public string? Type { get; set; }
    public IReadOnlyList<string>? Labels { get; set; }
    public IReadOnlyList<CapChartDataset>? Datasets { get; set; }
    public IReadOnlyList<CapChartPoint3D>? Points3D { get; set; }
    public IReadOnlyList<string>? XCategories { get; set; }
    public IReadOnlyList<string>? YCategories { get; set; }
}

public sealed class CapChartPlayback
{
    public bool AutoPlay { get; set; }
    public int IntervalMs { get; set; } = 1200;
    public bool Loop { get; set; } = true;
    public int CurrentIndex { get; set; }
}

public sealed class CapChartPoint3D
{
    public int X { get; set; }
    public int Y { get; set; }
    public decimal Z { get; set; }
}

public sealed class CapChartBubblePoint
{
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal R { get; set; }
    public string? Name { get; set; }
}

public static class CapChartFactory
{
    public static CapChartSpec Line(IEnumerable<string> labels, string seriesLabel, IEnumerable<decimal> values, bool fill = true, string colorRole = "accent") =>
        new()
        {
            Type = "line",
            Labels = labels.ToList(),
            Datasets =
            [
                new CapChartDataset
                {
                    Label = seriesLabel,
                    Data = values.ToList(),
                    ColorRole = colorRole,
                    Fill = fill
                }
            ]
        };

    public static CapChartSpec DualLine(
        IEnumerable<string> labels,
        string aLabel, IEnumerable<decimal> a,
        string bLabel, IEnumerable<decimal> b,
        bool fill = true) =>
        new()
        {
            Type = "line",
            Labels = labels.ToList(),
            Datasets =
            [
                new CapChartDataset { Label = aLabel, Data = a.ToList(), ColorRole = "accent", Fill = fill },
                new CapChartDataset { Label = bLabel, Data = b.ToList(), ColorRole = "accent2", Fill = fill }
            ]
        };

    public static CapChartSpec DualBar(IEnumerable<string> labels, string aLabel, IEnumerable<decimal> a, string bLabel, IEnumerable<decimal> b) =>
        new()
        {
            Type = "bar",
            Labels = labels.ToList(),
            Datasets =
            [
                new CapChartDataset { Label = aLabel, Data = a.ToList(), ColorRole = "accent" },
                new CapChartDataset { Label = bLabel, Data = b.ToList(), ColorRole = "accent2" }
            ]
        };

    public static CapChartSpec HorizontalBar(IEnumerable<string> labels, string seriesLabel, IEnumerable<decimal> values, string colorRole = "accent") =>
        new()
        {
            Type = "horizontalBar",
            Labels = labels.ToList(),
            Datasets =
            [
                new CapChartDataset { Label = seriesLabel, Data = values.ToList(), ColorRole = colorRole }
            ]
        };

    public static CapChartSpec Doughnut(IEnumerable<string> labels, string seriesLabel, IEnumerable<decimal> values) =>
        new()
        {
            Type = "doughnut",
            Labels = labels.ToList(),
            Datasets =
            [
                new CapChartDataset { Label = seriesLabel, Data = values.ToList() }
            ]
        };

    public static CapChartSpec StackedArea(IEnumerable<string> labels, params (string Label, IEnumerable<decimal> Values, string? ColorRole)[] series) =>
        new()
        {
            Type = "stackedArea",
            Stacked = true,
            Labels = labels.ToList(),
            Datasets = series.Select(s => new CapChartDataset
            {
                Label = s.Label,
                Data = s.Values.ToList(),
                ColorRole = s.ColorRole,
                Fill = true
            }).ToList()
        };

    public static CapChartSpec GroupedBar(IEnumerable<string> labels, params (string Label, IEnumerable<decimal> Values, string? ColorRole)[] series) =>
        new()
        {
            Type = "bar",
            Labels = labels.ToList(),
            Datasets = series.Select(s => new CapChartDataset
            {
                Label = s.Label,
                Data = s.Values.ToList(),
                ColorRole = s.ColorRole
            }).ToList()
        };

    public static CapChartSpec StackedBar(IEnumerable<string> labels, params (string Label, IEnumerable<decimal> Values, string? ColorRole)[] series) =>
        new()
        {
            Type = "bar",
            Stacked = true,
            Labels = labels.ToList(),
            Datasets = series.Select(s => new CapChartDataset
            {
                Label = s.Label,
                Data = s.Values.ToList(),
                ColorRole = s.ColorRole
            }).ToList()
        };

    public static CapChartSpec Bubble(IEnumerable<CapChartBubblePoint> points) =>
        new()
        {
            Type = "bubble",
            Labels = ["bubble"],
            Datasets = [new CapChartDataset { Label = "Bubbles", Data = [1] }],
            Bubbles = points.ToList()
        };

    public static CapChartSpec Timeline(
        IReadOnlyList<CapChartFrame> frames,
        string baseType = "line",
        bool autoPlay = false,
        int intervalMs = 1200) =>
        new()
        {
            Type = "timeline",
            Labels = frames.FirstOrDefault()?.Labels ?? [],
            Datasets = frames.FirstOrDefault()?.Datasets ?? [],
            Frames = frames,
            Playback = new CapChartPlayback
            {
                AutoPlay = autoPlay,
                IntervalMs = intervalMs,
                Loop = true,
                CurrentIndex = 0
            }
        };

    public static CapChartSpec Bar3D(
        IReadOnlyList<string> xCategories,
        IReadOnlyList<string> yCategories,
        IReadOnlyList<CapChartPoint3D> points,
        IReadOnlyList<CapChartFrame>? frames = null,
        bool autoRotate = false) =>
        new()
        {
            Type = "bar3d",
            Labels = xCategories,
            XCategories = xCategories,
            YCategories = yCategories,
            Points3D = points,
            Frames = frames,
            AutoRotate = autoRotate,
            Datasets = [new CapChartDataset { Label = "Sales", Data = points.Select(p => p.Z).ToList() }],
            Playback = frames is { Count: > 0 }
                ? new CapChartPlayback { AutoPlay = false, IntervalMs = 1400, Loop = true }
                : null
        };
}
