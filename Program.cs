using System.Diagnostics;
using System.Security.Principal;

namespace G15Dashboard;

internal static class LcdDimensions
{
    public const int Width = 160;
    public const int Height = 43;
}

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        const string instanceName = @"Local\G15SystemMonitor-1";
        if (args.Contains("--shutdown", StringComparer.OrdinalIgnoreCase))
        {
            if (EventWaitHandle.TryOpenExisting(instanceName + "-stop", out var signal))
                using (signal) signal.Set();
            return;
        }
        if (!IsAdministrator())
        {
            try
            {
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
                // A framework-dependent launch may be hosted by dotnet.exe.
                if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
                    start.ArgumentList.Add(typeof(Program).Assembly.Location);
                using var elevated = Process.Start(start);
                return;
            }
            catch (System.ComponentModel.Win32Exception) { }
        }
        using var instance = new Mutex(true, instanceName, out bool firstInstance);
        if (!firstInstance) return;
        using var stop = new EventWaitHandle(false, EventResetMode.AutoReset, instanceName + "-stop");
        ApplicationConfiguration.Initialize();
        using var iconStream = typeof(Program).Assembly.GetManifestResourceStream("G15Dashboard.AppIcon")
            ?? throw new InvalidOperationException("Application icon resource is missing.");
        using var appIcon = new Icon(iconStream);
        using var dashboard = new DashboardForm(appIcon);
        _ = dashboard.Handle;
        var stopRegistration = ThreadPool.RegisterWaitForSingleObject(stop, (_, _) =>
        {
            try { dashboard.BeginInvoke(new Action(dashboard.Close)); }
            catch (InvalidOperationException) { }
        }, null, Timeout.Infinite, true);
        try { Application.Run(dashboard); }
        finally { stopRegistration.Unregister(null); }
    }

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

internal sealed class DashboardForm : Form
{
    private const int WidthPx = LcdDimensions.Width;
    private const int HeightPx = LcdDimensions.Height;
    private const int PreviewScale = 4;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly HardwareReader _hardware = new();
    private readonly LcdSdk _lcd = new();
    private readonly FpsReader _fps = new();
    private readonly NotifyIcon _tray = new();
    private bool _previewRequested;
    private readonly Bitmap _frame = new(WidthPx, HeightPx, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
    private const string Title = "G15 System Monitor";
    private bool _lcdReady;

    public DashboardForm(Icon appIcon)
    {
        Icon = appIcon;
        Text = Title;
        ClientSize = new Size(WidthPx * PreviewScale, HeightPx * PreviewScale);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = true;
        DoubleBuffered = true;
        KeyPreview = true;
        BackColor = Color.Black;
        ForeColor = Color.White;
        _lcdReady = _lcd.Initialize(Title);
        Text = $"{Title} — {_lcd.Status}";
        var menu = new ContextMenuStrip();
        menu.Items.Add("Vorschau anzeigen", null, (_, _) => { _previewRequested = true; ShowInTaskbar = true; Show(); Activate(); });
        menu.Items.Add("Vorschau ausblenden", null, (_, _) => { if (_lcdReady) { _previewRequested = false; Hide(); ShowInTaskbar = false; } });
        menu.Items.Add("Sensor-Diagnose", null, (_, _) => ShowDiagnostics());
        menu.Items.Add("Beenden", null, (_, _) => Close());
        _tray.Icon = appIcon;
        _tray.Text = Title;
        _tray.ContextMenuStrip = menu;
        _tray.Visible = true;
        ShowInTaskbar = !_lcdReady;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F2)
                ShowDiagnostics();
        };
        _timer.Tick += (_, _) => RefreshDashboard();
        _timer.Start();
        RefreshDashboard();
    }

    private void ShowDiagnostics() => MessageBox.Show(this,
        _hardware.GetDiagnostics() + Environment.NewLine + _fps.Status + Environment.NewLine + _lcd.Status,
        "Sensor diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);

    protected override void SetVisibleCore(bool value)
    {
        if (value && _lcdReady && !_previewRequested)
        {
            if (!IsHandleCreated) CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        e.Graphics.DrawImage(_frame, ClientRectangle);
    }

    private void RefreshDashboard()
    {
        bool connected = _lcd.EnsureConnected(Title);
        if (connected != _lcdReady)
        {
            _lcdReady = connected;
            ShowInTaskbar = !_lcdReady || _previewRequested;
            if (_lcdReady && !_previewRequested) Hide();
            else Show();
        }
        DashboardRenderer.RenderInto(_frame, _hardware.Read(), _fps.Read());
        if (_lcdReady) _lcd.Update(_frame);
        Text = $"{Title} — {_lcd.Status}";
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            _timer.Dispose();
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _fps.Dispose();
            _hardware.Dispose();
            _lcd.Shutdown();
            _frame.Dispose();
        }
        base.Dispose(disposing);
    }

}

