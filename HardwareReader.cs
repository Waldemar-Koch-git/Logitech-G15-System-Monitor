using System.Diagnostics;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;

namespace G15Dashboard;

internal sealed class HardwareReader : IDisposable
{
    private readonly PerformanceCounter? _cpu;
    private readonly Computer _computer = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsMotherboardEnabled = true };
    private readonly UpdateVisitor _visitor = new();
    private readonly List<SensorValue> _sensors = new();
    private string _openError = "none";
    private string _updateError = "none";
    private readonly string _cpuError = "none";

    public HardwareReader()
    {
        try
        {
            _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ = _cpu.NextValue();
        }
        catch (Exception ex) { _cpuError = ex.Message; _cpu?.Dispose(); _cpu = null; }
        try { _computer.Open(); } catch (Exception ex) { _openError = ex.GetType().Name + ": " + ex.Message; }
    }

    public void Dispose()
    {
        try { _computer.Close(); } catch { }
        try { _cpu?.Dispose(); } catch { }
    }

    public Stats Read()
    {
        double cpuLoad = SafeRead(_cpu);
        var (ramUsed, ramTotal) = PhysicalMemory.Read();
        var stats = new Stats(cpuLoad, -1, -1, -1, ramUsed, ramTotal, -1, -1);
        try
        {
            _computer.Accept(_visitor);
            _sensors.Clear();
            AddSensors(_computer.Hardware, _sensors);
            stats = ApplySensors(stats, _sensors);
            _updateError = "none";
        }
        catch (Exception ex) { _updateError = ex.GetType().Name + ": " + ex.Message; }
        return stats;
    }

    public string GetDiagnostics()
    {
        string pawnIoInstalled = SafePawnIoStatus(() => PawnIo.IsInstalled);
        var lines = new List<string>
        {
            $"Administrator: {Program.IsAdministrator()}",
            $"PawnIO installed: {pawnIoInstalled}",
            $"PawnIO version: {SafePawnIoStatus(() => PawnIo.Version?.ToString() ?? "none")}",
            $"Computer.Open: {_openError}",
            $"CPU counter initialization: {_cpuError}",
            $"Sensor update: {_updateError}"
        };
        if (pawnIoInstalled == bool.FalseString)
            lines.Add("CPU-Temperatur nicht verfügbar: PawnIO-Treiber fehlt. Nach der Installation die App neu starten.");
        AddHardwareDiagnostics(_computer.Hardware, lines);
        return string.Join(Environment.NewLine, lines);
    }

    private static string SafePawnIoStatus<T>(Func<T> read)
    {
        try { return read()?.ToString() ?? "none"; }
        catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
    }

    private static void AddHardwareDiagnostics(IEnumerable<IHardware> hardware, List<string> lines)
    {
        foreach (IHardware item in hardware)
        {
            lines.Add($"[{item.HardwareType}] {item.Name}");
            foreach (ISensor sensor in item.Sensors.Where(s => s.SensorType == SensorType.Temperature))
                lines.Add($"  {sensor.Name}: {sensor.Value?.ToString("0.0") ?? "null"} °C");
            AddHardwareDiagnostics(item.SubHardware, lines);
        }
    }

    private static void AddSensors(IEnumerable<IHardware> hardware, List<SensorValue> result)
    {
        foreach (IHardware item in hardware)
        {
            string parent = item.HardwareType.ToString();
            string hardwareId = item.Identifier.ToString();
            foreach (ISensor sensor in item.Sensors)
                if (sensor.Value is float value && float.IsFinite(value))
                    result.Add(new SensorValue(sensor.Name, parent, sensor.SensorType.ToString(), value, hardwareId));
            AddSensors(item.SubHardware, result);
        }
    }

    // Select all metrics in one pass; first matches and CPU priorities are stable.
    internal static Stats ApplySensors(Stats stats, List<SensorValue> sensors)
    {
        SensorValue? cpu = null, gpuTemp = null, gpuCore = null, gpuD3d = null;
        SensorValue? usedSmall = null, usedData = null, totalSmall = null, totalData = null;
        int cpuPriority = int.MaxValue;
        string? gpuId = null;
        // This method does not mutate the list; read structs without copying each one.
        foreach (ref readonly var sensor in CollectionsMarshal.AsSpan(sensors))
        {
            if (!double.IsFinite(sensor.Value) || sensor.Value < 0) continue;
            if (Is(sensor.Parent, "Cpu") && Is(sensor.Type, "Temperature"))
            {
                int priority = CpuTemperaturePriority(sensor.Name);
                if (priority < cpuPriority) { cpu = sensor; cpuPriority = priority; }
            }
            if (!sensor.Parent.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase)) continue;
            gpuId ??= sensor.HardwareId;
            if (sensor.HardwareId != gpuId) continue;

            if (Is(sensor.Type, "Temperature"))
            {
                if (Has(sensor.Name, "GPU Core") || Has(sensor.Name, "GPU Edge") || Has(sensor.Name, "GPU Temperature"))
                    gpuTemp ??= sensor;
            }
            else if (Is(sensor.Type, "Load"))
            {
                if (Has(sensor.Name, "GPU Core")) gpuCore ??= sensor;
                if (Has(sensor.Name, "D3D") && sensor.Value > (gpuD3d?.Value ?? -1)) gpuD3d = sensor;
            }
            else if (Is(sensor.Type, "SmallData") || Is(sensor.Type, "Data"))
            {
                bool small = Is(sensor.Type, "SmallData");
                if (Has(sensor.Name, "GPU Memory Used") || Has(sensor.Name, "GPU Memory Allocated")
                    || Has(sensor.Name, "D3D Dedicated Memory Used"))
                {
                    if (small) usedSmall ??= sensor;
                    else usedData ??= sensor;
                }
                if (Has(sensor.Name, "GPU Memory Total"))
                {
                    if (small) totalSmall ??= sensor;
                    else totalData ??= sensor;
                }
            }
        }
        return stats with
        {
            CpuTemp = cpu?.Value ?? stats.CpuTemp,
            GpuTemp = gpuTemp?.Value ?? stats.GpuTemp,
            GpuLoad = (gpuCore ?? gpuD3d)?.Value ?? stats.GpuLoad,
            VramUsed = ToGb(usedSmall ?? usedData, stats.VramUsed),
            VramTotal = ToGb(totalSmall ?? totalData, stats.VramTotal)
        };
    }

    private static int CpuTemperaturePriority(string name) =>
        Has(name, "Tctl/Tdie") ? 0 : Has(name, "CPU Package") ? 1 : Has(name, "Tctl") ? 2
        : Has(name, "Tdie") ? 3 : Has(name, "CPU Die") ? 4 : Is(name, "Core Max") ? 5 : int.MaxValue;

    private static double ToGb(SensorValue? sensor, double unavailable) => sensor is { } value
        ? Is(value.Type, "SmallData") ? value.Value / 1024d : value.Value : unavailable;

    private static bool Is(string value, string expected) => value.Equals(expected, StringComparison.OrdinalIgnoreCase);
    private static bool Has(string value, string part) => value.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static double SafeRead(PerformanceCounter? c)
    {
        try
        {
            float value = c?.NextValue() ?? float.NaN;
            return float.IsFinite(value) ? Math.Clamp(value, 0, 100) : -1;
        }
        catch { return -1; }
    }
}

internal sealed class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);
    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
    }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}

internal readonly record struct SensorValue(string Name, string Parent, string Type, double Value, string HardwareId = "");

internal record Stats(double CpuLoad, double GpuLoad, double CpuTemp, double GpuTemp,
    double RamUsed, double RamTotal, double VramUsed, double VramTotal);



