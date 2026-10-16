using System.Drawing.Imaging;
using G15Dashboard;

// Dependency-free regression runner. Does not start the dashboard, sensors, ETW or installers.
var checks = new (string Name, Action Run)[]
{
    ("CPU temperatures cannot come from motherboard or GPU sensors", () =>
    {
        var stats = ReadSensors(new("CPU Package", "Motherboard", "Temperature", 90),
            new("CPU (Tctl/Tdie)", "GpuAmd", "Temperature", 80),
            new("Core Max", "Cpu", "Temperature", 0));
        Equal(0, stats.CpuTemp);
        Equal(-1, ReadSensors(new SensorValue("CPU Package", "Motherboard", "Temperature", 90)).CpuTemp);
    }),
    ("Invalid first GPU readings do not hide the next valid GPU", () =>
    {
        var stats = ReadSensors(new("GPU Core", "GpuIntel", "Load", double.NaN, "/gpu/0"),
            new("GPU Core", "GpuAmd", "Load", 50, "/gpu/1"),
            new("GPU Memory Used", "GpuIntel", "SmallData", -1, "/gpu/0"),
            new("GPU Memory Total", "GpuAmd", "SmallData", 8192, "/gpu/1"));
        Equal(50, stats.GpuLoad);
        Equal(8, stats.VramTotal);
        Equal(-1, stats.VramUsed);
    }),
    ("CPU and VRAM priorities preserve first matches", () =>
    {
        var stats = ReadSensors(new("CPU Package", "Cpu", "Temperature", 55),
            new("CPU Package", "Cpu", "Temperature", 70),
            new("Core Max", "Cpu", "Temperature", 90),
            new("GPU Memory Used", "GpuAmd", "Data", 7),
            new("GPU Memory Used", "GpuAmd", "SmallData", 1024),
            new("GPU Memory Allocated", "GpuAmd", "SmallData", 2048));
        Equal(55, stats.CpuTemp);
        Equal(1, stats.VramUsed);
    }),
    ("Reused frame clears FPS and bars without changing layout", () =>
    {
        var now = new DateTime(2026, 10, 11, 12, 34, 56);
        using var frame = DashboardRenderer.Render(new Stats(100, 100, 100, 100, 8, 8, 4, 4), 144, now);
        DashboardRenderer.RenderInto(frame, new Stats(0, 0, -1, -1, 0, 8, 0, 4), -1, now);
        for (int y = 0; y < 24; y++)
            for (int x = 134; x < 158; x++) Equal(0, frame.GetPixel(x, y).R);
        foreach (int y in new[] { 12, 20, 28, 36 })
            for (int x = 51; x < 130; x++) Equal(0, frame.GetPixel(x, y).R);
        Equal(255, frame.GetPixel(84, 2).R);
        Equal(255, frame.GetPixel(96, 2).R);
    }),
    ("PresentMon CSV handles quoted fields, corrupt rows and repeated headers", () =>
    {
        using var csv = new PresentMonCsvReader(new StringReader(
            "Application,ProcessID,SwapChainAddress,msBetweenDisplayChange\n"
            + "\"game, sample.exe\",42,chain,10\n"
            + "\"broken\"x,42,chain,10\n"
            + "game,42,chain,NaN\ngame,42,chain,0\nshort,42\n"
            + "msBetweenDisplayChange,SwapChainAddress,ProcessID\n20,other,43\n"));
        if (!csv.TryRead(out var first) || first.ProcessId != 42 || first.SwapChain != "chain")
            throw new Exception("Quoted CSV fields lost.");
        Equal(10, first.Milliseconds);
        if (!csv.TryRead(out var second) || second.ProcessId != 43 || second.SwapChain != "other")
            throw new Exception("Capture did not recover after corrupt CSV.");
        Equal(20, second.Milliseconds);
        Equal(1, csv.MalformedRows);
        if (csv.TryRead(out _)) throw new Exception("Unexpected sample after EOF.");
    }),
    ("Missing CSV columns cannot produce FPS samples", () =>
    {
        foreach (string header in new[] { "ProcessID,SwapChainAddress", "ProcessID,msBetweenDisplayChange", "SwapChainAddress,msBetweenDisplayChange" })
        {
            bool? reported = null;
            using var csv = new PresentMonCsvReader(new StringReader(header + "\n42,10\n"), hasTime => reported = hasTime);
            if (csv.TryRead(out _)) throw new Exception("Incomplete header accepted.");
            if (reported != header.Contains("msBetweenDisplayChange")) throw new Exception("Missing header diagnostics.");
        }
    }),
    ("CPU and GPU bars show load independently of temperature numbers", () =>
    {
        foreach (double temperature in new[] { -1d, 10d, 20d, 60d, 100d, 110d })
        {
            using var bitmap = DashboardRenderer.Render(new Stats(100, 0, temperature, temperature, 2, 8, 1, 4), -1);
            for (int x = 51; x < 130; x++)
            {
                Equal(255, bitmap.GetPixel(x, 28).R);
                Equal(0, bitmap.GetPixel(x, 36).R);
            }
            Equal(255, bitmap.GetPixel(60, 28).R); // CPU load remains full.
            Equal(0, bitmap.GetPixel(60, 36).R); // GPU load remains empty.
            Equal(255, bitmap.GetPixel(60, 12).R); // RAM at 25 percent uses the same long bar.
            Equal(0, bitmap.GetPixel(100, 12).R);
        }
    }),
    ("All bars have equal length and enlarged FPS stays above the temperatures", () =>
    {
        using var bitmap = DashboardRenderer.Render(new Stats(100, 100, 100, 100, 8, 8, 4, 4), 888);
        foreach (int y in new[] { 12, 20, 28, 36 })
        {
            for (int x = 51; x < 130; x++) Equal(255, bitmap.GetPixel(x, y).R);
            Equal(0, bitmap.GetPixel(131, y).R);
        }
        // Colons in HH:mm:ss verify the clock is centered over the bars, in the first row.
        Equal(255, bitmap.GetPixel(84, 2).R);
        Equal(255, bitmap.GetPixel(96, 2).R);
        // A scaled 8 occupies the entire height of both memory rows (9 through 23).
        Equal(255, bitmap.GetPixel(136, 9).R);
        Equal(255, bitmap.GetPixel(136, 23).R);
        for (int x = 134; x < 158; x++) Equal(0, bitmap.GetPixel(x, 24).R);
        // Four-digit and unavailable FPS also fit the right column.
        foreach (double fps in new[] { 1234d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1e12 })
        {
            using var other = DashboardRenderer.Render(new Stats(0, 0, -1, -1, 0, 8, 0, 4), fps);
            for (int y = 9; y < 24; y++) Equal(0, other.GetPixel(133, y).R);
            if (!double.IsFinite(fps) || fps < 0)
                for (int y = 0; y < 24; y++)
                    for (int x = 134; x < 158; x++) Equal(0, other.GetPixel(x, y).R);
        }
        using var zero = DashboardRenderer.Render(new Stats(0, 0, -1, -1, 0, 8, 0, 4), 0);
        Equal(255, zero.GetPixel(143, 9).R); // A valid zero remains visible.
        Equal(255, zero.GetPixel(140, 1).R); // The FPS label is visible with a valid value.
    }),
    ("VRAM units follow each selected sensor", () =>
    {
        var stats = ReadSensors(new("GPU Memory Allocated", "GpuNvidia", "SmallData", 2048),
            new("GPU Memory Total", "GpuNvidia", "Data", 8));
        Equal(2, stats.VramUsed);
        Equal(8, stats.VramTotal);
        Equal(25, DashboardRenderer.Percentage(stats.VramUsed, stats.VramTotal));
    }),
    ("Data usage and SmallData capacity remain independent", () =>
    {
        var stats = ReadSensors(new("GPU Memory Used", "GpuNvidia", "Data", 2),
            new("GPU Memory Total", "GpuNvidia", "SmallData", 8192));
        Equal(2, stats.VramUsed);
        Equal(8, stats.VramTotal);
    }),
    ("Zero load and zero VRAM are valid", () =>
    {
        var stats = ReadSensors(new("GPU Core", "GpuNvidia", "Load", 0),
            new("GPU D3D", "GpuNvidia", "Load", 75),
            new("GPU Memory Used", "GpuNvidia", "Data", 0),
            new("GPU Memory Total", "GpuNvidia", "Data", 8));
        Equal(0, stats.GpuLoad);
        Equal(0, DashboardRenderer.Percentage(stats.VramUsed, stats.VramTotal));
    }),
    ("Missing and invalid readings stay unavailable", () =>
    {
        var stats = ReadSensors(new("GPU Core", "GpuNvidia", "Load", double.NaN),
            new("GPU Memory Total", "GpuNvidia", "Data", double.PositiveInfinity));
        Equal(-1, stats.GpuLoad);
        Equal(-1, stats.VramTotal);
        Equal(-1, DashboardRenderer.Percentage(stats.VramUsed, stats.VramTotal));
        Equal(-1, DashboardRenderer.Percentage(0, 0));
        Equal(-1, DashboardRenderer.Percentage(double.NaN, 8));
    }),
    ("GPU readings never mix different devices", () =>
    {
        var stats = ReadSensors(new("GPU Core", "GpuIntel", "Load", 10, "/gpu/0"),
            new("GPU Memory Used", "GpuIntel", "Data", 1, "/gpu/0"),
            new("GPU Memory Total", "GpuNvidia", "Data", 8, "/gpu/1"));
        Equal(10, stats.GpuLoad);
        Equal(-1, stats.VramTotal);
    }),
    ("CPU temperature priority is preserved", () =>
    {
        var stats = ReadSensors(new("CPU Package", "Cpu", "Temperature", 60),
            new("CPU (Tctl/Tdie)", "Cpu", "Temperature", 65));
        Equal(65, stats.CpuTemp);
    }),
    ("Intel iGPU D3D engines and CPU fallback are recognized", () =>
    {
        var stats = ReadSensors(new("Core Max", "Cpu", "Temperature", 55),
            new("D3D 3D", "GpuIntel", "Load", 35),
            new("D3D Copy", "GpuIntel", "Load", 0),
            new("D3D Video Decode", "GpuIntel", "Load", 70),
            new("D3D Shared Memory Used", "GpuIntel", "Data", 2),
            new("D3D Shared Memory Total", "GpuIntel", "Data", 16));
        Equal(55, stats.CpuTemp);
        Equal(70, stats.GpuLoad);
        Equal(-1, stats.VramTotal);
    }),
    ("FPS uses the most active chain and ignores invalid durations", () =>
    {
        var window = new FpsWindow();
        window.Add("a", 0, 20); window.Add("a", 100, 20);
        window.Add("b", 0, 10); window.Add("b", 100, 10); window.Add("b", 200, 10);
        window.Add("a", 200, double.NaN); window.Add("a", 200, 0);
        window.Add("a", 200, double.PositiveInfinity); window.Add("a", 200, -5);
        Equal(100, window.Read(200));
    }),
    ("FPS trims at the one-second boundary and resets on window changes", () =>
    {
        var window = new FpsWindow();
        window.Add("a", 0, 20); window.Add("a", 100, 20);
        Equal(50, window.Read(1000));
        Equal(-1, window.Read(1001));
        Equal(-1, window.Read(1101));
        window.Add("a", 1200, 10); window.Add("a", 1300, 10);
        Equal(100, window.Read(1300));
        window.Clear();
        Equal(-1, window.Read(1300));
    }),
    ("FPS rolling sums match a reference calculation", () =>
    {
        var window = new FpsWindow();
        var samples = new List<(long At, double Ms)>();
        for (int i = 0; i < 2000; i++)
        {
            long now = i * 17;
            double ms = 10 + i % 13;
            window.Add("a", now, ms);
            samples.Add((now, ms));
            var recent = samples.Where(s => now - s.At <= 1000).ToArray();
            Equal(recent.Length < 2 ? -1 : 1000d * recent.Length / recent.Sum(s => s.Ms), window.Read(now));
        }
    }),
    ("LCD bytes match the previous brightness conversion", () =>
    {
        using var bitmap = new Bitmap(160, 43, PixelFormat.Format24bppRgb);
        var random = new Random(1234);
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                bitmap.SetPixel(x, y, Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
        var bytes = new byte[160 * 43];
        LcdSdk.CopyMonochrome(bitmap, bytes, new byte[160 * 3]);
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                Equal(bitmap.GetPixel(x, y).GetBrightness() > .5f ? 255 : 0, bytes[y * 160 + x]);
        // A second conversion also verifies that LockBits was released.
        LcdSdk.CopyMonochrome(bitmap, bytes, new byte[160 * 3]);
    }),
    ("Windows physical memory API returns plausible values", () =>
    {
        var (used, total) = PhysicalMemory.Read();
        if (!(total > 0 && used >= 0 && used <= total)) throw new Exception("Invalid physical memory result.");
    })
};

int failed = 0;
foreach (var (name, run) in checks)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
Console.WriteLine($"{checks.Length - failed}/{checks.Length} checks passed.");
if (failed == 0 && args.Length == 3 && args[0] == "--benchmark")
    OptimizationBenchmark.Run(args[1], args[2]);
if (failed == 0 && args.Length == 2 && args[0] == "--preview")
{
    using var frame = DashboardRenderer.Render(new Stats(42, 76, 60, 75, 12, 32, 6, 8), 144);
    using var preview = new Bitmap(960, 258);
    using var graphics = Graphics.FromImage(preview);
    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
    graphics.DrawImage(frame, new Rectangle(0, 0, preview.Width, preview.Height));
    preview.Save(args[1], ImageFormat.Png);
}
return failed == 0 ? 0 : 1;

static Stats ReadSensors(params SensorValue[] sensors) =>
    HardwareReader.ApplySensors(new Stats(-1, -1, -1, -1, -1, -1, -1, -1), sensors.ToList());

static void Equal(double expected, double actual)
{
    if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 1e-8)
        throw new Exception($"Expected {expected}, got {actual}.");
}

