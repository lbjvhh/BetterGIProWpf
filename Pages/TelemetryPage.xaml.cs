using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Replay;
using BetterGIProWpf.Services.Safety;
using BetterGIProWpf.Services.Telemetry;

namespace BetterGIProWpf.Pages;

public partial class TelemetryPage : Page
{
    private readonly DashboardMetrics _dash = new();
    private readonly EmergencyStop _emergency = new();
    private readonly ReplayRecorder _replayA = new();
    private readonly ReplayRecorder _replayB = new();

    public TelemetryPage()
    {
        InitializeComponent();
        _emergency.Alerted += ev => Dispatcher.Invoke(() => { TelemetryLog.AppendText($"[紧急警报] {ev.Kind}: {ev.Detail}\n"); EmerStatus.Text = "已停机"; });
        _emergency.Log += m => Dispatcher.Invoke(() => TelemetryLog.AppendText(m + "\n"));
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        TelemetryLog.AppendText("== 60 秒指标采样（1Hz，覆盖 11 项指标） ==\n");
        var rng = new Random(42);
        for (var i = 0; i < 60; i++)
        {
            _dash.Sample("游戏FPS", 55 + rng.Next(20));
            _dash.Sample("GPU占用%", 40 + rng.Next(40));
            _dash.Sample("同步误差ms", 100 + rng.Next(400));
            _dash.Sample("任务进度%", i * 100 / 60d);
            _dash.Sample("AI推理FPS", 15 + rng.Next(5));
            System.Threading.Thread.Sleep(1);
        }
        var names = _dash.Names;
        DashSummary.Text = $"{names.Count} 项指标 · 最新: FPS={_dash.Latest("游戏FPS"):0} GPU={_dash.Latest("GPU占用%"):0}% 同步误差={_dash.Latest("同步误差ms"):0}ms";
        TelemetryLog.AppendText($"指标: {string.Join(" / ", names)}\n");
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "metrics.csv");
        System.IO.File.WriteAllText(path, _dash.ExportCsv());
        TelemetryLog.AppendText($"已导出指标 CSV → {path}\n");
    }

    private void EmergencyNet_Click(object sender, RoutedEventArgs e) => _emergency.Raise(EmergencyKind.NetworkError, "网络连接失败，正在重试…（模拟）");
    private void EmergencyBan_Click(object sender, RoutedEventArgs e) => _emergency.Raise(EmergencyKind.BanWarning, "检测到账号异常提示（模拟风险文本）");
    private void Resume_Click(object sender, RoutedEventArgs e) { _emergency.Resume(); EmerStatus.Text = "已恢复"; }

    private void Replay_Click(object sender, RoutedEventArgs e)
    {
        TelemetryLog.AppendText("== 记录一次任务执行（帧+输入+识别） ==\n");
        for (var i = 0; i < 20; i++)
        {
            _replayA.Record(new ReplayFrame
            {
                TimeSec = i,
                VideoFramePath = $"v{i}.jpg", GameFramePath = $"g{i}.jpg",
                InputCommand = i is 8 or 15 ? "移动" : "等待",
                Recognition = i is 8 or 15 ? "位置获取异常" : "成功",
                DeviationNote = i is 8 or 15 ? "与计划路径偏离" : null
            });
        }
        TelemetryLog.AppendText($"已记录 {_replayA.Count} 帧，标记偏离 {_replayA.DeviationCount} 个\n");
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (_replayA.Count == 0) { Replay_Click(sender, e); }
        TelemetryLog.AppendText("== 两次执行对比 ==\n" + ReplayRecorder.Compare(_replayA, _replayB) + "\n");
    }
}
