using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace BetterGIProWpf.Services.Media;

public record FrameSample(TimeSpan T, double MotionScore, double CombatScore, double DialogueScore);
public record HighlightClip(int Index, TimeSpan Start, TimeSpan End, double Score, string Reason);
public record BattleReport(int Tasks, int Completed, int Abnormal, double DurationSec, double SuccessRate);

public class HighlightRecorder
{
    private readonly List<FrameSample> _samples = new();
    private readonly object _lock = new();
    private readonly Random _rng = new();

    public int HighlightCount { get; private set; }
    public TimeSpan RecordingLength { get; private set; }
    public event Action<string>? Log;

    public void Start() { lock (_lock) { _samples.Clear(); HighlightCount = 0; Log?.Invoke("录制开始"); } }
    public List<HighlightClip> Stop() { lock (_lock) { var clips = DetectClips(); Log?.Invoke($"录制结束，共 {clips.Count} 个高光"); return clips; } }

    public void PushFrame(TimeSpan t, byte[] rgb, int width, int height)
    {
        var motion = EstimateMotion(rgb);
        var combat = EstimateCombat(rgb);
        var dialogue = EstimateDialogue(rgb);
        lock (_lock) { _samples.Add(new FrameSample(t, motion, combat, dialogue)); RecordingLength = t; }
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
            double energy = 0, combat = 0, dialogue = 0;
            for (var i = idx; i < end; i++)
            {
                energy += _samples[i].MotionScore;
                combat += _samples[i].CombatScore;
                dialogue += _samples[i].DialogueScore;
            }
            energy /= (end - idx); combat /= (end - idx); dialogue /= (end - idx);
            if (energy > 0.4 || combat > 0.35 || dialogue > 0.5)
            {
                var reason = combat > 0.35 ? "战斗" : dialogue > 0.5 ? "对话/剧情" : "高动态";
                clips.Add(new HighlightClip(clips.Count, _samples[idx].T, _samples[end - 1].T, Math.Max(energy, Math.Max(combat, dialogue)), reason));
            }
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
        var picked = new List<HighlightClip>();
        var used = 0d;
        foreach (var c in clips.OrderByDescending(c => c.Score))
        {
            if (used + (c.End - c.Start).TotalSeconds > targetSeconds + 5) continue;
            picked.Add(c); used += (c.End - c.Start).TotalSeconds;
            if (used >= targetSeconds) break;
        }
        return picked.OrderBy(c => c.Start).ToList();
    }

    public List<(TimeSpan Start, TimeSpan End, string Text)> GenerateSubtitles(
        List<HighlightClip> clips, Func<string, string>? transcribe = null)
    {
        var subs = new List<(TimeSpan, TimeSpan, string)>();
        foreach (var c in clips)
        {
            var text = transcribe == null ? $"[{c.Reason}] 高光片段 {c.Index + 1}" : transcribe($"{c.Start:hh\\:mm\\:ss}-{c.End:hh\\:mm\\:ss}");
            subs.Add((c.Start, c.End, text));
        }
        return subs;
    }

    private double EstimateMotion(byte[] rgb)
    {
        double sum = 0, sum2 = 0; var n = Math.Min(4000, rgb.Length / 3);
        for (var i = 0; i < n * 3; i += 3) { var v = (rgb[i] + rgb[i + 1] + rgb[i + 2]) / 3d; sum += v; sum2 += v * v; }
        var mean = sum / n; var var_ = sum2 / n - mean * mean;
        return Math.Clamp(var_ / 6000d, 0, 1);
    }

    private double EstimateCombat(byte[] rgb)
    {
        double red = 0; var n = Math.Min(4000, rgb.Length / 3);
        for (var i = 0; i < n * 3; i += 3) red += rgb[i] - (rgb[i + 1] + rgb[i + 2]) / 2d;
        red = Math.Clamp(red / n / 60d + 0.25, 0, 1);
        return red;
    }

    private double EstimateDialogue(byte[] rgb)
    {
        var n = Math.Min(1000, rgb.Length / 3);
        double dark = 0; var c = 0;
        for (var i = n * 2; i < n * 3 && i + 2 < rgb.Length; i += 3) { dark += (rgb[i] + rgb[i + 1] + rgb[i + 2]) / 3d; c++; }
        var bottom = c == 0 ? 0.5 : dark / c / 255d;
        return Math.Clamp(bottom < 0.55 ? 0.7 : 0.1 + _rng.NextDouble() * 0.1, 0, 1);
    }

    public async Task<string> ComposeVideoAsync(string sourceVideo, List<HighlightClip> clips,
        string outputPath, BattleReport? report = null, int ratio = 16)
    {
        if (clips == null || clips.Count == 0 || !File.Exists(sourceVideo)) return "";
        var ffmpeg = FindFfmpeg();
        if (ffmpeg == null) { Log?.Invoke("[高光合成] 未找到 ffmpeg"); return ""; }

        var listPath = Path.Combine(Path.GetTempPath(), $"hl_concat_{Guid.NewGuid():N}.txt");
        var lines = clips.Select(c =>
        {
            var dur = (c.End - c.Start).TotalSeconds;
            return $"file '{sourceVideo}'\ninpoint {c.Start.TotalSeconds}\noutpoint {c.End.TotalSeconds}\nduration {dur}";
        });
        await File.WriteAllLinesAsync(listPath, lines);

        try
        {
            var scale = ratio == 21 ? "scale=1920:816" : "scale=1920:1080";
            var args = $"-y -f concat -safe 0 -i \"{listPath}\" -vf \"{scale},format=yuv420p\" -c:v libx264 -preset veryfast -crf 23 -c:a aac \"{outputPath}\"";
            Log?.Invoke($"[高光合成] ffmpeg 拼接 {clips.Count} 段 → {outputPath}");
            var psi = new ProcessStartInfo(ffmpeg, args) { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var proc = Process.Start(psi);
            if (proc == null) return "";
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0)
            {
                var err = await proc.StandardError.ReadToEndAsync();
                Log?.Invoke($"[高光合成] ffmpeg 失败: {err[..Math.Min(300, err.Length)]}");
                return "";
            }
            if (report != null)
            {
                var overlay = Path.Combine(Path.GetDirectoryName(outputPath)!, $"_overlay_{Guid.NewGuid():N}.mp4");
                var text = $"任务 {report.Completed}/{report.Tasks}  耗时 {report.DurationSec:F0}s  异常 {report.Abnormal}";
                var ovArgs = $"-y -i \"{outputPath}\" -vf \"drawtext=fontfile='C\\:/Windows\\/Fonts\\/msyh.ttc':text='{text}':x=10:y=10:fontsize=28:fontcolor=white:box=1:boxcolor=black@0.6\" -c:a copy \"{overlay}\"";
                using var ov = Process.Start(new ProcessStartInfo(ffmpeg, ovArgs) { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
                if (ov != null) { await ov.WaitForExitAsync(); if (ov.ExitCode == 0) { File.Copy(overlay, outputPath, true); File.Delete(overlay); } }
            }
            Log?.Invoke($"[高光合成] 完成: {outputPath}");
            return outputPath;
        }
        catch (Exception ex) { Log?.Invoke($"[高光合成] 异常: {ex.Message}"); return ""; }
        finally { try { if (File.Exists(listPath)) File.Delete(listPath); } catch { } }
    }

    private static string? FindFfmpeg()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            try { var p = Path.Combine(dir, "ffmpeg.exe"); if (File.Exists(p)) return p; } catch { }
        }
        var winget = @"C:\Users\as123\AppData\Local\Microsoft\WinGet\Packages";
        if (Directory.Exists(winget))
        {
            try
            {
                var found = Directory.GetDirectories(winget, "Gyan.FFmpeg*")
                    .SelectMany(d => Directory.GetFiles(d, "ffmpeg.exe", SearchOption.AllDirectories))
                    .FirstOrDefault();
                if (found != null) return found;
            }
            catch { }
        }
        return null;
    }
}
