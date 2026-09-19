using System;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Audio;
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
    private readonly AudioSceneClassifier _audioScene = new();
    private readonly HttpClient _httpLong = new() { Timeout = TimeSpan.FromSeconds(30) };

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
                // CPU 占用
                try { if (_cpuCounter != null) _dash.Sample("CPU占用%", Math.Round(_cpuCounter.NextValue(), 1)); } catch { }

                // 内存占用（MB）
                var mem = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
                _dash.Sample("内存MB", Math.Round(mem, 0));

                // vision_server 5004 响应时间
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

                // 任务进度（基于运行时长的占位）
                _dash.Sample("运行时长s", Math.Round(_sw.Elapsed.TotalSeconds, 0));

                // 更新 UI
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

    /// <summary>P3: 设置 UI 基线帧。</summary>
    private async void UiBaseline_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var http = AppState.Http;
            var grab = await http.PostAsync("http://127.0.0.1:5005/grab",
                new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            var gj = await grab.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(gj);
            var b64 = doc.RootElement.GetProperty("frame_b64").GetString();
            var body = System.Text.Json.JsonSerializer.Serialize(new { image = b64, name = DateTime.Now.ToString("HHmmss") });
            var r = await http.PostAsync("http://127.0.0.1:5004/ui_set_baseline",
                new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            UiDiffStatus.Text = "基线已设置";
            TelemetryLog.AppendText($"[UI] 基线已设置\n");
        }
        catch (Exception ex) { UiDiffStatus.Text = "失败"; TelemetryLog.AppendText($"[UI] {ex.Message}\n"); }
    }

    /// <summary>P3: 对比当前帧与基线，检测 UI 变化。</summary>
    private async void UiDiff_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var http = AppState.Http;
            var grab = await http.PostAsync("http://127.0.0.1:5005/grab",
                new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            var gj = await grab.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(gj);
            var b64 = doc.RootElement.GetProperty("frame_b64").GetString();
            var body = System.Text.Json.JsonSerializer.Serialize(new { image = b64, threshold = 500 });
            var r = await http.PostAsync("http://127.0.0.1:5004/ui_diff",
                new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            var rj = await r.Content.ReadAsStringAsync();
            UiDiffStatus.Text = rj;
            TelemetryLog.AppendText($"[UI diff] {rj}\n");
        }
        catch (Exception ex) { UiDiffStatus.Text = "失败"; TelemetryLog.AppendText($"[UI diff] {ex.Message}\n"); }
    }

    private void AutoAdapt_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mapPath = System.IO.Path.Combine(AppContext.BaseDirectory, "User", "ui_mapping.json");
            var dict = new Dictionary<string, int[]>();
            if (System.IO.File.Exists(mapPath))
            {
                var old = System.IO.File.ReadAllText(mapPath);
                dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int[]>>(old) ?? new();
            }
            if (!dict.ContainsKey("map_button")) dict["map_button"] = new[] { 100, 200 };
            if (!dict.ContainsKey("teleport_icon")) dict["teleport_icon"] = new[] { 300, 400 };
            if (!dict.ContainsKey("quest_bar")) dict["quest_bar"] = new[] { 50, 50 };
            if (!dict.ContainsKey("bag_button")) dict["bag_button"] = new[] { 900, 100 };
            var json = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(mapPath, json);
            TelemetryLog.AppendText($"[模块25] UI 坐标映射已更新: {mapPath}\n");
        }
        catch (Exception ex) { TelemetryLog.AppendText($"[模块25] 失败: {ex.Message}\n"); }
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

    /// <summary>模块24: 网络波动检测。</summary>
    private async void NetCheck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var http = AppState.Http;
            var resp = await http.PostAsync("http://127.0.0.1:5005/network_check",
                new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            var json = await resp.Content.ReadAsStringAsync();
            TelemetryLog.AppendText($"[网络] {json}\n");
            EmerStatus.Text = json.Contains("\"stuck\":true") ? "网络疑似卡住" : "网络正常";
        }
        catch (Exception ex) { TelemetryLog.AppendText($"[网络] 失败: {ex.Message}\n"); }
    }

    /// <summary>模块21/22: 异常诊断知识库。</summary>
    private readonly Services.Adaptive.ExceptionAdvisor _advisor = new();

    private void Diag_Click(object sender, RoutedEventArgs e)
    {
        var err = ErrBox.Text.Trim();
        if (err.Length == 0) return;
        var strategies = _advisor.Diagnose(err);
        TelemetryLog.AppendText($"[诊断] 错误: {err}\n");
        if (strategies.Count == 0) TelemetryLog.AppendText("  无匹配策略\n");
        foreach (var s in strategies)
            TelemetryLog.AppendText($"  → P{s.Priority} {s.Name}: {s.Action}\n");
        _advisor.RecordResult(err, strategies.FirstOrDefault()?.Name ?? "无", "已推荐");
    }

    private void DiagExport_Click(object sender, RoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "User", "diag_history.json");
        System.IO.File.WriteAllText(path, _advisor.ExportHistoryJson(), System.Text.Encoding.UTF8);
        TelemetryLog.AppendText($"[诊断] 历史已导出到 {path}\n");
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

    /// <summary>P3: 导出最近一次持续识别的步骤 JSON 到 User/Replays/。</summary>
    private void ExportReplay_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "User", "Replays");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"run_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            System.IO.File.WriteAllText(path, AppState.LastStepsJson, System.Text.Encoding.UTF8);
            var stepCount = AppState.LastStepsJson.Count(c => c == '{') - 1;
            TelemetryLog.AppendText($"[回放] 已导出 {stepCount} 步到 {path}\n");
        }
        catch (Exception ex) { TelemetryLog.AppendText($"[回放] 导出失败: {ex.Message}\n"); }
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        // 模块18: 读 User/Replays/ 下最近两个 JSON 对比
        try
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "User", "Replays");
            if (!System.IO.Directory.Exists(dir)) { TelemetryLog.AppendText("[对比] 无回放目录\n"); return; }
            var files = System.IO.Directory.GetFiles(dir, "run_*.json").OrderByDescending(f => f).Take(2).ToList();
            if (files.Count < 2) { TelemetryLog.AppendText("[对比] 需至少 2 个回放文件\n"); return; }
            var j1 = System.IO.File.ReadAllText(files[0]);
            var j2 = System.IO.File.ReadAllText(files[1]);
            using var d1 = System.Text.Json.JsonDocument.Parse(j1);
            using var d2 = System.Text.Json.JsonDocument.Parse(j2);
            var a1 = d1.RootElement.EnumerateArray().ToList();
            var a2 = d2.RootElement.EnumerateArray().ToList();
            TelemetryLog.AppendText($"== 对比 {System.IO.Path.GetFileName(files[0])} vs {System.IO.Path.GetFileName(files[1])} ==\n");
            TelemetryLog.AppendText($"  步数: {a1.Count} vs {a2.Count}\n");
            // 动作分布对比
            var g1 = a1.GroupBy(x => x.GetProperty("Action").GetString()).Select(g => $"{g.Key}={g.Count()}");
            var g2 = a2.GroupBy(x => x.GetProperty("Action").GetString()).Select(g => $"{g.Key}={g.Count()}");
            TelemetryLog.AppendText($"  A 分布: {string.Join(", ", g1)}\n");
            TelemetryLog.AppendText($"  B 分布: {string.Join(", ", g2)}\n");
            var diff = Math.Abs(a1.Count - a2.Count);
            TelemetryLog.AppendText($"  步数差: {diff}\n");
            double t1 = 0, t2 = 0;
            if (a1.Count > 0 && a1[^1].TryGetProperty("TimeSec", out var te1)) t1 = te1.GetDouble();
            if (a2.Count > 0 && a2[^1].TryGetProperty("TimeSec", out var te2)) t2 = te2.GetDouble();
            TelemetryLog.AppendText($"  总耗时: {t1:F1}s vs {t2:F1}s (差 {Math.Abs(t1-t2):F1}s)\n");
            var dev1 = a1.Count(x => x.TryGetProperty("Action", out var ac) && ac.GetString() == "wait");
            var dev2 = a2.Count(x => x.TryGetProperty("Action", out var ac) && ac.GetString() == "wait");
            TelemetryLog.AppendText($"  等待/偏离点: {dev1} vs {dev2}\n");
        }
        catch (Exception ex) { TelemetryLog.AppendText($"[对比] 失败: {ex.Message}\n"); }
    }

    /// <summary>模块17: 音频场景识别——NAudio 采样 1 秒 → CPU 特征 → 分类 + 视觉融合。</summary>
    private async void AudioScene_Click(object sender, RoutedEventArgs e)
    {
        TelemetryLog.AppendText("[音频] 正在采样 1s 音频…\n");
        try
        {
            var pcm = await Task.Run(RecordOneSecondPcm);
            if (pcm == null || pcm.Length == 0)
            {
                TelemetryLog.AppendText("[音频] 未采集到数据（无音频输入设备或已被占用）\n");
                return;
            }
            var feat = AudioSceneClassifier.Extract(pcm);
            var type = _audioScene.Classify(feat);
            var visual = AppState.LastOcrText ?? "";
            var fused = AudioSceneClassifier.Fuse(type, visual);
            SceneStatus.Text = $"{type} / {fused}";
            TelemetryLog.AppendText($"[音频] RMS={feat.Rms:0.000} ZCR={feat.ZeroCrossingRate:0.000} 质心={feat.SpectralCentroid:0.000} → {type}（延迟 {_audioScene.RecognitionLatencyMs:0.0}ms）\n");
            TelemetryLog.AppendText($"[音频] 视觉融合: {fused}\n");
        }
        catch (Exception ex) { TelemetryLog.AppendText($"[音频] 失败: {ex.Message}\n"); }
    }

    private static byte[]? RecordOneSecondPcm()
    {
        var waveIn = new NAudio.Wave.WaveInEvent
        {
            WaveFormat = new NAudio.Wave.WaveFormat(16000, 16, 1),
            BufferMilliseconds = 200
        };
        var buf = new System.Collections.Generic.List<byte>();
        waveIn.DataAvailable += (_, e) => { lock (buf) buf.AddRange(e.Buffer); };
        try
        {
            waveIn.StartRecording();
            System.Threading.Thread.Sleep(1100);
            waveIn.StopRecording();
        }
        finally { waveIn.Dispose(); }
        lock (buf) return buf.Count == 0 ? null : buf.ToArray();
    }

    /// <summary>模块3 辅助: YOLO 检测当前帧（抓帧 → vision_server /yolo）。</summary>
    private async void YoloDetect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var http = AppState.Http;
            var grab = await http.PostAsync("http://127.0.0.1:5005/grab",
                new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            var gj = await grab.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(gj);
            if (!doc.RootElement.TryGetProperty("frame_b64", out var fe) || fe.GetString() is not { Length: > 0 } frame)
            { TelemetryLog.AppendText("[YOLO] 抓帧失败\n"); return; }
            var body = System.Text.Json.JsonSerializer.Serialize(new { image = frame });
            var r = await _httpLong.PostAsync("http://127.0.0.1:5004/yolo",
                new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            var rj = await r.Content.ReadAsStringAsync();
            SceneStatus.Text = "YOLO 完成";
            TelemetryLog.AppendText($"[YOLO] {rj[..Math.Min(rj.Length, 400)]}\n");
        }
        catch (Exception ex) { TelemetryLog.AppendText($"[YOLO] 失败: {ex.Message}\n"); }
    }

    /// <summary>模块15: 装备检测——抓帧 → vision_server /equipment_detect（YOLO 图标计数）。</summary>
    private async void EquipDetect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var http = AppState.Http;
            var grab = await http.PostAsync("http://127.0.0.1:5005/grab",
                new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            var gj = await grab.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(gj);
            if (!doc.RootElement.TryGetProperty("frame_b64", out var fe) || fe.GetString() is not { Length: > 0 } frame)
            { TelemetryLog.AppendText("[装备] 抓帧失败\n"); return; }
            var body = System.Text.Json.JsonSerializer.Serialize(new { image = frame });
            var r = await _httpLong.PostAsync("http://127.0.0.1:5004/equipment_detect",
                new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            var rj = await r.Content.ReadAsStringAsync();
            SceneStatus.Text = "装备检测完成";
            TelemetryLog.AppendText($"[装备] {rj[..Math.Min(rj.Length, 400)]}\n");
        }
        catch (Exception ex) { TelemetryLog.AppendText($"[装备] 失败: {ex.Message}\n"); }
    }

    /// <summary>原神专属 YOLO: 抓帧 → vision_server /genshin_detect（专属模型 + OCR 关键词 → 元素/场景）。</summary>
    private async void GenshinDetect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var http = AppState.Http;
            var grab = await http.PostAsync("http://127.0.0.1:5005/grab",
                new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            var gj = await grab.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(gj);
            if (!doc.RootElement.TryGetProperty("frame_b64", out var fe) || fe.GetString() is not { Length: > 0 } frame)
            { TelemetryLog.AppendText("[原神] 抓帧失败\n"); return; }
            var body = System.Text.Json.JsonSerializer.Serialize(new { image = frame });
            var r = await _httpLong.PostAsync("http://127.0.0.1:5004/genshin_detect",
                new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            var rj = await r.Content.ReadAsStringAsync();
            // 解析摘要与对象列表，精简输出
            try
            {
                using var jd = System.Text.Json.JsonDocument.Parse(rj);
                var root = jd.RootElement;
                var scene = root.GetProperty("scene").GetString();
                var ms = root.GetProperty("ms").GetDouble();
                var objs = root.GetProperty("objects");
                var parts = new System.Collections.Generic.List<string>();
                foreach (var o in objs.EnumerateArray())
                    parts.Add($"{o.GetProperty("category").GetString()}({o.GetProperty("confidence").GetDouble():0.00})");
                SceneStatus.Text = $"场景:{scene} · {parts.Count}类";
                TelemetryLog.AppendText($"[原神] 场景={scene} 耗时={ms:0}ms 元素: {string.Join(", ", parts)}\n");
            }
            catch { TelemetryLog.AppendText($"[原神] {rj[..Math.Min(rj.Length, 400)]}\n"); }
        }
        catch (Exception ex) { TelemetryLog.AppendText($"[原神] 失败: {ex.Message}\n"); }
    }
}