using System;
using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.LocalAI;
using BetterGIProWpf.Services.Media;

namespace BetterGIProWpf.Pages;

public partial class MediaPage : Page
{
    private readonly HighlightRecorder _recorder = new();
    private readonly NarrationGenerator _narration = new();
    private List<HighlightClip> _clips = new();

    public MediaPage()
    {
        InitializeComponent();
        LocalAiEngine.BindNarration(_narration);
        _recorder.Log += m => Dispatcher.Invoke(() => MediaLog.AppendText($"[录制] {m}\n"));
        _narration.Log += m => Dispatcher.Invoke(() => MediaLog.AppendText($"[解说] {m}\n"));
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        MediaLog.AppendText("== 模拟任务执行 90 秒后台录制 ==\n");
        _recorder.Start();
        var rng = new Random();
        for (var s = 0; s < 90; s++)
        {
            var combat = s is >= 20 and <= 30 || s is >= 55 and <= 70;
            var frame = new byte[32 * 18 * 3];
            for (var i = 0; i < frame.Length; i += 3)
            {
                if (combat) { frame[i] = (byte)rng.Next(180, 255); frame[i + 1] = (byte)rng.Next(20, 80); frame[i + 2] = (byte)rng.Next(20, 80); }
                else { var v = (byte)rng.Next(60, 160); frame[i] = frame[i + 1] = frame[i + 2] = v; }
            }
            _recorder.PushFrame(TimeSpan.FromSeconds(s), frame, 32, 18);
        }
        _clips = _recorder.Stop();
        MediaLog.AppendText($"识别到 {_clips.Count} 个高光：{string.Join(" / ", _clips.Select(c => $"{c.Reason}@{c.Start.TotalSeconds:0}s"))}\n");
        var trimmed = _recorder.TrimToTarget(_clips, int.Parse((LenCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "60"));
        MediaLog.AppendText($"裁剪至目标时长：{trimmed.Count} 段\n");
        var subs = _recorder.GenerateSubtitles(trimmed);
        MediaLog.AppendText($"字幕 {subs.Count} 条\n");
        MediaStatus.Text = $"录制完成 · 高光 {_clips.Count} · 字幕 {subs.Count}";
    }

    /// <summary>P1-3：真实录制 10 秒（从 stream_bridge 5005 /grab 抓真实游戏帧）。</summary>
    private async void LiveRecord_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        btn.IsEnabled = false;
        MediaLog.AppendText("== 真实录制 10 秒（从 stream_bridge /grab 抓帧） ==\n");
        _recorder.Start();
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var t0 = DateTime.Now;
        var n = 0;
        while ((DateTime.Now - t0).TotalSeconds < 10)
        {
            try
            {
                var resp = await http.PostAsync("http://127.0.0.1:5005/grab",
                    new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("frame_b64", out var fb) && fb.GetString() is { Length: > 0 } b64)
                    {
                        var jpg = Convert.FromBase64String(b64);
                        _recorder.PushFrame(TimeSpan.FromSeconds(n * 0.5), jpg, 1, 1);
                        n++;
                    }
                }
            }
            catch (Exception ex) { MediaLog.AppendText($"[grab err] {ex.Message}\n"); break; }
            await Task.Delay(500);
        }
        _clips = _recorder.Stop();
        MediaLog.AppendText($"真实录制完成：{n} 帧，识别到 {_clips.Count} 个高光片段\n");
        MediaStatus.Text = $"真实录制 {n} 帧 · 高光 {_clips.Count}";
        btn.IsEnabled = true;
    }

    private void Report_Click(object sender, RoutedEventArgs e)
    {
        var report = HighlightRecorder.BuildReport(tasks: 8, completed: 7, abnormal: 1, durationSec: 540);
        MediaLog.AppendText($"[战报] 任务 {report.Tasks} 完成 {report.Completed} 异常 {report.Abnormal} 成功率 {report.SuccessRate:P0} 耗时 {TimeSpan.FromSeconds(report.DurationSec):hh\\:mm\\:ss}\n");
    }

    private void Narrate_Click(object sender, RoutedEventArgs e)
    {
        var events = _clips.Select((c, i) => new NarrationGenerator.GameEvent(
            c.Start.TotalSeconds, c.End.TotalSeconds, c.Reason, $"完成「{c.Reason}」阶段操作")).ToList();
        if (events.Count == 0) events.Add(new NarrationGenerator.GameEvent(0, 20, "探索", "探索风起地并采集资源"));
        var lines = _narration.Generate(events, NarrationStyle.Tutorial);
        var srt = NarrationGenerator.ToSrt(lines);
        MediaLog.AppendText("[解说] 生成解说词（教程风格）:\n" + srt.Split('\n').Take(6).Aggregate("", (a, b) => a + "  " + b + "\n") + "  …\n");
    }
}
