using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Ftop.App.Views;

internal static class Draw
{
    public static FormattedText Text(Visual visual, string s, double size, Brush brush, bool semibold = false)
    {
        return new FormattedText(
            s,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"),
                FontStyles.Normal,
                semibold ? FontWeights.SemiBold : FontWeights.Normal,
                FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(visual).PixelsPerDip);
    }

    public static void RoundedRect(DrawingContext dc, Brush brush, Rect rect, double radius)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        dc.DrawRoundedRectangle(brush, null, rect, radius, radius);
    }
}
