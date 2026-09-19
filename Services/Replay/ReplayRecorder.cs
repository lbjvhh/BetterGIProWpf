using System.Text.Json; namespace BetterGIProWpf.Services.Replay;
public class ReplayFrame { public double TimeSec { get; set; } public string? Input { get; set; } public string? Recognition { get; set; } public bool Deviation { get; set; } public string? Note { get; set; } }
public class ReplayRecorder {
    private readonly List<ReplayFrame> _f = new(); private readonly object _lock = new();
    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..8]; public int DeviationCount { get; private set; }
    public void Record(ReplayFrame f) { lock (_lock) { var dev = (!string.IsNullOrEmpty(f.Recognition) && f.Recognition.ContainsAny("异常","失败","卡住","超时")); f.Deviation = dev; if (dev) DeviationCount++; _f.Add(f); } }
    public IReadOnlyList<ReplayFrame> Frames { get { lock (_lock) return _f.ToArray(); } }
    public string ExportJson() { lock (_lock) return JsonSerializer.Serialize(_f); }
}
internal static class StrExt { public static bool ContainsAny(this string s, params string[] k) => k.Any(x => s.Contains(x, StringComparison.OrdinalIgnoreCase)); }
