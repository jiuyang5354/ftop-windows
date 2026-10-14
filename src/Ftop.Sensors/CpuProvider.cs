using System.Diagnostics;
using Ftop.Core;
using Microsoft.Win32;

namespace Ftop.Sensors;

// Per-core CPU usage and (where exposed) current frequency.
// Usage: PerformanceCounter "% Processor Time" per core instance.
// Frequency: "% Processor Performance" x max MHz from the registry,
// the standard portable approximation on Windows.
public sealed class CpuProvider : IDisposable
{
    private readonly List<(PerformanceCounter Usage, PerformanceCounter? Perf)> _cores = new();
    private readonly double _maxMhz;
    private bool _disposed;

    public int CoreCount => _cores.Count;

    public CpuProvider()
    {
        _maxMhz = ReadMaxMhz();
        try
        {
            var cat = new PerformanceCounterCategory("Processor");
            foreach (var inst in cat.GetInstanceNames())
            {
                if (!int.TryParse(inst, out _)) continue; // skip _Total
                var usage = new PerformanceCounter("Processor", "% Processor Time", inst, true);
                PerformanceCounter? perf = null;
                try { perf = new PerformanceCounter("Processor", "% Processor Performance", inst, true); }
                catch { /* frequency simply unavailable for this core */ }
                // Warmup: the first NextValue() is always 0 / stale.
                try { usage.NextValue(); perf?.NextValue(); } catch { }
                _cores.Add((usage, perf));
            }
        }
        catch
        {
            // Category missing: CoreCount stays 0, sampler reports unavailable.
        }
    }

    public IReadOnlyList<CoreReading> Sample()
    {
        var list = new List<CoreReading>(_cores.Count);
        for (int i = 0; i < _cores.Count; i++)
        {
            var (usage, perf) = _cores[i];
            Reading<double> u, f;
            try
            {
                u = Reading<double>.Ok(Math.Clamp(usage.NextValue(), 0, 100));
            }
            catch (Exception ex) { u = Reading<double>.Unavailable(ex.Message); }

            if (perf is not null && _maxMhz > 0)
            {
                try
                {
                    var p = Math.Clamp(perf.NextValue(), 0, 100);
                    f = Reading<double>.Ok(p / 100.0 * _maxMhz);
                }
                catch (Exception ex) { f = Reading<double>.Unavailable(ex.Message); }
            }
            else
            {
                f = Reading<double>.Unavailable("frequency not exposed");
            }

            // Windows has no portable P/E cluster API; single group.
            list.Add(new CoreReading(i, u, f, 0));
        }
        return list;
    }

    private static double ReadMaxMhz()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var v = key?.GetValue("~MHz");
            return v is int mhz ? mhz : 0;
        }
        catch { return 0; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (u, p) in _cores) { u.Dispose(); p?.Dispose(); }
    }
}
