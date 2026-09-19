using System.IO;
using BetterGIProWpf.Services.Automation;
using BetterGIProWpf.Services.BetterGI;
using BetterGIProWpf.Services.Calibration;
using BetterGIProWpf.Services.EndToEnd;
using BetterGIProWpf.Services.Evaluation;
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
void Check(string name, bool cond, string? detail = "") { if (cond) { pass++; Console.WriteLine($"  ✔ {name}"); } else { fail++; Console.WriteLine($"  ✘ {name}  {detail}"); } }

Console.WriteLine("== CronExpression ==");
{ var c = new CronExpression("0 3 * * *"); var next = c.Next(new DateTime(2026, 9, 18, 20, 0, 0)); Check("每日03:00", next == new DateTime(2026, 9, 19, 3, 0, 0), next.ToString()); }

Console.WriteLine("== CoordinateCalibration ==");
{ var cal = new CoordinateCalibration(); cal.Fit(new List<CoordinateCalibration.Landmark> { new(0,0,100,200), new(1920,0,2020,200), new(0,1080,100,1280) }); var (x,y) = cal.Transform(0,0); Check("变换", Math.Abs(x-100)<1e-6 && Math.Abs(y-200)<1e-6, $"({x},{y})"); }

Console.WriteLine("== TaskEvaluator ==");
{ var gold = "{\"tasks\":[{\"type\":\"teleport\"}],\"scenes\":[{\"start\":0}]}"; var r = TaskEvaluator.Evaluate(gold, gold); Check("自比100%", r.TaskAccuracy >= 0.99, r.Report); }

Console.WriteLine("== CompanionAgent ==");
{ var p = CompanionAgent.ParseCommand("帮我打这个怪"); Check("战斗", p.TaskType == CompanionTaskType.Combat, p.TaskType.ToString()); }

Console.WriteLine("== StrategyCoach ==");
{ var coach = new StrategyCoach { Frequency = CoachFrequency.High, TriggerThreshold = 0.1 }; var v = new StrategyCoach.VideoContext { Histogram = new double[16], KeyTexts = new[]{"x"}, SceneName = "s", Actions = new List<string>{}, Weaknesses = new Dictionary<string,string>() }; v.Histogram[0] = 1; var list = coach.Analyze(v, new double[16], Array.Empty<string>(), 0.1, "a", "b", new[]{true}); Check("建议≥1", list.Count >= 1, list.Count.ToString()); }

Console.WriteLine("== KnowledgeBase ==");
{ var kb = new KnowledgeBase(); kb.Add(new KnowledgeEntry { VideoSource = "v1", TimestampSec = 45, TaskDescription = "传送到风起地", Location = "风起地", TaskType = "传送" }); var hits = kb.Search("风起地"); Check("检索", hits.Count >= 1, hits.Count.ToString()); }

Console.WriteLine("== EmergencyStop ==");
{ var e = new EmergencyStop(); e.Raise(EmergencyKind.NetworkError, "网络失败"); Check("停机", e.IsStopped); }

Console.WriteLine("== DashboardMetrics ==");
{ var d = new DashboardMetrics(); for (var i=0;i<5;i++) d.Sample("fps", 60+i); Check("最新", d.Latest("fps") == 64); }

Console.WriteLine("== StuckDetector ==");
{ var s = new StuckDetector(); for (var i=0;i<6;i++) s.Update(100,100); Check("卡死", s.DetectStuck()); }

Console.WriteLine("== NetworkMonitor ==");
{ var n = new NetworkMonitor(); var got = n.FetchWithFallback("/x", url => url.Contains("gitee") ? "ok" : null); Check("镜像", got == "ok"); }

Console.WriteLine("== UiLayoutAdaptor ==");
{ var u = new UiLayoutAdaptor(); u.RegisterBaseline("5.4", new Dictionary<UiElement, UiLayoutAdaptor.ElementBox> { [UiElement.Map] = new(100,80,60,60,0.9) }); var r = u.Detect("5.4", new Dictionary<UiElement, UiLayoutAdaptor.ElementBox> { [UiElement.Map] = new(105,82,60,60,0.9) }); Check("自动适配", !u.NeedsUserUpdate); }

BetterGIProWpf.Tests.NewModuleTests.Run(Check);
Console.WriteLine($"\n通过 {pass} / 失败 {fail}");
return fail == 0 ? 0 : 1;
