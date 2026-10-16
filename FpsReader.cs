using System.Diagnostics;
using System.Runtime.InteropServices;

namespace G15Dashboard;

// Displayed frames of the foreground process, averaged over the latest second.
internal sealed class FpsReader : IDisposable
{
    private readonly object _gate = new();
    private readonly FpsWindow _window = new();
    private readonly Process? _capture;
    private readonly Task? _consumer;
    private bool _disposed;
    private bool _captureEnded;
    private uint _target;
    private string _status = "FPS: warte auf aktives Programm";
    public string Status { get { lock (_gate) return _status; } }

    public FpsReader()
    {
        try
        {
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "PresentMon.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string arg in new[] { "--output_stdout", "--no_console_stats", "--v1_metrics",
                "--exclude_dropped", "--no_track_gpu", "--no_track_input",
                "--session_name", $"G15Dashboard-{Environment.ProcessId}" })
                start.ArgumentList.Add(arg);
            _capture = Process.Start(start) ?? throw new InvalidOperationException("PresentMon startet nicht.");
            _capture.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    lock (_gate) _status = "PresentMon: " + e.Data;
            };
            _capture.BeginErrorReadLine();
            _consumer = Task.Run(Consume);
        }
        catch (Exception ex)
        {
            _status = "FPS nicht verfügbar: " + ex.Message;
            Dispose();
        }
    }

    private void Consume()
    {
        try
        {
            using var csv = new PresentMonCsvReader(_capture!.StandardOutput, hasDisplayTime =>
            {
                if (!hasDisplayTime)
                    lock (_gate) _status = "FPS: PresentMon liefert keine Display-Zeit";
            });
            while (csv.TryRead(out var sample))
            {
                lock (_gate)
                    if (_disposed || sample.ProcessId != _target || _target == (uint)Environment.ProcessId) continue;
                uint foreground = ForegroundProcessId();
                lock (_gate)
                {
                    if (_disposed || sample.ProcessId != _target || sample.ProcessId != foreground) continue;
                    _window.Add(sample.SwapChain, Environment.TickCount64, sample.Milliseconds);
                }
            }
            lock (_gate) _status = !csv.HasDisplayTime ? "FPS: PresentMon liefert keine Display-Zeit"
                : $"FPS: PresentMon beendet; fehlerhafte CSV-Zeilen: {csv.MalformedRows}. " + _status;
        }
        catch (Exception ex) { lock (_gate) _status = "FPS-Erfassung: " + ex.Message; }
        finally
        {
            lock (_gate) { _captureEnded = true; _window.Clear(); }
        }
    }

    public double Read()
    {
        uint foreground = ForegroundProcessId();
        lock (_gate)
        {
            if (_disposed || _captureEnded) return -1;
            if (_target != foreground) { _target = foreground; _window.Clear(); }
            double fps = _window.Read(Environment.TickCount64);
            if (fps >= 0) _status = $"FPS: Prozess {_target}, angezeigte Frames (1 s)";
            return fps;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _window.Clear();
        }
        if (_capture is null) return;
        try
        {
            if (!_capture.HasExited)
            {
                var stop = new ProcessStartInfo(_capture.StartInfo.FileName) { UseShellExecute = false, CreateNoWindow = true };
                foreach (string arg in new[] { "--session_name", $"G15Dashboard-{Environment.ProcessId}", "--terminate_existing_session" })
                    stop.ArgumentList.Add(arg);
                using var terminator = Process.Start(stop);
                _capture.WaitForExit(2000);
                if (terminator is not null && !terminator.WaitForExit(2000)) terminator.Kill();
            }
        }
        catch { }
        // Even a failed session terminator must not leave our capture process running.
        try
        {
            if (!_capture.HasExited) { _capture.Kill(); _capture.WaitForExit(2000); }
        }
        catch { }
        // Let the pipe reader finish before releasing the process streams.
        _consumer?.Wait(2000);
        _capture.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    private static uint ForegroundProcessId()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint processId);
        return processId;
    }
}
