using System.Windows;
using System.Windows.Media;

namespace Ftop.App.Views;

public sealed record ProcessRow(int Pid, string Name, string CpuText, string MemText, double CpuFraction, ImageSource? Icon);

// The busiest apps with their own icons. Rows are deliberately stable:
// the host only reorders when the lead actually changes, so two apps
// trading a point do not swap — ftop's "a list that rests".
public sealed class ProcessListView : FrameworkElement
{
    public const double RowH = 26;

    public IReadOnlyList<ProcessRow>? Rows { get; set; }
    public EasingEngine? Easer { get; set; }
    public double FrameDt { get; set; }

    protected override Size MeasureOverride(Size available)
    {
        int n = Rows?.Count ?? 0;
        return new Size(0, n * RowH);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rows = Rows;
        if (rows is null || Easer is null) return;
        var pal = Theme.Current;

        var textBrush = new SolidColorBrush(pal.Text);
        var dimBrush = new SolidColorBrush(pal.DimText);

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            double y = i * RowH;
            double cy = y + RowH / 2;

            if (r.Icon is not null)
                dc.DrawImage(r.Icon, new Rect(2, cy - 8, 16, 16));

            var name = Draw.Text(this, r.Name, 12, textBrush);
            if (name.Width > ActualWidth - 110)
            {
                // Ellipsize long names instead of clipping.
                string s = r.Name;
                while (s.Length > 4 && Draw.Text(this, s + "…", 12, textBrush).Width > ActualWidth - 110)
                    s = s[..^1];
                name = Draw.Text(this, s + "…", 12, textBrush);
            }
            dc.DrawText(name, new Point(24, cy - name.Height / 2));

            var cpu = Draw.Text(this, r.CpuText, 12, dimBrush, true);
            dc.DrawText(cpu, new Point(ActualWidth - cpu.Width, cy - cpu.Height / 2));

            double frac = Easer.Ease($"proc:{r.Pid}", Math.Clamp(r.CpuFraction, 0, 1), FrameDt);
            if (frac > 0.005)
            {
                var bar = new SolidColorBrush(Color.FromArgb(0x66, pal.Cpu.R, pal.Cpu.G, pal.Cpu.B));
                bar.Freeze();
                Draw.RoundedRect(dc, bar, new Rect(24, y + RowH - 3, (ActualWidth - 30) * frac, 2.5), 1.2);
            }
        }
    }
}
