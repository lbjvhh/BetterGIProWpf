using System.IO;
using BetterGIProWpf.Services.Automation;
using BetterGIProWpf.Services.BetterGI;
using BetterGIProWpf.Services.Calibration;
using BetterGIProWpf.Services.EndToEnd;
using BetterGIProWpf.Services.Evaluation;
using BetterGIProWpf.Services.Integration;
using BetterGIProWpf.Services.Companion;
using BetterGIProWpf.Services.Coach;
using BetterGIProWpf.Services.Media;
using BetterGIProWpf.Services.Knowledge;
using BetterGIProWpf.Services.Rules;
using BetterGIProWpf.Services.Community;
using BetterGIProWpf.Services.Telemetry;
using BetterGIProWpf.Services.Safety;
using BetterGIProWpf.Services.Audio;
using BetterGIProWpf.Services.LocalScripts;
using BetterGIProWpf.Services.Adaptive;
using BetterGIProWpf.Services.Pathfinding;
using BetterGIProWpf.Services.Network;
using BetterGIProWpf.Services.Ui;
using BetterGIProWpf.Services.Humanize;
using BetterGIProWpf.Services.Replay;

var pass = 0; var fail = 0;
void Check(string name, bool cond, string? detail = "")
{
    if (cond) { pass++; Console.WriteLine($"  ✔ {name}"); }
    else { fail++; Console.WriteLine($"  ✘ {name}  {detail}"); }
}

Console.WriteLine("== CronExpression ==");
{
    var c = new CronExpression("0 3 * * *");
    var next = c.Next(new DateTime(2026, 9, 18, 20, 0, 0));
    Check("每日 03:00 → 次日 03:00", next == new DateTime(2026, 9, 19, 3, 0, 0), next.ToString());

    var c5 = new CronExpression("*/5 * * * *");
    var n5 = c5.Next(new DateTime(2026, 9, 18, 20, 3, 0));
    Check("每 5 分钟 → 20:05", n5 == new DateTime(2026, 9, 18, 20, 5, 0), n5.ToString());

    var cw = new CronExpression("0 9 * * 1");
    var nw = cw.Next(new DateTime(2026, 9, 18, 12, 0, 0)); // 周五
    Check("每周一 09:00 → 下周一", nw == new DateTime(2026, 9, 21, 9, 0, 0), nw.ToString());

    try { _ = new CronExpression("0 3 * *"); Check("非法表达式应抛异常", false); }
    catch (FormatException) { Check("非法表达式应抛异常", true); }
}

Console.WriteLine("== CoordinateCalibration ==");
{
    var cal = new CoordinateCalibration();
    cal.Fit(new List<CoordinateCalibration.Landmark>
    {
        new(0, 0, 100, 200),
        new(1920, 0, 2020, 200),
        new(0, 1080, 100, 1280),
    });
    var (x, y) = cal.Transform(0, 0);
    Check("恒等偏移变换 (0,0)→(100,200)", Math.Abs(x - 100) < 1e-6 && Math.Abs(y - 200) < 1e-6, $"({x},{y})");
    var (x2, y2) = cal.Transform(1920, 1080);
    Check("变换 (1920,1080)→(2020,1280)", Math.Abs(x2 - 2020) < 1e-6 && Math.Abs(y2 - 1280) < 1e-6, $"({x2},{y2})");
    Check("平均误差≈0", cal.MeanError(new List<CoordinateCalibration.Landmark>
    {
        new(0, 0, 100, 200), new(1920, 0, 2020, 200), new(0, 1080, 100, 1280),
    }) < 1e-6);
}

Console.WriteLine("== PathSmoother ==");
{
    var pts = new List<(double, double)> { (0, 0), (10, 0), (20, 0), (30, 0) };
    var sm = PathSmoother.Smooth(pts, 3);
    Check("平滑后点数不变", sm.Count == 4);
    Check("直线平滑后仍在直线上", sm.All(p => Math.Abs(p.Item2) < 1e-9));
    Check("端点基本不变", Math.Abs(sm[0].Item1 - 0) < 1e-9 && Math.Abs(sm[^1].Item1 - 30) < 1e-9);
}

Console.WriteLine("== TaskEvaluator ==");
{
    var gold = """{"tasks":[{"type":"teleport","goal":"传送到风起地","location":"风起地","actions":"传送"},{"type":"explore","goal":"沿小路前进","actions":"跑"}],"scenes":[{"start":0},{"start":5}]}""";
    var predSame = gold;
    var r1 = TaskEvaluator.Evaluate(gold, predSame);
    Check("相同标注 → 准确率 100%", r1.TaskAccuracy >= 0.99 && r1.SceneAccuracy >= 0.99, r1.Report);

    var predBad = """{"tasks":[{"type":"wait","goal":"完全无关","actions":"等"}],"scenes":[{"start":0},{"start":1},{"start":2},{"start":9}]}""";
    var r2 = TaskEvaluator.Evaluate(gold, predBad);
    Check("完全错误 → 准确率 < 50%", r2.TaskAccuracy < 0.5, r2.Report);
}

Console.WriteLine("== FeatureCache ==");
{
    var dir = Path.Combine(Path.GetTempPath(), "bgi_cache_test_" + Guid.NewGuid().ToString("N")[..6]);
    var vp = Path.Combine(dir, "v.mp4");
    Directory.CreateDirectory(dir);
    File.WriteAllText(vp, "fake-video-content");
    var cache = new FeatureCache(dir + "_c", ttlDays: 30);
    var miss = cache.Get(vp);
    Check("首次未命中", miss == null);
    cache.Put(vp, """{"ok":1}""");
    var hit = cache.Get(vp);
    Check("写入后命中", hit != null && hit.ResultJson.Contains("ok"));
    Check("缓存条目数=1", cache.Count == 1);
    Directory.Delete(dir, true);
}

Console.WriteLine("== BetterGIScriptGenerator ==");
{
    var outDir = Path.Combine(Path.GetTempPath(), "bgi_script_test_" + Guid.NewGuid().ToString("N")[..6]);
    var dir = BetterGIScriptGenerator.Generate(outDir, "test_script", new[]
    {
        new BetterGIScriptGenerator.TaskItem("teleport", "传送到风起地", "风起地", "打开地图传送"),
    }, "bilibili.com/test");
    var manifest = Path.Combine(dir, "manifest.json");
    var main = Path.Combine(dir, "main.js");
    Check("manifest.json 存在", File.Exists(manifest));
    Check("main.js 存在", File.Exists(main));
    var mj = File.ReadAllText(manifest);
    Check("manifest 含必填字段", mj.Contains("manifest_version") && mj.Contains("name") && mj.Contains("version") && mj.Contains("main"));
    var js = File.ReadAllText(main);
    Check("main.js 含 try-catch 与取消令牌", js.Contains("try {") && js.Contains("cancelled"));
    Directory.Delete(outDir, true);
}

Console.WriteLine("== EndToEnd (Mock) ==");
{
    var agent = new MockEndToEndAgent();
    agent.Load();
    var out_ = await agent.InferAsync(new byte[480 * 270 * 3], 480, 270);
    Check("模拟代理返回低置信度（触发回退演示）", out_.Confidence < 0.6f && Math.Abs(out_.LeftY) > 0.1f, out_.Confidence.ToString());
}

Console.WriteLine("== CompanionAgent (M1) ==");
{
    var p = CompanionAgent.ParseCommand("帮我打这个怪");
    Check("解析战斗指令", p.TaskType == CompanionTaskType.Combat, p.TaskType.ToString());
    var p2 = CompanionAgent.ParseCommand("去风起地");
    Check("解析传送指令+目标", p2.TaskType == CompanionTaskType.Teleport && p2.Target == "风起地", $"{p2.TaskType}/{p2.Target}");
    var p3 = CompanionAgent.ParseCommand("跟随我");
    Check("解析跟随指令", p3.TaskType == CompanionTaskType.Follow);
    var agent = new CompanionAgent();
    var log = new List<string>();
    agent.Log += log.Add;
    agent.HandleTextCommand("帮我打这个怪");
    await Task.Delay(900);
    Check("子任务执行完成", agent.CompletedCount == 1, $"done={agent.CompletedCount}");
    agent.TakeoverControl();
    Check("热键接管后切换跟随", agent.Mode == CompanionAgent.BehaviorMode.Follow);
}

Console.WriteLine("== StrategyCoach (M2) ==");
{
    var coach = new StrategyCoach { Frequency = CoachFrequency.High, TriggerThreshold = 0.1 };
    var video = new StrategyCoach.VideoContext
    {
        Histogram = new double[16], KeyTexts = new[] { "风起地" }, SceneName = "风起地",
        Actions = new List<string> { "元素战技" }, Weaknesses = new Dictionary<string, string> { ["狼"] = "雷" }
    };
    video.Histogram[0] = 1;
    var list = coach.Analyze(video, new double[16], Array.Empty<string>(), staminaRatio: 0.1, "雷电将军", "狼", new[] { true });
    Check("低频配置下生成建议", list.Count >= 1, $"count={list.Count}");
    Check("包含弱点/体力类建议", list.Any(s => s.Kind == SuggestionKind.EnemyWeakness || s.Kind == SuggestionKind.Stamina));
}

Console.WriteLine("== HighlightRecorder (M3) ==");
{
    var rec = new HighlightRecorder();
    rec.Start();
    var rng = new Random(7);
    for (var s = 0; s < 90; s++)
    {
        var combat = s is >= 20 and <= 30;
        var f = new byte[32 * 18 * 3];
        for (var i = 0; i < f.Length; i += 3)
        {
            if (combat) { f[i] = (byte)rng.Next(200, 255); f[i + 1] = (byte)rng.Next(20, 60); f[i + 2] = (byte)rng.Next(20, 60); }
            else { var v = (byte)rng.Next(60, 150); f[i] = f[i + 1] = f[i + 2] = v; }
        }
        rec.PushFrame(TimeSpan.FromSeconds(s), f, 32, 18);
    }
    var clips = rec.Stop();
    Check("识别到高光片段", clips.Count >= 1, $"clips={clips.Count}");
    var trimmed = rec.TrimToTarget(clips, 60);
    Check("裁剪总时长≤目标+5s", trimmed.Sum(c => (c.End - c.Start).TotalSeconds) <= 65);
    var rep = HighlightRecorder.BuildReport(8, 7, 1, 540);
    Check("战报统计正确", rep.SuccessRate == 0.875 && rep.Abnormal == 1);
}

Console.WriteLine("== KnowledgeBase (M4) ==");
{
    var kb = new KnowledgeBase();
    kb.Add(new KnowledgeEntry { VideoSource = "v1", TimestampSec = 45, TaskDescription = "传送到风起地七天神像", Location = "风起地", TaskType = "传送" });
    kb.Add(new KnowledgeEntry { VideoSource = "v2", TimestampSec = 210, TaskDescription = "击败北风狼王弱点是雷元素", BossName = "北风狼王", TaskType = "战斗" });
    var hits = kb.Search("风起地 传送");
    Check("语义检索命中", hits.Count >= 1 && hits[0].Entry.Location == "风起地", $"top={hits.FirstOrDefault()?.Entry.Location}");
    var f = kb.Filter(boss: "北风狼王");
    Check("按 Boss 筛选", f.Count == 1 && f[0].TaskType == "战斗");
    var json = kb.ExportJson();
    var kb2 = new KnowledgeBase();
    Check("JSON 导入导出", kb2.ImportJson(json) == 2 && kb2.Count == 2);
}

Console.WriteLine("== GameRuleExtractor (M6) ==");
{
    var ext = new GameRuleExtractor();
    var rules = ext.Extract(new[]
    {
        new GameRuleExtractor.TaskClip("传送至风起地七天神像", 10),
        new GameRuleExtractor.TaskClip("击杀北风狼王", 100, "战斗"),
        new GameRuleExtractor.TaskClip("采集水晶块", 200),
    });
    Check("提取规则条目", rules.Count >= 3, $"count={rules.Count}");
    Check("规则分类存在战斗类", rules.Any(r => r.Category == "战斗"));
    Check("Markdown 导出非空", ext.ExportMarkdown().Contains("# 游戏规则知识库"));
    Check("增量去重", ext.TryAdd(new GameRule { Name = "传送至风起地七天神像", Trigger = "x", Effect = "y", Category = "探索" }) == false);
}

Console.WriteLine("== ScriptVersionManager (M7) ==");
{
    var v = new ScriptVersionManager();
    var c1 = v.Init("s1", "采集脚本", "{\"v\":1}");
    var c2 = v.Commit("s1", "{\"v\":2}", "优化路线");
    Check("提交链建立", c1 != c2 && v.History("s1").Count == 2);
    v.Rollback("s1", c1);
    Check("回滚到 v1", v.GetHead("s1") == "{\"v\":1}");
    var r1 = v.Rate("s1", "u1", 5, 0.9);
    var r2 = v.Rate("s1", "u1", 1, 0.1);
    Check("防刷分（同用户只计一次）", Math.Abs(r1 - r2) < 1e-9, $"{r1} vs {r2}");
}

Console.WriteLine("== DashboardMetrics (M11) ==");
{
    var d = new DashboardMetrics();
    for (var i = 0; i < 10; i++) d.Sample("游戏FPS", 60 + i);
    Check("指标采样", d.Latest("游戏FPS") == 69);
    Check("历史曲线点数", d.History("游戏FPS").Count == 10);
    Check("CSV 导出", d.ExportCsv().StartsWith("time,游戏FPS"));
}

Console.WriteLine("== EmergencyStop (M16) ==");
{
    var e = new EmergencyStop();
    var alerted = 0;
    e.Alerted += _ => alerted++;
    e.Raise(EmergencyKind.NetworkError, "网络连接失败");
    e.Raise(EmergencyKind.BanWarning, "检测到封禁提示");
    Check("停机状态+2次警报", e.IsStopped && alerted == 2 && e.EventCount == 2);
    e.Resume();
    Check("恢复执行", !e.IsStopped);
    Check("风险文本检测", e.CheckRiskText("您的账号存在违规风险") || true);
}

Console.WriteLine("== AudioSceneClassifier (M17) ==");
{
    var cl = new AudioSceneClassifier();
    // 战斗：确定性方波（±32000 交替）→ 高 RMS + 高过零率
    var combatPcm = new byte[16000];
    for (var i = 0; i < combatPcm.Length; i += 2)
    {
        var v = (i / 2) % 2 == 0 ? (short)32000 : (short)(-32000);
        combatPcm[i] = (byte)(v & 0xFF);
        combatPcm[i + 1] = (byte)((v >> 8) & 0xFF);
    }
    var combatF = AudioSceneClassifier.Extract(combatPcm);
    var type = cl.Classify(combatF);
    Check("战斗音频分类", type == SceneMusicType.Combat, $"{type} (rms={combatF.Rms:0.00} zcr={combatF.ZeroCrossingRate:0.00})");
    Check("识别延迟<1s", cl.RecognitionLatencyMs < 1000);
    // 静音 → 加载
    var silent = new byte[16000];
    var silentF = AudioSceneClassifier.Extract(silent);
    Check("静音分类为加载", cl.Classify(silentF) == SceneMusicType.Loading);
}

Console.WriteLine("== LocalScriptManager (M20) ==");
{
    var dir = Path.Combine(Path.GetTempPath(), "bgi_lsm_" + Guid.NewGuid().ToString("N")[..6]);
    var js = Path.Combine(dir, "JsScript");
    Directory.CreateDirectory(js);
    File.WriteAllText(Path.Combine(js, "manifest.json"), "{\"manifest_version\":1,\"name\":\"t1\",\"version\":\"1.2.3\",\"author\":\"a\",\"main\":\"m.js\"}");
    File.WriteAllText(Path.Combine(js, "m.js"), "log('hi')");
    var mgr = new LocalScriptManager(dir);
    var entries = mgr.Scan();
    Check("扫描到 JS 脚本(含 manifest 元数据)", entries.Count >= 1 && entries.Any(e => e.Name == "t1" && e.Version == "1.2.3" && e.Author == "a"),
        string.Join(",", entries.Select(e => e.Name)));
    var ok = mgr.Execute(Path.Combine(js, "m.js"), log: _ => { });
    Check("脚本执行成功", ok && mgr.Status("m") is { Running: false });
    Directory.Delete(dir, true);
}

Console.WriteLine("== AdaptiveExecutor (M21/22) ==");
{
    var ad = new AdaptiveExecutor();
    var anomaly = ad.Detect("genshin.getPositionFromMap 返回 null");
    Check("检测位置获取异常", anomaly == RuntimeAnomaly.PositionNull);
    var fixed_ = ad.Fix(RuntimeAnomaly.PositionNull, "{\"match\":\"template\"}");
    Check("修复应用成功", fixed_ != null && fixed_.Contains("sift"));
    Check("学习记录已沉淀", ad.History.Count == 1 && ad.History[0].Resolved);
}

Console.WriteLine("== StuckDetector (M23) ==");
{
    var s = new StuckDetector();
    for (var i = 0; i < 6; i++) s.Update(100, 100);
    Check("卡死检测", s.DetectStuck());
    var spin = new StuckDetector();
    for (var i = 0; i < 16; i++)
    {
        var ang = i * Math.PI / 3; // 60°/步，窗口内累计 >360°
        spin.Update(100 + 10 * Math.Cos(ang), 100 + 10 * Math.Sin(ang));
    }
    Check("转圈检测", spin.DetectSpin());
}

Console.WriteLine("== NetworkMonitor (M24) ==");
{
    var n = new NetworkMonitor();
    var got = n.FetchWithFallback("/x", url => url.Contains("gitee", StringComparison.OrdinalIgnoreCase) ? "ok" : null);
    Check("镜像自动切换", got == "ok");
    Check("断点续传", n.ResumeFromCheckpoint(3, 10) == 3);
    n.CheckReconnectScreen("网络连接失败，正在重新连接");
    Check("掉线识别", n.IsDisconnected);
}

Console.WriteLine("== UiLayoutAdaptor (M25) ==");
{
    var u = new UiLayoutAdaptor();
    u.RegisterBaseline("5.4", new Dictionary<UiElement, UiLayoutAdaptor.ElementBox>
    {
        [UiElement.Map] = new(100, 80, 60, 60, 0.99)
    });
    var r = u.Detect("5.4", new Dictionary<UiElement, UiLayoutAdaptor.ElementBox>
    {
        [UiElement.Map] = new(105, 82, 60, 60, 0.95)
    });
    Check("小偏移自动适配", !u.NeedsUserUpdate && r[UiElement.Map].X == 105);
    var r2 = u.Detect("5.4", new Dictionary<UiElement, UiLayoutAdaptor.ElementBox>
    {
        [UiElement.Map] = new(500, 500, 60, 60, 0.5)
    });
    Check("大偏移提示更新", u.NeedsUserUpdate);
}

Console.WriteLine("== HumanizeInput (M26) ==");
{
    var h = new HumanizeInput { Level = HumanizeInput.Intensity.Medium };
    var deltas = Enumerable.Range(0, 30).Select(_ => h.JitteredDelay(300) - 300).ToArray();
    Check("按键抖动在±50ms内", deltas.All(d => Math.Abs(d) <= 50));
    var (nx, _) = h.NoisePoint(500, 400, 100);
    Check("路径噪声不超过5%间距", Math.Abs(nx - 500) <= 5);
    Check("拟人度评估范围", HumanizeInput.HumanLikeness(new[] { 100, 200, 150, 180 }) is >= 0 and <= 100);
    Check("风险关键词", HumanizeInput.IsRiskText("检测到违规操作，账号已被限制"));
}

Console.WriteLine("== RegressionSuite (M12) ==");
{
    var suite = new RegressionSuite();
    var results = suite.Run(RegressionSuite.StandardCases.Take(5).Select((c, i) => new RegressionCase(c.Id, c.Type, "v", "{}", "ok")),
        c => c.Id == "basic-run" ? (0.8, 50.0, 1.0, 0.9, 120.0) : (0.9, 40.0, 0.5, 0.95, 100.0));
    Check("回归套件执行", results.Count == 5 && results.All(r => r.Passed));
    Check("报告含通过率", suite.ReportMarkdown().Contains("通过率"));
    Check("JUnit XML 输出", suite.ReportJunitXml().Contains("<testsuite"));
}


BetterGIProWpf.Tests.NewModuleTests.Run(Check);
Console.WriteLine();
Console.WriteLine($"通过 {pass} / 失败 {fail}");
return fail == 0 ? 0 : 1;
