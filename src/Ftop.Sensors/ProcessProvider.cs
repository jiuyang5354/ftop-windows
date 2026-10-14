using System.Diagnostics;
using Ftop.Core;

namespace Ftop.Sensors;

// Top processes by CPU share of the whole machine (same unit as the total,
// so it never passes 100% — mirroring ftop's convention).
// CPU% comes from TotalProcessorTime deltas between ticks.
public sealed class ProcessProvider
{
    private readonly Dictionary<int, (TimeSpan Cpu, DateTime Stamp)> _prev = new();

    public IReadOnlyList<ProcessReading> SampleTop(int count)
    {
        var now = DateTime.UtcNow;
        var rows = new List<ProcessReading>();
        var seen = new HashSet<int>();

        foreach (var p in Process.GetProcesses())
        {
            int pid;
            try
            {
                pid = p.Id;
                seen.Add(pid);
                var cpu = p.TotalProcessorTime;
                string name = p.ProcessName;
                double mem = 0;
                try { mem = p.WorkingSet64; } catch { }

                Reading<double> cpuR;
                if (_prev.TryGetValue(pid, out var prev))
                {
                    double wallMs = (now - prev.Stamp).TotalMilliseconds;
                    double cpuMs = (cpu - prev.Cpu).TotalMilliseconds;
                    double pct = wallMs > 0
                        ? Math.Clamp(cpuMs / wallMs / Environment.ProcessorCount * 100.0, 0, 100)
                        : 0;
                    cpuR = Reading<double>.Ok(pct);
                }
                else
                {
                    cpuR = Reading<double>.Ok(0); // first sighting: no delta yet
                }
                _prev[pid] = (cpu, now);
                rows.Add(new ProcessReading(pid, name, cpuR,
                    mem > 0 ? Reading<double>.Ok(mem) : Reading<double>.Unavailable("n/a")));
            }
            catch
            {
                // Process exited or access denied: skip it this tick.
            }
            finally
            {
                try { p.Dispose(); } catch { }
            }
        }

        // Drop pids that vanished so the map does not grow forever.
        foreach (var pid in _prev.Keys.Where(k => !seen.Contains(k)).ToList())
            _prev.Remove(pid);

        return rows.OrderByDescending(r => r.CpuPercent.IsAvailable ? r.CpuPercent.Value : 0)
                   .Take(count)
                   .ToList();
    }
}
