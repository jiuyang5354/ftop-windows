using Ftop.Core;

namespace Ftop.Sensors;

// One sampler on a coalescing timer produces an immutable Snapshot per tick.
// A failing provider degrades its own module to "unavailable" and never
// blanks the panel. Pause() stops sampling (hidden/occluded window).
public sealed class Sampler : IDisposable
{
    private readonly CpuProvider _cpu = new();
    private readonly MemoryProvider _mem = new();
    private readonly NetworkProvider _net = new();
    private readonly GpuProvider _gpu = new();
    private readonly ProcessProvider _procs = new();

    private Timer? _timer;
    private int _refreshMs;
    private int _processRows = 8;
    private bool _disposed;

    public event Action<Snapshot>? SnapshotReady;

    public int RefreshMs
    {
        get => _refreshMs;
        set
        {
            _refreshMs = Math.Clamp(value, 250, 5000);
            _timer?.Change(0, _refreshMs);
        }
    }

    public int ProcessRows
    {
        get => _processRows;
        set => _processRows = Math.Clamp(value, 0, 24);
    }

    public Sampler(int refreshMs = 1000)
    {
        _refreshMs = Math.Clamp(refreshMs, 250, 5000);
    }

    public void Start() => _timer ??= new Timer(_ => Tick(), null, 0, _refreshMs);

    public void Pause() => _timer?.Change(Timeout.Infinite, Timeout.Infinite);

    public void Resume() => _timer?.Change(0, _refreshMs);

    private void Tick()
    {
        if (_disposed) return;
        try
        {
            var takenAt = DateTimeOffset.Now;
            IReadOnlyList<CoreReading> cores;
            try { cores = _cpu.Sample(); }
            catch { cores = Array.Empty<CoreReading>(); }

            // Power and temperature have no portable unprivileged API on
            // Windows; report unavailable instead of inventing numbers.
            var snap = new Snapshot(
                takenAt,
                cores,
                _mem.Sample(),
                _net.Sample(),
                _gpu.Sample(),
                Reading<double>.Unavailable("no portable API on Windows"),
                Reading<double>.Unavailable("no portable API on Windows"),
                _procs.SampleTop(_processRows));

            SnapshotReady?.Invoke(snap);
        }
        catch
        {
            // Never let the timer die on an unexpected error.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _cpu.Dispose();
        _mem.Dispose();
        _net.Dispose();
        _gpu.Dispose();
    }
}
