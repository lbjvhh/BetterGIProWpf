using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Adaptive;
using BetterGIProWpf.Services.Community;
using BetterGIProWpf.Services.Humanize;
using BetterGIProWpf.Services.LocalScripts;
using BetterGIProWpf.Services.Network;
using BetterGIProWpf.Services.Pathfinding;
using BetterGIProWpf.Services.Ui;

namespace BetterGIProWpf.Pages;

public partial class ScriptsPage : Page
{
    private readonly LocalScriptManager _scripts = new(UserRootOrDefault());
    private readonly ScriptRepoClient _repo = new();
    private readonly AdaptiveExecutor _adaptive = new();
    private readonly StuckDetector _stuck = new();
    private readonly NetworkMonitor _net = new();
    private readonly UiLayoutAdaptor _ui = new();
    private readonly HumanizeInput _human = new();
    private List<ScriptEntry> _lastScanned = new();

    public ScriptsPage()
    {
        InitializeComponent();
        _adaptive.Log += m => Dispatcher.Invoke(() => ScriptsLog.AppendText(m + "\n"));
        _stuck.Log += m => Dispatcher.Invoke(() => ScriptsLog.AppendText(m + "\n"));
        _net.Log += m => Dispatcher.Invoke(() => ScriptsLog.AppendText(m + "\n"));
        _ui.Log += m => Dispatcher.Invoke(() => ScriptsLog.AppendText(m + "\n"));
    }

    private static string UserRootOrDefault()
    {
        if (System.IO.Directory.Exists(@"C:\BetterGI\User")) return @"C:\BetterGI\User";
        // P2-1：默认指向发布目录的 User（与持续识别输出同一根目录）
        return System.IO.Path.Combine(AppContext.BaseDirectory, "User");
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        var root = UserRootBox.Text.Trim();
        var mgr = new LocalScriptManager(root);
        var entries = mgr.Scan();
        _lastScanned = entries;
        ScriptsLog.AppendText($"== 扫描 {root} ==\n");
        if (entries.Count == 0) { ScriptsLog.AppendText("  未找到脚本（目录不存在或为空）。演示：生成示例脚本目录…\n"); CreateDemoScripts(); return; }
        foreach (var en in entries.Take(30))
            ScriptsLog.AppendText($"  [{en.Kind}] {en.Name} v{en.Version} by {en.Author}（min {en.MinGameVersion}）\n");
        ScriptsLog.AppendText($"  共 {entries.Count} 个脚本\n");
    }

    /// <summary>P2-1：真实执行 —— 选择 AutoFight 战斗脚本解析为按键序列，经 stream_bridge /inject 注入。</summary>
    private async void BridgeRun_Click(object sender, RoutedEventArgs e)
    {
        var root = UserRootBox.Text.Trim();
        var mgr = new LocalScriptManager(root);
        var entries = mgr.Scan();
        _lastScanned = entries;

        // 优先 AutoFight 战斗脚本（.txt/.json），其次任意脚本
        var target = entries.FirstOrDefault(x => x.Kind == ScriptKind.AutoFight) ??
                     entries.FirstOrDefault(x => x.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) ??
                     entries.FirstOrDefault();
        if (target == null)
        {
            ScriptsLog.AppendText("无脚本可执行。生成示例战斗脚本（AutoFight/demo_fight.txt）…\n");
            var dir = System.IO.Path.Combine(root, "AutoFight");
            System.IO.Directory.CreateDirectory(dir);
            var demo = System.IO.Path.Combine(dir, "demo_fight.txt");
            System.IO.File.WriteAllText(demo,
                "# 示例战斗脚本（P2-1 真实执行）\n" +
                "walk 2\nattack 3\nskill e\nburst q\ndash 1\njump 1\nwait 300\ninteract 1\n");
            target = new ScriptEntry(ScriptKind.AutoFight, "demo_fight", demo, "-", "-", "-");
            _lastScanned = new List<ScriptEntry> { target };
        }

        ScriptsLog.AppendText($"== 真实执行: [{target.Kind}] {target.Name} ==\n");
        ScriptsLog.AppendText($"   文件: {target.Path}\n");
        var ok = await mgr.ExecuteViaBridgeAsync(target.Path,
            m => Dispatcher.Invoke(() => ScriptsLog.AppendText("  " + m + "\n")));
        ScriptsLog.AppendText($"执行结果: {(ok ? "✅ 成功" : "❌ 失败")}（请确认 stream_bridge 5005 已启动且游戏窗口在前台）\n");
    }

    private void CreateDemoScripts()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bgi_demo_user");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "JsScript"));
        var manifest = """{"manifest_version":1,"name":"demo_collect","version":"1.0.0","author":"demo","main":"main.js"}""";
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "JsScript", "manifest.json"), manifest);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "JsScript", "main.js"),
            "try {\n  log(\"采集脚本开始\");\n} catch (e) { log(\"error: \" + e); }\n");
        UserRootBox.Text = dir;
        var entries = new LocalScriptManager(dir).Scan();
        foreach (var en in entries) ScriptsLog.AppendText($"  [示例] [{en.Kind}] {en.Name} v{en.Version}\n");
    }

    private void RunScript_Click(object sender, RoutedEventArgs e)
    {
        var root = UserRootBox.Text.Trim();
        var script = System.IO.Path.Combine(root, "JsScript", "main.js");
        if (!System.IO.File.Exists(script)) { ScriptsLog.AppendText("无脚本可执行，请先扫描\n"); return; }
        var ok = _scripts.Execute(script, arg: "collect", log: m => Dispatcher.Invoke(() => ScriptsLog.AppendText(m + "\n")));
        ScriptsLog.AppendText($"执行结果: {(ok ? "成功" : "失败")}\n");
    }

    private void FixAnomaly_Click(object sender, RoutedEventArgs e)
    {
        var line = AnomalyBox.Text.Trim();
        var anomaly = _adaptive.Detect(line);
        if (anomaly == null) { ScriptsLog.AppendText($"未识别到异常模式: {line}\n"); return; }
        ScriptsLog.AppendText($"检测到异常: {anomaly}\n");
        var script = """{"match":"template","collectFilter":"all","fightStrategy":"main"}""";
        var fixed_ = _adaptive.Fix(anomaly.Value, script);
        ScriptsLog.AppendText(fixed_ == null ? "修复失败，跳过当前任务\n" : $"修复后配置: {fixed_}\n");
        ScriptsLog.AppendText($"学习记录: {_adaptive.History.Count} 条\n");
    }

    private void Stuck_Click(object sender, RoutedEventArgs e)
    {
        ScriptsLog.AppendText("== 卡死/转圈检测演示 ==\n");
        for (var i = 0; i < 6; i++) { _stuck.Update(100, 100); System.Threading.Thread.Sleep(10); }
        ScriptsLog.AppendText($"卡死检测: {_stuck.DetectStuck()}\n");
        var esc = _stuck.Escape(StuckDetector.Terrain.Narrow);
        ScriptsLog.AppendText(esc == null ? "跳过当前路径点\n" : $"脱离成功: {esc}\n");
        var spin = new StuckDetector();
        for (var i = 0; i < 16; i++)
        {
            var ang = i * Math.PI / 4;
            spin.Update(100 + 10 * Math.Cos(ang), 100 + 10 * Math.Sin(ang));
        }
        ScriptsLog.AppendText($"转圈检测: {spin.DetectSpin()}\n");
    }

    /// <summary>P2-5：实时帧差卡死检测 —— 从 stream_bridge /grab 抓连续帧算帧差，卡死则注入脱离按键。</summary>
    private async void FrameStuck_Click(object sender, RoutedEventArgs e)
    {
        ScriptsLog.AppendText("== 帧差卡死检测（真实抓帧） ==\n");
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            byte[]? prev = null;
            var stuck = new StuckDetector();
            for (var i = 0; i < 8; i++)
            {
                var resp = await http.PostAsync("http://127.0.0.1:5005/grab",
                    new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
                if (!resp.IsSuccessStatusCode) { ScriptsLog.AppendText("  /grab 失败（stream_bridge 未启动？）\n"); return; }
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var b64 = doc.RootElement.GetProperty("frame_b64").GetString();
                var cur = Convert.FromBase64String(b64);
                double diff = 0;
                if (prev != null)
                {
                    var sample = Math.Min(prev.Length, cur.Length);
                    var changed = 0;
                    for (var k = 0; k < sample; k += 97)
                        if (prev[k] != cur[k]) changed++;
                    diff = changed / (double)Math.Ceiling(sample / 97.0);
                }
                prev = cur;
                stuck.UpdateFrameDiff(diff);
                ScriptsLog.AppendText($"  帧 {i + 1}: 帧差 {diff:P1}（卡死={stuck.IsStuck}）\n");
                await Task.Delay(400);
            }
            if (stuck.IsStuck)
            {
                ScriptsLog.AppendText("  ⚠ 检测到卡死，尝试脱离（注入方向组合）…\n");
                var resp = await http.PostAsync("http://127.0.0.1:5005/inject",
                    new System.Net.Http.StringContent("{\"keys\":[\"a\",\"d\",\"space\"],\"delay_ms\":50}", System.Text.Encoding.UTF8, "application/json"));
                ScriptsLog.AppendText($"  脱离注入 → {await resp.Content.ReadAsStringAsync()}\n");
            }
            else
            {
                ScriptsLog.AppendText("  ✅ 画面在正常变化，未卡死\n");
            }
        }
        catch (Exception ex)
        {
            ScriptsLog.AppendText($"  帧差检测异常: {ex.Message}\n");
        }
    }

    private async void RepoRefresh_Click(object sender, RoutedEventArgs e)
    {
        ScriptsLog.AppendText("== 脚本仓库刷新 ==\n");
        var (count, fromCache, msg) = await _repo.RefreshAsync();
        ScriptsLog.AppendText($"  {msg}\n");
        foreach (var s in _repo.All.Take(8))
            ScriptsLog.AppendText($"  [{s.Category}] {s.Name} ({s.Size}B)\n");
    }

    private void RepoSearch_Click(object sender, RoutedEventArgs e)
    {
        var kw = RepoSearchBox.Text.Trim();
        var hits = _repo.Search(kw);
        ScriptsLog.AppendText($"== 仓库搜索「{kw}」：{hits.Count} 个 ==\n");
        foreach (var s in hits.Take(10))
            ScriptsLog.AppendText($"  [{s.Category}] {s.Name}\n");
        if (hits.Count == 0) ScriptsLog.AppendText("  无结果（先点「刷新仓库」或检查网络）\n");
    }

    private async void RepoSubscribe_Click(object sender, RoutedEventArgs e)
    {
        var kw = RepoSearchBox.Text.Trim();
        var hits = _repo.Search(kw);
        if (hits.Count == 0) { ScriptsLog.AppendText("无脚本可订阅，请先搜索/刷新\n"); return; }
        var localDir = System.IO.Path.Combine(UserRootBox.Text.Trim(), "ScriptGroup");
        var (ok, msg) = await _repo.SubscribeAsync(hits[0], localDir);
        ScriptsLog.AppendText($"  {msg}\n");
    }
    private void Humanize_Click(object sender, RoutedEventArgs e)
    {
        // P2-3：强度设置作用于全局实例（CompanionPage/LocalScriptManager 注入链统一生效）
        _human.Level = (HumanCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() switch
        {
            "Low" => HumanizeInput.Intensity.Low,
            "High" => HumanizeInput.Intensity.High,
            _ => HumanizeInput.Intensity.Medium
        };
        AppState.Humanize.Level = _human.Level;
        var intervals = Enumerable.Range(0, 20).Select(_ => _human.JitteredDelay(300)).ToArray();
        var pathPts = Enumerable.Range(0, 8).Select(i => (_human.NoisePoint(i * 100, i * 60, 100).X, _human.NoisePoint(i * 100, i * 60, 100).Y)).ToList();
        ScriptsLog.AppendText($"== 拟人化按键序列（300ms 基准 ±{_human.KeyJitterMs}ms，{_human.Level} 档） ==\n");
        ScriptsLog.AppendText($"  序列: {string.Join(", ", intervals.Take(12))}…\n");
        var report = HumanizeInput.AnalyzeSequence(intervals, pathPts);
        ScriptsLog.AppendText($"  节拍标准差: {report.BeatStdMs:0.0}ms · 固定节拍占比: {report.FixedBeatRatio:P0} · 操作熵: {report.ActionEntropy:0.00} · 路径噪声: {report.PathNoisePct:0.0}%\n");
        ScriptsLog.AppendText($"  综合拟人度: {report.Overall:0.0}/100 — {report.Conclusion}\n");
        var (x, y) = _human.NoisePoint(500, 400, spacing: 100);
        ScriptsLog.AppendText($"  路径点噪声: (500,400)→({x:0.0},{y:0.0})\n");
    }

    private void UiAdapt_Click(object sender, RoutedEventArgs e)
    {
        _ui.RegisterBaseline("5.4", new Dictionary<UiElement, UiLayoutAdaptor.ElementBox>
        {
            [UiElement.Map] = new(100, 80, 60, 60, 0.99),
            [UiElement.Bag] = new(300, 700, 80, 80, 0.98),
        });
        var detected = new Dictionary<UiElement, UiLayoutAdaptor.ElementBox>
        {
            [UiElement.Map] = new(105, 82, 60, 60, 0.95),
            [UiElement.Bag] = new(300, 700, 80, 80, 0.97),
            [UiElement.Character] = new(500, 500, 40, 40, 0.9),
        };
        var result = _ui.Detect("5.4", detected);
        ScriptsLog.AppendText($"UI 适配完成: {result.Count} 元素已映射，需要更新: {_ui.NeedsUserUpdate}\n");
    }

    private void Network_Click(object sender, RoutedEventArgs e)
    {
        ScriptsLog.AppendText("== 网络镜像切换演示 ==\n");
        var ok = _net.FetchWithFallback("/bettergi/scripts/path.json", url =>
            url.Contains("gitee", StringComparison.OrdinalIgnoreCase) ? "{\"mirror\":\"gitee\"}" : null);
        ScriptsLog.AppendText(ok != null ? $"获取成功（备用源）: {ok}\n" : "全部镜像失败\n");
        _net.CheckReconnectScreen("网络连接失败，正在重新连接");
        ScriptsLog.AppendText($"掉线识别: {_net.IsDisconnected}，断点续传: checkpoint 3/10 → {_net.ResumeFromCheckpoint(3, 10)}\n");
    }
}
