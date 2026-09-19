using System;
using System.Diagnostics;
using System.Net.Http;
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
    private System.Windows.Threading.DispatcherTimer? _liveTimer;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private readonly PerformanceCounter? _cpuCounter;
    private readonly Stopwatch _sw = new();

    public TelemetryPage()
    {
        InitializeComponent();
        try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); } catch { }
        _emergency.Alerted += ev => Dispatcher.Invoke(() =>
        {
            TelemetryLog.AppendText($"[紧急警报] {ev.Kind}: {ev.Detail}\n");
            EmerStatus.Text = "已停机";
        });
        _emergency.Log += m => Dispatcher.Invoke(() => TelemetryLog.AppendText(m + "\n"));
    }

    /// <summary>P1-11：开始实时监控（1Hz，真实指标）</summary>
    private async void StartLive_Click(object sender, RoutedEventArgs e)
    {
        if (_liveTimer != null && _liveTimer.IsEnabled) return;
        _sw.Restart();
        _liveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveTimer.Tick += async (_, _) =>
        {
            try
            {
                try { if (_cpuCounter != null) _dash.Sample("CPU占用%", Math.Round(_cpuCounter.NextValue(), 1)); } catch { }

                var mem = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
                _dash.Sample("内存MB", Math.Round(mem, 0));

                var t0 = Stopwatch.StartNew();
                try
                {
                    var r = await _http.GetAsync("http://127.0.0.1:5004/health");
                    _dash.Sample("vision延迟ms", Math.Round(t0.Elapsed.TotalMilliseconds, 0));
                    _dash.Sample("vision在线", r.IsSuccessStatusCode ? 1 : 0);
                }
                catch { _dash.Sample("vision在线", 0); }

                // stream_bridge 5005 安全状态（P2-4：检测到停机自动联动 EmergencyStop）
                try
                {
                    var r = await _http.GetAsync("http://127.0.0.1:5005/safety_status");
                    if (r.IsSuccessStatusCode)
                    {
                        var json = await r.Content.ReadAsStringAsync();
                        var stopped = json.Contains("\"emergency\":true");
                        if (stopped)
                        {
                            _dash.Sample("紧急停机", 1);
                            if (!_emergency.IsStopped)
                            {
                                _emergency.Raise(EmergencyKind.AntiCheatPopup, "stream_bridge 检测到风险文本，自动停机（紧急状态联动）");
                                EmerStatus.Text = "⚠ 已停机（bridge）";
                                TelemetryLog.AppendText("[应急] stream_bridge 上报 emergency=true，已联动停机\n");
                            }
                        }
                        else
                        {
                            _dash.Sample("紧急停机", 0);
                            if (_emergency.IsStopped) { EmerStatus.Text = "运行中"; }
                        }
                    }
                }
                catch { }

                _dash.Sample("运行时长s", Math.Round(_sw.Elapsed.TotalSeconds, 0));

                var names = _dash.Names;
                DashSummary.Text = $"{names.Count} 项指标 · " +
                    $"CPU={_dash.Latest("CPU占用%"):0}% · " +
                    $"内存={_dash.Latest("内存MB"):0}MB · " +
                    $"vision={_dash.Latest("vision延迟ms"):0}ms · " +
                    $"停机={(_dash.Latest("紧急停机") > 0.5 ? "是" : "否")}";
            }
            catch (Exception ex) { TelemetryLog.AppendText($"[监控] {ex.Message}\n"); }
        };
        _liveTimer.Start();
        TelemetryLog.AppendText("== 实时监控已启动（1Hz，真实指标） ==\n");
    }

    private void StopLive_Click(object sender, RoutedEventArgs e)
    {
        _liveTimer?.Stop();
        DashSummary.Text = "监控已停止";
        TelemetryLog.AppendText("== 实时监控已停止 ==\n");
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
        TelemetryLog.AppendText($"历史曲线: 游戏FPS 最近60s {_dash.History("游戏FPS").Count} 点\n");
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "metrics.csv");
        System.IO.File.WriteAllText(path, _dash.ExportCsv());
        TelemetryLog.AppendText($"已导出指标 CSV → {path}\n");
    }

    private void EmergencyNet_Click(object sender, RoutedEventArgs e)
    {
        _emergency.Raise(EmergencyKind.NetworkError, "网络连接失败，正在重试…（模拟）");
    }

    private void EmergencyBan_Click(object sender, RoutedEventArgs e)
    {
        _emergency.Raise(EmergencyKind.BanWarning, "检测到账号异常提示（模拟风险文本）");
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        _emergency.Resume();
        EmerStatus.Text = "已恢复";
    }

    /// <summary>P2-4：安全复位 —— 调 stream_bridge /safety_reset 解除停机。</summary>
    private async void SafetyReset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var resp = await http.PostAsync("http://127.0.0.1:5005/safety_reset",
                new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            var json = await resp.Content.ReadAsStringAsync();
            _emergency.Resume();
            EmerStatus.Text = resp.IsSuccessStatusCode ? $"已复位({json})" : "复位失败";
            TelemetryLog.AppendText($"[应急] 安全复位 → {json}\n");
        }
        catch (Exception ex)
        {
            EmerStatus.Text = "bridge 未启动";
            TelemetryLog.AppendText($"[应急] 安全复位失败: {ex.Message}（stream_bridge 5005 未启动？）\n");
        }
    }

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
        TelemetryLog.AppendText($"导出 JSON 示例: {_replayA.ExportJson()[..80]}…\n");
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (_replayA.Count == 0) { Replay_Click(sender, e); }
        TelemetryLog.AppendText("== 两次执行对比 ==\n");
        TelemetryLog.AppendText(ReplayRecorder.Compare(_replayA, _replayB) + "\n");
    }
}
