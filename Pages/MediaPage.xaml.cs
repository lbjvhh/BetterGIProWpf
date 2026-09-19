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
        MediaLog.AppendText("== 模拟任务执行 90 秒后台录制（1080p/30fps 由采集端保证） ==\n");
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
        MediaLog.AppendText($"裁剪至目标时长：{trimmed.Count} 段（{trimmed.Sum(c => (c.End - c.Start).TotalSeconds):0}s）\n");
        var subs = _recorder.GenerateSubtitles(trimmed);
        MediaLog.AppendText($"字幕 {subs.Count} 条（首条: {subs.FirstOrDefault().Text}）\n");
        MediaStatus.Text = $"录制完成 · 高光 {_clips.Count} · 字幕 {subs.Count}";
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
