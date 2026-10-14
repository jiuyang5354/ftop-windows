using System.Windows;
using System.Windows.Media;
using Ftop.Core;

namespace Ftop.App.Views;

// One column per core: fill = eased usage, tick = eased frequency.
// Columns glide toward new readings instead of jumping once a second.
public sealed class CoresView : FrameworkElement
{
    public IReadOnlyList<CoreReading>? Cores { get; set; }
    public EasingEngine? Easer { get; set; }
    public double FrameDt { get; set; }

    protected override Size MeasureOverride(Size available)
    {
        int n = Cores?.Count ?? 0;
        if (n == 0) return new Size(0, 0);
        double w = n * Theme.ColumnWidth + (n - 1) * Theme.ColumnGap;
        return new Size(w, Theme.ColumnMaxHeight + 26);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var cores = Cores;
        if (cores is null || cores.Count == 0 || Easer is null) return;
        var pal = Theme.Current;

        double maxFreq = 0;
        foreach (var c in cores)
            if (c.FrequencyMhz.IsAvailable) maxFreq = Math.Max(maxFreq, c.FrequencyMhz.Value);
        if (maxFreq <= 0) maxFreq = 1;

        var trackBrush = new SolidColorBrush(pal.Track);
        var labelBrush = new SolidColorBrush(pal.DimText);
        trackBrush.Freeze();

        double h = Theme.ColumnMaxHeight;
        for (int i = 0; i < cores.Count; i++)
        {
            var c = cores[i];
            double x = i * (Theme.ColumnWidth + Theme.ColumnGap);

            // Track.
            Draw.RoundedRect(dc, trackBrush, new Rect(x, 0, Theme.ColumnWidth, h), Theme.ColumnWidth / 2);

            // Eased fill.
            double usage = c.UsagePercent.IsAvailable ? c.UsagePercent.Value : 0;
            double eased = Easer.Ease($"core:{i}:use", usage, FrameDt);
            double fillH = h * Math.Clamp(eased / 100.0, 0, 1);
            if (fillH > 0.5)
            {
                var col = c.Group % 2 == 0 ? pal.Cpu : pal.CpuAlt;
                var b = new SolidColorBrush(col);
                b.Freeze();
                Draw.RoundedRect(dc, b, new Rect(x, h - fillH, Theme.ColumnWidth, fillH), Theme.ColumnWidth / 2);
            }

            // Frequency tick: a short line across the column.
            if (c.FrequencyMhz.IsAvailable)
            {
                double fe = Easer.Ease($"core:{i}:freq", c.FrequencyMhz.Value, FrameDt);
                double y = h - h * Math.Clamp(fe / maxFreq, 0, 1);
                var tick = new Pen(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)), 1.2);
                tick.Freeze();
                dc.DrawLine(tick, new Point(x - 1, y), new Point(x + Theme.ColumnWidth + 1, y));
            }
        }

        // Caption: "CPU" + average.
        double avg = cores.Where(c => c.UsagePercent.IsAvailable).Select(c => c.UsagePercent.Value).DefaultIfEmpty(0).Average();
        var cap = Draw.Text(this, $"CPU  {Format.Percent(avg)}", 11, labelBrush);
        dc.DrawText(cap, new Point(0, h + 8));
    }
}
