using System.Windows;
using System.Windows.Media;

namespace Ftop.App.Views;

public sealed record MeterRow(string Label, string ValueText, double Fraction, Color Color, bool Available);

// Badge-led meter rows: memory, network, GPU, power.
// A badge with a filled arc is a gauge; unavailable reads show "—", never 0.
public sealed class MetersView : FrameworkElement
{
    public IReadOnlyList<MeterRow>? Rows { get; set; }
    public EasingEngine? Easer { get; set; }
    public double FrameDt { get; set; }

    protected override Size MeasureOverride(Size available)
    {
        int n = Rows?.Count ?? 0;
        return new Size(0, n * Theme.RowHeight);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rows = Rows;
        if (rows is null || Easer is null) return;
        var pal = Theme.Current;

        var textBrush = new SolidColorBrush(pal.Text);
        var dimBrush = new SolidColorBrush(pal.DimText);
        var trackBrush = new SolidColorBrush(pal.Track);

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            double y = i * Theme.RowHeight;
            double cy = y + Theme.RowHeight / 2;

            // Badge: filled circle, alpha follows the eased fraction.
            double frac = r.Available ? Easer.Ease($"meter:{r.Label}", Math.Clamp(r.Fraction, 0, 1), FrameDt) : 0;
            byte alpha = r.Available ? (byte)(0x55 + 0xAA * frac) : (byte)0x33;
            var badge = new SolidColorBrush(Color.FromArgb(alpha, r.Color.R, r.Color.G, r.Color.B));
            badge.Freeze();
            dc.DrawEllipse(badge, null, new Point(Theme.BadgeRadius, cy), Theme.BadgeRadius, Theme.BadgeRadius);

            // Label + value.
            var label = Draw.Text(this, r.Label, 12, dimBrush);
            dc.DrawText(label, new Point(Theme.BadgeRadius * 2 + 8, cy - label.Height / 2));
            var value = Draw.Text(this, r.Available ? r.ValueText : "—", 12, r.Available ? textBrush : dimBrush, true);
            dc.DrawText(value, new Point(ActualWidth - value.Width, cy - value.Height / 2));

            // Thin gauge bar under the row.
            if (r.Available && frac > 0.005)
            {
                var bar = new SolidColorBrush(Color.FromArgb(0x99, r.Color.R, r.Color.G, r.Color.B));
                bar.Freeze();
                double bw = (ActualWidth - 4) * frac;
                Draw.RoundedRect(dc, bar, new Rect(2, y + Theme.RowHeight - 3, bw, 2.5), 1.2);
            }
        }
    }
}
