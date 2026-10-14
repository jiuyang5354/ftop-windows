using System.Diagnostics;
using Ftop.Core;

namespace Ftop.Sensors;

// Aggregate throughput across physical network interfaces (loopback excluded),
// as deltas between ticks — the same idea as ftop's 64-bit interface counters.
public sealed class NetworkProvider : IDisposable
{
    private readonly List<(PerformanceCounter Down, PerformanceCounter Up)> _ifs = new();
    private bool _disposed;

    public NetworkProvider()
    {
        try
        {
            var cat = new PerformanceCounterCategory("Network Interface");
            foreach (var inst in cat.GetInstanceNames())
            {
                if (inst.Contains("Loopback", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var down = new PerformanceCounter("Network Interface", "Bytes Received/sec", inst, true);
                    var up = new PerformanceCounter("Network Interface", "Bytes Sent/sec", inst, true);
                    down.NextValue(); up.NextValue(); // warmup
                    _ifs.Add((down, up));
                }
                catch { /* skip interfaces we cannot read */ }
            }
        }
        catch { /* category missing */ }
    }

    public NetworkReading Sample()
    {
        if (_ifs.Count == 0)
        {
            var r = Reading<double>.Unavailable("no network interfaces found");
            return new NetworkReading(r, r);
        }
        try
        {
            double down = 0, up = 0;
            foreach (var (d, u) in _ifs)
            {
                down += Math.Max(0, d.NextValue());
                up += Math.Max(0, u.NextValue());
            }
            return new NetworkReading(Reading<double>.Ok(down), Reading<double>.Ok(up));
        }
        catch (Exception ex)
        {
            var r = Reading<double>.Unavailable(ex.Message);
            return new NetworkReading(r, r);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (d, u) in _ifs) { d.Dispose(); u.Dispose(); }
    }
}
