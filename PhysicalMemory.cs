using System.Runtime.InteropServices;

namespace G15Dashboard;

internal static class PhysicalMemory
{
    // Windows returns physical memory in bytes; no WMI query or performance counter is needed.
    internal static (double UsedGb, double TotalGb) Read()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0) return (-1, -1);
        const double bytesPerGb = 1024d * 1024 * 1024;
        return ((status.TotalPhys - Math.Min(status.AvailPhys, status.TotalPhys)) / bytesPerGb,
            status.TotalPhys / bytesPerGb);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
