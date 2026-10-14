using System.Diagnostics;
using System.Runtime.InteropServices;
using Ftop.Core;

namespace Ftop.Sensors;

public sealed class MemoryProvider : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private readonly PerformanceCounter? _availMb;
    private bool _disposed;

    public MemoryProvider()
    {
        try
        {
            _availMb = new PerformanceCounter("Memory", "Available MBytes", true);
            _availMb.NextValue(); // warmup
        }
        catch { /* fall back to GlobalMemoryStatusEx only */ }
    }

    public MemoryReading Sample()
    {
        try
        {
            var st = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref st) || st.ullTotalPhys == 0)
                return Unavailable("GlobalMemoryStatusEx failed");

            double total = st.ullTotalPhys;
            double avail = st.ullAvailPhys;
            double used = total - avail;
            // dwMemoryLoad is the OS's own pressure figure (0..100).
            double pressure = st.dwMemoryLoad;
            return new MemoryReading(
                Reading<double>.Ok(used),
                Reading<double>.Ok(total),
                Reading<double>.Ok(pressure));
        }
        catch (Exception ex)
        {
            return Unavailable(ex.Message);
        }
    }

    private static MemoryReading Unavailable(string reason)
    {
        var r = Reading<double>.Unavailable(reason);
        return new MemoryReading(r, r, r);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _availMb?.Dispose();
    }
}
