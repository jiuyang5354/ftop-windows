using System.Windows.Media;

namespace Ftop.App;

// Design tokens live once, here. A theme is data: adding a palette
// never needs new views. (Mirrors ftop's FtopUI/Theme.)
public sealed record Palette(
    Color PanelFill,
    Color PanelStroke,
    Color Text,
    Color DimText,
    Color Track,
    Color Cpu,
    Color CpuAlt,
    Color Memory,
    Color Network,
    Color Gpu,
    Color Power);

public static class Theme
{
    public static Palette Current { get; private set; } = Dark;

    public static readonly Palette Dark = new(
        PanelFill: Color.FromArgb(0x59, 0x14, 0x14, 0x1C),
        PanelStroke: Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF),
        Text: Color.FromRgb(0xF5, 0xF5, 0xF7),
        DimText: Color.FromRgb(0x9A, 0x9A, 0xA2),
        Track: Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF),
        Cpu: Color.FromRgb(0x5E, 0xB1, 0xFF),
        CpuAlt: Color.FromRgb(0x7D, 0xD3, 0xFC),
        Memory: Color.FromRgb(0x4A, 0xDE, 0x80),
        Network: Color.FromRgb(0xFB, 0x92, 0x3C),
        Gpu: Color.FromRgb(0xC0, 0x84, 0xFC),
        Power: Color.FromRgb(0xFA, 0xCC, 0x15));

    public static readonly Palette Light = new(
        PanelFill: Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF),
        PanelStroke: Color.FromArgb(0x40, 0x00, 0x00, 0x00),
        Text: Color.FromRgb(0x1C, 0x1C, 0x1E),
        DimText: Color.FromRgb(0x6E, 0x6E, 0x73),
        Track: Color.FromArgb(0x2E, 0x00, 0x00, 0x00),
        Cpu: Color.FromRgb(0x0A, 0x84, 0xFF),
        CpuAlt: Color.FromRgb(0x64, 0xD2, 0xFF),
        Memory: Color.FromRgb(0x1F, 0xA3, 0x4A),
        Network: Color.FromRgb(0xE8, 0x6E, 0x0B),
        Gpu: Color.FromRgb(0x9D, 0x5C, 0xF5),
        Power: Color.FromRgb(0xB8, 0x8E, 0x0A));

    public static void SetPalette(string name) =>
        Current = name == "light" ? Light : Dark;

    // Spacing / radii / motion
    public const double CornerRadius = 14;
    public const double ModuleGap = 14;
    public const double Pad = 14;
    public const double ColumnWidth = 10;
    public const double ColumnGap = 5;
    public const double ColumnMaxHeight = 120;
    public const double BadgeRadius = 9;
    public const double RowHeight = 24;

    // Exponential-ease speed (per second); higher = snappier.
    public const double EaseSpeed = 7.0;
}
