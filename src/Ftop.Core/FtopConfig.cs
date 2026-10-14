using System.Text.Json;

namespace Ftop.Core;

// User-owned config, stored at %APPDATA%\Ftop\config.json.
// Invalid files keep the last good config; the UI surfaces the error.
public sealed record FtopConfig(
    List<string> ModuleOrder,
    string Palette,
    int RefreshMs,
    bool ReducedMotion,
    bool ShowUnavailable)
{
    public static FtopConfig Default => new(
        new List<string> { "cores", "memory", "network", "gpu", "power", "processes" },
        "dark",
        1000,
        false,
        true);

    private static readonly HashSet<string> KnownModules = new(StringComparer.OrdinalIgnoreCase)
        { "cores", "memory", "network", "gpu", "power", "processes" };

    public static (FtopConfig Config, string? Error) Load(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            var cfg = Default;
            if (raw is null) return (cfg, "empty config, using defaults");

            if (raw.TryGetValue("moduleOrder", out var mo) && mo.ValueKind == JsonValueKind.Array)
            {
                var order = mo.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => s is not null && KnownModules.Contains(s))
                    .Select(s => s!)
                    .ToList();
                if (order.Count > 0) cfg = cfg with { ModuleOrder = order };
            }
            if (raw.TryGetValue("palette", out var pal) && pal.ValueKind == JsonValueKind.String)
            {
                var p = pal.GetString();
                if (p is "dark" or "light") cfg = cfg with { Palette = p };
            }
            if (raw.TryGetValue("refreshMs", out var r) && r.ValueKind == JsonValueKind.Number)
            {
                var ms = Math.Clamp(r.GetInt32(), 250, 5000);
                cfg = cfg with { RefreshMs = ms };
            }
            if (raw.TryGetValue("reducedMotion", out var rm) && (rm.ValueKind == JsonValueKind.True || rm.ValueKind == JsonValueKind.False))
                cfg = cfg with { ReducedMotion = rm.GetBoolean() };
            if (raw.TryGetValue("showUnavailable", out var su) && (su.ValueKind == JsonValueKind.True || su.ValueKind == JsonValueKind.False))
                cfg = cfg with { ShowUnavailable = su.GetBoolean() };

            return (cfg, null);
        }
        catch (Exception ex)
        {
            return (Default, ex.Message);
        }
    }

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ftop", "config.json");

    public static void EnsureExists(string path)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            """
            {
              // Module order and visibility. Known modules:
              // "cores", "memory", "network", "gpu", "power", "processes"
              "moduleOrder": ["cores", "memory", "network", "gpu", "power", "processes"],
              "palette": "dark",      // "dark" | "light"
              "refreshMs": 1000,      // 250..5000
              "reducedMotion": false,
              "showUnavailable": true
            }
            """);
    }
}
