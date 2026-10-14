using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;
using Ftop.Core;

namespace Ftop.Sensors;

// GPU utilization from the "GPU Engine" performance counters.
// Engines are grouped by physical adapter (phys_N); utilization is summed
// per adapter and clamped to 0..100. Absent category => no GPUs reported.
public sealed class GpuProvider : IDisposable
{
    private static readonly Regex PhysIndex = new(@"phys_(\d+)", RegexOptions.Compiled);

    private readonly List<(PerformanceCounter Counter, int Phys)> _engines = new();
    private readonly List<string> _names = new();
    private bool _disposed;

    public GpuProvider()
    {
        try
        {
            var cat = new PerformanceCounterCategory("GPU Engine");
            foreach (var inst in cat.GetInstanceNames())
            {
                var m = PhysIndex.Match(inst);
                if (!m.Success) continue;
                try
                {
                    var c = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, true);
                    c.NextValue(); // warmup
                    _engines.Add((c, int.Parse(m.Groups[1].Value)));
                }
                catch { /* skip unreadable engines */ }
            }
            _names.AddRange(ReadAdapterNames());
        }
        catch { /* category missing on this machine */ }
    }

    public IReadOnlyList<GpuReading> Sample()
    {
        if (_engines.Count == 0) return Array.Empty<GpuReading>();
        try
        {
            var perPhys = new Dictionary<int, double>();
            foreach (var (c, phys) in _engines)
            {
                double v;
                try { v = Math.Clamp(c.NextValue(), 0, 100); }
                catch { continue; }
                perPhys[phys] = perPhys.GetValueOrDefault(phys) + v;
            }
            return perPhys.OrderBy(kv => kv.Key).Select(kv =>
            {
                string name = kv.Key < _names.Count ? _names[kv.Key] : $"GPU {kv.Key}";
                return new GpuReading(
                    name,
                    Reading<double>.Ok(Math.Clamp(kv.Value, 0, 100)),
                    Reading<double>.Unavailable("per-adapter VRAM not exposed"));
            }).ToList();
        }
        catch
        {
            return Array.Empty<GpuReading>();
        }
    }

    private static List<string> ReadAdapterNames()
    {
        var names = new List<string>();
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var o in s.Get())
                names.Add(o["Name"]?.ToString()?.Trim() ?? "");
        }
        catch { /* names stay empty; we fall back to "GPU N" */ }
        return names;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (c, _) in _engines) c.Dispose();
    }
}
