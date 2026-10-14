// Immutable snapshot produced by the sampler once per tick.
// The UI only ever renders a Snapshot; providers never throw into the UI.
namespace Ftop.Core;

public sealed record CoreReading(
    int Index,
    Reading<double> UsagePercent,   // 0..100
    Reading<double> FrequencyMhz,   // current frequency, if the OS exposes it
    int Group);                     // core group (P/E or NUMA-ish cluster), for coloring

public sealed record MemoryReading(
    Reading<double> UsedBytes,
    Reading<double> TotalBytes,
    Reading<double> PressurePercent); // 0..100, higher = more pressure

public sealed record NetworkReading(
    Reading<double> DownBytesPerSec,
    Reading<double> UpBytesPerSec);

public sealed record GpuReading(
    string Name,
    Reading<double> UsagePercent,   // 0..100
    Reading<double> MemoryUsedBytes);

public sealed record ProcessReading(
    int Pid,
    string Name,
    Reading<double> CpuPercent,     // share of the whole machine, 0..100
    Reading<double> MemoryBytes);

public sealed record Snapshot(
    DateTimeOffset TakenAt,
    IReadOnlyList<CoreReading> Cores,
    MemoryReading Memory,
    NetworkReading Network,
    IReadOnlyList<GpuReading> Gpus,
    Reading<double> PowerWatts,
    Reading<double> TemperatureC,
    IReadOnlyList<ProcessReading> Processes)
{
    public static Snapshot Empty(DateTimeOffset takenAt) => new(
        takenAt,
        Array.Empty<CoreReading>(),
        new MemoryReading(Reading<double>.Unavailable("no data"), Reading<double>.Unavailable("no data"), Reading<double>.Unavailable("no data")),
        new NetworkReading(Reading<double>.Unavailable("no data"), Reading<double>.Unavailable("no data")),
        Array.Empty<GpuReading>(),
        Reading<double>.Unavailable("no data"),
        Reading<double>.Unavailable("no data"),
        Array.Empty<ProcessReading>());
}
