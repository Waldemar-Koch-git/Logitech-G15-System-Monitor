using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using G15Dashboard;

// Optional synthetic comparison. Never instantiates hardware readers or starts the app.
internal static class OptimizationBenchmark
{
    internal static void Run(string baselinePath, string reportPath)
    {
        var baseline = Assembly.LoadFile(Path.GetFullPath(baselinePath));
        var current = typeof(HardwareReader).Assembly;
        var measurements = new List<object>();
        foreach (var (name, assembly) in new[] { ("before", baseline), ("after", current) })
        {
            var sensorType = assembly.GetType("G15Dashboard.SensorValue", true)!;
            var statsType = assembly.GetType("G15Dashboard.Stats", true)!;
            var sensors = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(sensorType))!;
            void Add(string label, string parent, string type, double value) =>
                sensors.Add(Activator.CreateInstance(sensorType, label, parent, type, value, "device"));
            Add("CPU Package", "Cpu", "Temperature", 60);
            Add("CPU (Tctl/Tdie)", "Cpu", "Temperature", 65);
            Add("GPU Core", "GpuAmd", "Load", 50);
            Add("GPU Core", "GpuAmd", "Temperature", 70);
            Add("GPU Memory Used", "GpuAmd", "SmallData", 2048);
            Add("GPU Memory Total", "GpuAmd", "SmallData", 8192);
            for (int i = 0; i < 122; i++) Add("Unused sensor " + i, "Motherboard", "Voltage", 1);
            var stats = Activator.CreateInstance(statsType, 42d, -1d, -1d, -1d, 12d, 32d, -1d, -1d)!;
            var map = assembly.GetType("G15Dashboard.HardwareReader", true)!
                .GetMethod("ApplySensors", BindingFlags.NonPublic | BindingFlags.Static)!;
            object?[] mapArgs = [stats, sensors];
            var mapped = map.Invoke(null, mapArgs)!;
            foreach (var (property, expected) in new[] { ("CpuTemp", 65d), ("GpuLoad", 50d), ("GpuTemp", 70d), ("VramUsed", 2d), ("VramTotal", 8d) })
                if ((double)statsType.GetProperty(property)!.GetValue(mapped)! != expected)
                    throw new Exception("Benchmark inputs produced unexpected metrics.");
            measurements.Add(Measure(name, "sensor mapping, 128 sensors", 30_000, () => map.Invoke(null, mapArgs)));

            if (name == "before")
            {
                var render = assembly.GetType("G15Dashboard.DashboardForm", true)!
                    .GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Static)!;
                object?[] renderArgs = [stats, 144d];
                measurements.Add(Measure(name, "frame rendering", 3000, () => ((Bitmap)render.Invoke(null, renderArgs)!).Dispose()));
                using var previous = (Bitmap)render.Invoke(null, renderArgs)!;
                using var next = DashboardRenderer.Render(new Stats(42, -1, -1, -1, 12, 32, -1, -1), 144);
                // Exclude the clock row, which may advance between calls.
                for (int y = 8; y < 43; y++)
                    for (int x = 0; x < 160; x++)
                        if (previous.GetPixel(x, y) != next.GetPixel(x, y))
                            throw new Exception($"Render regression at {x},{y}.");
            }
            else
            {
                using var frame = new Bitmap(160, 43, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                var render = typeof(DashboardRenderer).GetMethod("RenderInto", BindingFlags.NonPublic | BindingFlags.Static)!;
                object?[] renderArgs = [frame, stats, 144d, null];
                measurements.Add(Measure(name, "frame rendering", 3000, () => render.Invoke(null, renderArgs)));
            }
        }
        var report = JsonSerializer.Serialize(new
        {
            scope = "Synthetic in-process comparison including reflection overhead; not total application performance or hardware IO.",
            baselineSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(baselinePath))),
            currentSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(current.Location))),
            measurements
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(reportPath, report);
        Console.WriteLine(report);
    }

    private static object Measure(string version, string operation, int iterations, Action action)
    {
        for (int i = 0; i < 300; i++) action();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) action();
        timer.Stop();
        return new { version, operation, iterations, milliseconds = timer.Elapsed.TotalMilliseconds,
            allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated };
    }
}
