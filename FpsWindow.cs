namespace G15Dashboard;

// Access is serialized by FpsReader; timestamps use monotonic milliseconds.
internal sealed class FpsWindow
{
    private readonly Dictionary<string, Samples> _chains = new();
    private readonly List<string> _expired = new();

    public void Add(string chain, long now, double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds <= 0) return;
        if (!_chains.TryGetValue(chain, out var samples)) _chains[chain] = samples = new();
        samples.Trim(now);
        samples.Frames.Enqueue((now, milliseconds));
        samples.TotalMilliseconds += milliseconds;
    }

    public double Read(long now)
    {
        Samples? active = null;
        foreach (var (chain, samples) in _chains)
        {
            samples.Trim(now);
            if (samples.Frames.Count == 0) _expired.Add(chain);
            else if (active is null || samples.Frames.Count > active.Frames.Count) active = samples;
        }
        foreach (string chain in _expired) _chains.Remove(chain);
        _expired.Clear();
        return active is null || active.Frames.Count < 2 ? -1
            : 1000d * active.Frames.Count / active.TotalMilliseconds;
    }

    public void Clear() => _chains.Clear();

    private sealed class Samples
    {
        public readonly Queue<(long At, double Milliseconds)> Frames = new();
        public double TotalMilliseconds;

        public void Trim(long now)
        {
            while (Frames.TryPeek(out var first) && now - first.At > 1000)
                TotalMilliseconds -= Frames.Dequeue().Milliseconds;
            if (Frames.Count == 0) TotalMilliseconds = 0;
        }
    }
}
