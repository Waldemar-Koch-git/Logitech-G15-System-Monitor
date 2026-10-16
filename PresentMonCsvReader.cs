using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace G15Dashboard;

// Retain the CSV parser's quoted-field handling; corrupt rows must not stop capture.
internal sealed class PresentMonCsvReader : IDisposable
{
    private readonly TextFieldParser _parser;
    private readonly Action<bool>? _headerRead;
    private int _pid = -1, _chain = -1, _duration = -1, _lastColumn = -1;
    internal bool HasDisplayTime { get; private set; }
    internal int MalformedRows { get; private set; }

    internal PresentMonCsvReader(TextReader reader, Action<bool>? headerRead = null)
    {
        _headerRead = headerRead;
        _parser = new TextFieldParser(reader);
        _parser.SetDelimiters(",");
    }

    internal bool TryRead(out PresentMonSample sample)
    {
        sample = default;
        while (!_parser.EndOfData)
        {
            string[]? fields;
            try { fields = _parser.ReadFields(); }
            catch (MalformedLineException) { MalformedRows++; continue; }
            if (fields is null) continue;
            int headerPid = Array.IndexOf(fields, "ProcessID");
            if (headerPid >= 0 || _lastColumn < 0)
            {
                _pid = headerPid;
                _chain = Array.IndexOf(fields, "SwapChainAddress");
                _duration = Array.IndexOf(fields, "msBetweenDisplayChange");
                HasDisplayTime = _duration >= 0;
                _headerRead?.Invoke(HasDisplayTime);
                _lastColumn = Math.Max(_pid, Math.Max(_chain, _duration));
                continue;
            }
            if (_pid < 0 || _chain < 0 || _duration < 0 || fields.Length <= _lastColumn) continue;
            if (!uint.TryParse(fields[_pid], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint pid)
                || !double.TryParse(fields[_duration], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms)
                || !double.IsFinite(ms) || ms <= 0 || string.IsNullOrWhiteSpace(fields[_chain])) continue;
            sample = new PresentMonSample(pid, fields[_chain], ms);
            return true;
        }
        return false;
    }

    public void Dispose() => _parser.Dispose();
}

internal readonly record struct PresentMonSample(uint ProcessId, string SwapChain, double Milliseconds);
