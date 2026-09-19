namespace BetterGIProWpf.Services.Media;

public record FrameSample(TimeSpan T, double MotionScore, double CombatScore, double DialogueScore);
public record HighlightClip(int Index, TimeSpan Start, TimeSpan End, double Score, string Reason);
public record BattleReport(int Tasks, int Completed, int Abnormal, double DurationSec, double SuccessRate);

public class HighlightRecorder
{
    private readonly List<FrameSample> _samples = new();
    private readonly object _lock = new();
    public int HighlightCount { get; private set; }
    public TimeSpan RecordingLength { get; private set; }
    public event Action<string>? Log;

    public void Start() { lock (_lock) { _samples.Clear(); HighlightCount = 0; } }

    public List<HighlightClip> Stop() { lock (_lock) { var clips = DetectClips(); return clips; } }

    public void PushFrame(TimeSpan t, byte[] rgb, int width, int height)
    {
        double sum = 0, sum2 = 0; var n = Math.Min(4000, rgb.Length / 3);
        for (var i = 0; i < n * 3; i += 3) { var v = (rgb[i] + rgb[i + 1] + rgb[i + 2]) / 3d; sum += v; sum2 += v * v; }
        var mean = sum / n; var var_ = sum2 / n - mean * mean;
        var motion = Math.Clamp(var_ / 6000d, 0, 1);
        double red = 0; for (var i = 0; i < n * 3; i += 3) red += rgb[i] - (rgb[i + 1] + rgb[i + 2]) / 2d;
        var combat = Math.Clamp(red / n / 60d + 0.25, 0, 1);
        lock (_lock) { _samples.Add(new FrameSample(t, motion, combat, 0.2)); RecordingLength = t; }
    }

    public List<HighlightClip> DetectClips()
    {
        var clips = new List<HighlightClip>();
        if (_samples.Count < 2) return clips;
        var window = Math.Min(60, _samples.Count);
        var idx = 0;
        while (idx < _samples.Count)
        {
            var end = Math.Min(_samples.Count, idx + window);
            double energy = 0, combat = 0;
            for (var i = idx; i < end; i++) { energy += _samples[i].MotionScore; combat += _samples[i].CombatScore; }
            energy /= (end - idx); combat /= (end - idx);
            if (energy > 0.4 || combat > 0.35) clips.Add(new HighlightClip(clips.Count, _samples[idx].T, _samples[end - 1].T, Math.Max(energy, combat), combat > 0.35 ? "战斗" : "高动态"));
            idx = end;
        }
        HighlightCount = clips.Count;
        return clips;
    }

    public static BattleReport BuildReport(int tasks, int completed, int abnormal, double durationSec) =>
        new(tasks, completed, abnormal, durationSec, tasks == 0 ? 0 : (double)completed / tasks);

    public List<HighlightClip> TrimToTarget(List<HighlightClip> clips, int targetSeconds = 60)
    {
        targetSeconds = Math.Clamp(targetSeconds, 30, 120);
        var picked = new List<HighlightClip>(); var used = 0d;
        foreach (var c in clips.OrderByDescending(c => c.Score))
        {
            if (used + (c.End - c.Start).TotalSeconds > targetSeconds + 5) continue;
            picked.Add(c); used += (c.End - c.Start).TotalSeconds;
            if (used >= targetSeconds) break;
        }
        return picked.OrderBy(c => c.Start).ToList();
    }

    public List<(TimeSpan Start, TimeSpan End, string Text)> GenerateSubtitles(List<HighlightClip> clips, Func<string, string>? transcribe = null)
    {
        var subs = new List<(TimeSpan, TimeSpan, string)>();
        foreach (var c in clips)
        {
            var text = transcribe == null ? $"[{c.Reason}] 片段 {c.Index + 1}" : transcribe($"{c.Start:hh\\:mm\\:ss}-{c.End:hh\\:mm\\:ss}");
            subs.Add((c.Start, c.End, text));
        }
        return subs;
    }
}
