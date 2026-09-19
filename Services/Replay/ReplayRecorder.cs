using System.Text.Json;

namespace BetterGIProWpf.Services.Replay;

public class ReplayFrame
{
    public double TimeSec { get; set; }
    public string? InputCommand { get; set; }
    public string? Recognition { get; set; }
    public bool Deviation { get; set; }
    public string? DeviationNote { get; set; }
}

public class ReplayRecorder
{
    private readonly List<ReplayFrame> _frames = new();
    private readonly object _lock = new();
    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..8];
    public int DeviationCount { get; private set; }

    public void Record(ReplayFrame f)
    {
        lock (_lock) { f.Deviation = DetectDeviation(f); if (f.Deviation) DeviationCount++; _frames.Add(f); }
    }

    public static bool DetectDeviation(ReplayFrame f)
    {
        if (!string.IsNullOrEmpty(f.Recognition) && (f.Recognition.Contains("异常") || f.Recognition.Contains("失败") || f.Recognition.Contains("卡住"))) return true;
        return false;
    }

    public IReadOnlyList<ReplayFrame> Frames { get { lock (_lock) return _frames.ToArray(); } }
    public int Count { get { lock (_lock) return _frames.Count; } }

    public static string Compare(ReplayRecorder a, ReplayRecorder b)
    {
        return $"{a.SessionId} vs {b.SessionId}: {a.Count} vs {b.Count} frames, {a.DeviationCount} vs {b.DeviationCount} deviations";
    }

    public string ExportJson() { lock (_lock) return JsonSerializer.Serialize(_frames); }

    public class Playback
    {
        public int Position { get; set; }
        public double Speed { get; set; } = 1.0;
        public bool IsPlaying { get; set; }
    }
}
