namespace Ftop.Core;

public static class Format
{
    public static string Bytes(double bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int u = 0;
        while (bytes >= 1024 && u < units.Length - 1) { bytes /= 1024; u++; }
        return $"{bytes:0.#} {units[u]}";
    }

    public static string BytesPerSec(double bps) => $"{Bytes(bps)}/s";

    public static string Frequency(double mhz) =>
        mhz >= 1000 ? $"{mhz / 1000:0.00} GHz" : $"{mhz:0} MHz";

    public static string Percent(double v) => $"{v:0.#}%";

    public static string Watts(double w) => $"{w:0.#} W";

    public static string Celsius(double c) => $"{c:0.#}°C";
}
