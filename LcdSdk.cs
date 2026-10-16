using System.Reflection;
using System.Runtime.InteropServices;

namespace G15Dashboard;

internal sealed class LcdSdk
{
    private const int Monochrome = 1;
    private bool _initialized;
    private long _nextConnectAttempt;
    private readonly byte[] _pixels = new byte[LcdDimensions.Width * LcdDimensions.Height];
    private readonly byte[] _row = new byte[LcdDimensions.Width * 3];
    public string Status { get; private set; } = "LCD-SDK nicht gefunden";

    static LcdSdk()
    {
        NativeLibrary.SetDllImportResolver(typeof(LcdSdk).Assembly, ResolveLgsDll);
    }

    private static IntPtr ResolveLgsDll(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals("LogitechLcd", StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
        var roots = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);
        string localDll = Path.Combine(AppContext.BaseDirectory, "LogitechLcd.dll");
        if (File.Exists(localDll) && NativeLibrary.TryLoad(localDll, out var localHandle)) return localHandle;
        foreach (var root in roots)
        {
            foreach (string path in new[] {
                Path.Combine(root, "Logitech Gaming Software", "LogitechLcd.dll"),
                Path.Combine(root, "Logitech Gaming Software", "LCDSDK", "LogitechLcd.dll"),
                Path.Combine(root, "Logitech Gaming Software", "LCDSDK_8.57.148", "Lib", "GameEnginesWrapper", "x64", "LogitechLcd.dll"),
                Path.Combine(root, "Logitech", "Logitech Gaming Software", "LogitechLcd.dll"),
            })
                if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle)) return handle;
        }
        // Search subdirectories only when the known locations did not contain a usable DLL.
        foreach (var root in roots)
        {
            try
            {
                var lgs = Path.Combine(root, "Logitech Gaming Software");
                if (!Directory.Exists(lgs)) continue;
                foreach (string path in Directory.EnumerateFiles(lgs, "LogitechLcd.dll", SearchOption.AllDirectories))
                    if (NativeLibrary.TryLoad(path, out var handle)) return handle;
            }
            catch { }
        }
        return IntPtr.Zero;
    }

    [DllImport("LogitechLcd", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiLcdInit(string appletName, int lcdType);
    [DllImport("LogitechLcd", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiLcdIsConnected(int lcdType);
    [DllImport("LogitechLcd", CallingConvention = CallingConvention.Cdecl)]
    private static extern void LogiLcdUpdate();
    [DllImport("LogitechLcd", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiLcdMonoSetBackground(byte[] bitmap);
    [DllImport("LogitechLcd", CallingConvention = CallingConvention.Cdecl)]
    private static extern void LogiLcdShutdown();

    public bool Initialize(string appletName)
    {
        _nextConnectAttempt = Environment.TickCount64 + 5000;
        try
        {
            _initialized = LogiLcdInit(appletName, Monochrome);
            if (!_initialized) { Status = "LGS hat LCD-App nicht initialisiert"; return false; }
            if (!LogiLcdIsConnected(Monochrome)) { Status = "G15 LCD nicht verbunden"; return false; }
            Status = "G15 LCD aktiv";
            return true;
        }
        catch (Exception ex) when (IsLibraryError(ex)) { HandleLibraryError(ex); return false; }
    }

    public bool EnsureConnected(string appletName)
    {
        if (_initialized)
        {
            try
            {
                if (LogiLcdIsConnected(Monochrome)) return true;
                Status = "G15 LCD nicht verbunden";
            }
            catch (Exception ex) when (IsLibraryError(ex)) { HandleLibraryError(ex); }
        }
        if (Environment.TickCount64 < _nextConnectAttempt) return false;
        Shutdown();
        return Initialize(appletName);
    }

    public void Update(Bitmap bitmap)
    {
        if (!_initialized) return;
        try
        {
            CopyMonochrome(bitmap, _pixels, _row);
            if (!LogiLcdMonoSetBackground(_pixels)) { Status = "LCD-Bild wurde abgelehnt"; return; }
            LogiLcdUpdate();
            Status = "G15 LCD aktiv";
        }
        catch (Exception ex) { Status = "LCD-Update: " + ex.Message; }
    }

    internal static void CopyMonochrome(Bitmap bitmap, byte[] pixels, byte[] row)
    {
        if (bitmap.Width != LcdDimensions.Width || bitmap.Height != LcdDimensions.Height)
            throw new ArgumentException("Das LCD-Bild muss 160 × 43 Pixel groß sein.", nameof(bitmap));
        if (pixels.Length < bitmap.Width * bitmap.Height || row.Length < bitmap.Width * 3)
            throw new ArgumentException("Der Bildpuffer ist zu klein.");
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, bitmap.Width * 3);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int offset = x * 3;
                    int max = Math.Max(row[offset], Math.Max(row[offset + 1], row[offset + 2]));
                    int min = Math.Min(row[offset], Math.Min(row[offset + 1], row[offset + 2]));
                    // Preserve the original Color.GetBrightness() > 0.5 threshold.
                    pixels[y * bitmap.Width + x] = max + min > 255 ? (byte)255 : (byte)0;
                }
            }
        }
        finally { bitmap.UnlockBits(data); }
    }

    public void Shutdown()
    {
        if (!_initialized) return;
        try { LogiLcdShutdown(); } catch { }
        finally { _initialized = false; }
    }

    private static bool IsLibraryError(Exception ex) =>
        ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;

    private void HandleLibraryError(Exception ex)
    {
        Shutdown();
        Status = ex switch
        {
            DllNotFoundException => "LogitechLcd.dll fehlt (siehe README)",
            EntryPointNotFoundException => "LCD-SDK inkompatibel",
            _ => "LCD-SDK hat die falsche Architektur (x64 erforderlich)"
        };
    }
}

