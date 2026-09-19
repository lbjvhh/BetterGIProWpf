using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BetterGIProWpf.Services.GuideBrowser;
using BetterGIProWpf.Services.Recognition;
using BetterGIProWpf.Services.Adaptive;
using BetterGIProWpf.Services.Integration;
using BetterGIProWpf.Services.NitroGen;
using BetterGIProWpf.Services.Plugins;
using BetterGIProWpf.Services.Safety;
using BetterGIProWpf.Services.EndToEnd;
using BetterGIProWpf.Services.Evaluation;

namespace BetterGIProWpf.Tests;

/// <summary>
/// 方案一~六章新增服务测试 + 本轮新增：
///  - CaptureFailover（方案 5.1 截图器自动切换）
///  - VersionCompatibility（方案 5.2 版本兼容矩阵）
///  - SecuritySoftwareCheck（方案 5.3 安全软件冲突检测）
///  - AdaptiveController（方案六 中央自适应控制器）
///  - NitroGen 端到端引擎（21×16 动作块 / 门控 / 置信度 / 复杂度 / 熵值 / 数据集录制）
///  - 插件框架（全部功能以插件形式融入）
/// </summary>
public static class NewModuleTests
{
    public static void Run(Action<string, bool, string?> check)
    {
        Console.WriteLine("== CaptureFailover (方案5.1 截图器自动切换) ==");
        {
            var cf = new CaptureFailover { MaxConsecutiveFails = 3 };
            var frameA = new byte[300];
            var frameB = new byte[300];
            for (var i = 0; i < frameB.Length; i++) frameB[i] = (byte)(i % 251);
            var switched = false;
            // 第 1 次建立基线，第 2~4 次为连续相同帧（=3 次失败，触发切换）
            for (var i = 0; i < 4; i++) switched |= cf.RegisterResult(true, frameA);
            check("连续相同帧触发切换", switched && cf.Current == CaptureMode.BitBlt, cf.Current.ToString());
            check("切换历史已记录", cf.SwitchHistory.Count == 1, string.Join(";", cf.SwitchHistory));
            check("帧差异计算", CaptureFailover.MeanDiff(frameA, frameB) > 0.3, CaptureFailover.MeanDiff(frameA, frameB).ToString("0.00"));
            cf.Force(CaptureMode.BitBlt);
            check("手动指定通道", cf.Current == CaptureMode.BitBlt && cf.CurrentAvailable(), cf.Current.ToString());
        }

        Console.WriteLine("== VersionCompatibility (方案5.2 版本矩阵) ==");
        {
            var hits = VersionCompatibility.Match("0.44.4", "5.4").ToList();
            check("命中截图器条目", hits.Any(e => e.RequiresExtraCheck && e.BetterGiVersion == "0.44.4"), string.Join(",", hits.Select(e => e.BetterGiVersion)));
            check("额外截图检查", VersionCompatibility.RequiresExtraStabilityCheck("0.44.4"), "");
            check("版本比较",
                VersionCompatibility.VersionAtLeast("0.63.0", "0.44.4") &&
                !VersionCompatibility.VersionAtLeast("0.40.0", "0.42.0"), "");
            var (ok, _) = VersionCompatibility.ValidateManifestMinVersion(
                System.Text.Json.Nodes.JsonNode.Parse("{\"min_bettergi_version\":\"0.42.0\"}"), "0.44.4");
            check("manifest 最低版本校验", ok, "");
            var (ok2, msg2) = VersionCompatibility.ValidateManifestMinVersion(
                System.Text.Json.Nodes.JsonNode.Parse("{\"min_bettergi_version\":\"0.63.0\"}"), "0.44.4");
            check("低版本拒绝并提示", !ok2 && msg2.Contains("升级"), msg2);
        }

        Console.WriteLine("== SecuritySoftwareCheck (方案5.3 安全软件检测) ==");
        {
            var advice = SecuritySoftwareCheck.BuildAdvice();
            check("检测结果可读", !string.IsNullOrEmpty(advice), advice.Length.ToString());
            check("常见软件识别", SecuritySoftwareCheck.Detect().All(i => !string.IsNullOrEmpty(i.Name)), "");
        }

        Console.WriteLine("== AdaptiveController (方案六 中央自适应控制器) ==");
        {
            var ac = new AdaptiveController();
            var strategies = ac.OnEvent(AnomalyKind.LowConfidence, 0.8);
            check("低置信→回退策略", strategies.Contains(Strategy.FallbackToScript), string.Join(",", strategies));
            ac.AddCheckpoint("node1", 0.3, 0.9, "shot1.png");
            ac.AddCheckpoint("node2", 0.6, 0.85, "shot2.png");
            var rollback = ac.ProgressAwareRollback("node3", 0.7, 0.2); // 对齐度跌破阈值
            check("进度感知回滚", rollback != null && rollback.Node == "node2" && rollback.SnapshotPath == "shot2.png",
                rollback?.Node ?? "null");
            check("回滚后进入恢复态", ac.InRecovery, "");
            ac.ProgressAwareRollback("node3", 0.7, 0.9);
            check("对齐恢复退出恢复态", !ac.InRecovery, "");
            ac.MarkUnreliable(rollback!.Id);
            var r2 = ac.ProgressAwareRollback("node4", 0.75, 0.1);
            check("不可靠检查点被排除", r2 != null && r2.Id != rollback.Id, r2?.Id.ToString() ?? "null");
            check("异常计数", ac.AnomalyCounts[AnomalyKind.LowConfidence] == 1, ac.AnomalyCounts[AnomalyKind.LowConfidence].ToString());
        }

        Console.WriteLine("== NitroGen 引擎 (方案一 NitroGen 兼容端到端) ==");
        {
            // 1) 动作块常量与基础
            check("动作空间 21×16", NitroGenConst.StepDim == 21 && NitroGenConst.BlockSteps == 16, "");
            var ab = new ActionBlock();
            check("空动作块按键熵=1", ab.Entropy() == 1f, ab.Entropy().ToString());
            ab.Data[4 + NitroGenConst.Btn.A] = 1f;            // 步0 A 键
            ab.Data[21 + 4 + NitroGenConst.Btn.B] = 1f;       // 步1 B 键
            check("按键检测", ab.IsButton(0, NitroGenConst.Btn.A) && ab.IsButton(1, NitroGenConst.Btn.B), "");
            check("按键熵下降", ab.Entropy() < 1f, ab.Entropy().ToString());
            check("按键激活度", ab.KeyActivation() > 0 && ab.KeyActivation() < 0.02, ab.KeyActivation().ToString("0.0000"));

            // 2) 本地视觉引擎：暖色帧 → 攻击；运动帧 → 移动
            var f1 = MakeFrame(320, 180, 0, 0);      // 背景帧
            var f2 = MakeFrame(320, 180, 200, 100);  // 暖色块移动到中央偏右（出现敌人）
            var f3 = MakeFrame(320, 180, 130, 60);   // 暖色块再移动（产生运动）
            var eng = new LocalVisionEngine();
            var r1 = eng.Infer(new[] { f1, f2 });
            check("动作块输出维度", r1.Actions.Data.Length == 21 * 16, r1.Actions.Data.Length.ToString());
            check("置信度在范围内", r1.Confidence is >= 0.05f and <= 0.95f, r1.Confidence.ToString("0.00"));
            check("本地模式标记", r1.Mode == "local-vision", r1.Mode);
            var r2 = eng.Infer(new[] { f2, f3 }); // 有运动历史 + 运动
            check("攻击按钮激活(A)", r2.Actions.IsButton(0, NitroGenConst.Btn.A), "");
            // 摇杆运动：块"突然出现"场景（单方向运动不抵消）
            var engM = new LocalVisionEngine();
            var dark = MakeFrame(320, 180, -500, -500);
            _ = engM.Infer(new[] { dark, dark }); // 建立基线
            var rM = engM.Infer(new[] { dark, f2 }); // 暖块在右下方出现 → 摇杆应偏离中心
            check("摇杆已平滑移动", Math.Abs(rM.Actions.LeftX(0) - 0.5f) > 0.01f || Math.Abs(rM.Actions.LeftY(0) - 0.5f) > 0.01f,
                $"({rM.Actions.LeftX(0):0.000},{rM.Actions.LeftY(0):0.000})");
            check("事件事件包含运动", r2.Events is { Motion: > 0 }, r2.Events?.Motion.ToString() ?? "null");

            // 3) 事件门控
            var gate = new EventGate();
            var t0 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var thumbA = new float[32 * 18];
            var thumbB = (float[])thumbA.Clone();
            for (int i = 0; i < 32 * 18 / 2; i++) thumbB[i] = 255; // 一半画面变亮 → 大差异
            var d1 = gate.Decide(thumbA, t0);
            check("首帧触发推理", d1.ShouldInfer && d1.Reason == "event", d1.Reason);
            var d2 = gate.Decide(thumbA, t0 + 500);
            check("相同帧走缓冲", !d2.ShouldInfer && d2.Reason == "buffered", d2.Reason);
            var d3 = gate.Decide(thumbB, t0 + 800);
            check("变化帧触发事件", d3.ShouldInfer && d3.Reason == "event", $"{d3.Reason} diff={d3.Diff}");
            var gate2 = new EventGate(maxIntervalMs: 100);
            _ = gate2.Decide(thumbA, t0); // 建立基线（event）
            var d4 = gate2.Decide(thumbA, t0 + 5000);
            check("心跳强制推理", d4.ShouldInfer && d4.Reason == "heartbeat", d4.Reason);

            // 4) 动作缓冲
            var buf = new ActionBuffer();
            buf.Push(ab, t0);
            int consumed = 0; while (buf.Next(t0) is not null) consumed++;
            check("16 步消费耗尽", consumed == 16 && buf.Next(t0) is null, consumed.ToString());
            check("剩余步数", buf.Remaining == 0, buf.Remaining.ToString());

            // 5) 三维置信度
            var scorer = new ConfidenceScorer();
            var s1 = scorer.Evaluate(new ActionBlock());
            check("空块置信度范围", s1.Value is >= 0.05f and <= 0.97f, s1.Value.ToString("0.000"));
            var act2 = new ActionBlock();
            for (int i = 0; i < 21 * 16; i++) act2.Data[i] = 0.5f;
            act2.Data[4 + NitroGenConst.Btn.A] = 1f;
            var s2 = scorer.Evaluate(act2);
            check("按键块熵更低", s2.Entropy < s1.Entropy, $"{s1.Entropy} vs {s2.Entropy}");

            // 6) 复杂度与精度
            var simple = ComplexityAnalyzer.Analyze(new byte[320 * 180 * 4], 320, 180);
            check("黑白帧低复杂度", simple.Complexity < 0.5, simple.Complexity.ToString("0.000"));
            var pc1 = ComplexityAnalyzer.ChoosePrecision(simple, 0f);
            check("简单场景→INT8", pc1.Precision == ComplexityAnalyzer.Int8, pc1.Precision);
            var pc2 = ComplexityAnalyzer.ChoosePrecision(simple, 0.7f);
            check("高运动→FP32", pc2.Precision == ComplexityAnalyzer.Fp32, pc2.Precision);

            // 7) 熵值审计
            var fixedBeat = EntropyAnalyzer.AnalyzeIntervals(new[] { 35.0, 35, 35, 35, 35 });
            check("固定节拍检测", fixedBeat.FixedBeat && fixedBeat.Advice.Any(a => a.Contains("风控")), string.Join(";", fixedBeat.Advice));
            var human = EntropyAnalyzer.Evaluate(new[] { 120.0, 340, 90, 480, 260, 150, 600, 200 }, new[] { "a", "d", "a", "x", "w", "a", "s", "d", "a" });
            check("人类分布非固定节拍", !human.Intervals.FixedBeat, "");
            check("拟人化评分输出", human.Overall is >= 0 and <= 1, human.Overall.ToString("0.000"));

            // 8) 数据集录制
            var rec = new DatasetRecorder();
            rec.StartRun("test-run");
            rec.AddSample(MakeFrame(64, 36, 30, 10), ab, 0.0);
            rec.AddSample(MakeFrame(64, 36, 90, 20), ab, 0.066);
            rec.EndRun();
            check("录制样本数", rec.Runs[0].Samples.Count == 2, rec.Runs[0].Samples.Count.ToString());
            check("JSON 导出含 action", rec.ExportJson().Contains("action") && rec.ExportJson().Contains("frame"), "");
            var lines = rec.ExportLines(rec.Runs[0]);
            check("训练行导出", lines.Count == 2 && lines[0].Contains("0.000"), lines.Count.ToString());

            // 9) 流水线
            var pipe = new VisionPipeline();
            var (acts, conf, mode, prec, cx) = pipe.InferOnce(new[] { MakeFrame(320, 180, 10, 5), MakeFrame(320, 180, 120, 60) });
            check("流水线推理输出", acts.Data.Length == 21 * 16 && conf > 0 && mode == "local-vision", $"{mode} {conf:0.00} {prec}");
            check("复杂度在范围", cx is >= 0 and <= 1, cx.ToString("0.000"));
        }

        Console.WriteLine("== 识别层 (GitHub 开源整合落地) ==");
        {
            // 按键图标模板：亮边框 + 暗中心（有方差，NCC 可区分按下/常态）
            Func<int, int, byte, byte, byte[]> keyIcon = (w, h, v, inner) =>
            {
                var t = new byte[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool border = x < 2 || y < 2 || x >= w - 2 || y >= h - 2;
                        t[y * w + x] = border ? v : inner;
                    }
                return t;
            };
            // 合成悬浮窗帧：按下 = 绘制图标边框，常态 = 均匀暗色
            Func<int, int, string, bool, string, bool, RgbFrame> overlayFrame = (w, h, k1, p1, k2, p2) =>
            {
                var data = new byte[w * h * 4];
                for (int i = 0; i < w * h; i++)
                {
                    data[i * 4] = 30; data[i * 4 + 1] = 30; data[i * 4 + 2] = 40; data[i * 4 + 3] = 255;
                }
                void Fill(int kx, int ky, int size, byte vv)
                {
                    for (int y = ky; y < ky + size; y++)
                        for (int x = kx; x < kx + size; x++)
                        {
                            int idx = (y * w + x) * 4;
                            data[idx] = vv; data[idx + 1] = vv; data[idx + 2] = vv;
                        }
                }
                void Icon(int kx, int ky, int size, byte vv)
                {
                    for (int y = ky; y < ky + size; y++)
                        for (int x = kx; x < kx + size; x++)
                        {
                            bool border = x - kx < 2 || y - ky < 2 || x - kx >= size - 2 || y - ky >= size - 2;
                            byte v = border ? vv : (byte)(vv * 4 / 5);
                            int idx = (y * w + x) * 4;
                            data[idx] = v; data[idx + 1] = v; data[idx + 2] = v;
                        }
                }
                if (p1) Icon(8, 8, 24, 240); else Fill(8, 8, 24, 40);
                if (p2) Icon(40, 8, 24, 190); else Fill(40, 8, 24, 40);
                return new RgbFrame(data, w, h);
            };

            // 1) multi-scale template matching
            var img = new byte[64 * 64];
            for (int i = 0; i < 64 * 64; i++) img[i] = 40;
            var tmpl = keyIcon(16, 16, 220, 90);
            for (int y = 20; y < 36; y++)
                for (int x = 24; x < 40; x++)
                {
                    bool border = x - 24 < 2 || y - 20 < 2 || x - 24 >= 14 || y - 20 >= 14;
                    img[y * 64 + x] = border ? (byte)220 : (byte)90;
                }
            var ms = TemplateMatching.MultiScale(img, 64, 64, tmpl, 16, 16, minScale: 0.8f, maxScale: 1.2f, threshold: 0.85f, topN: 3);
            check("multi-scale hit", ms.Count > 0 && ms[0].X == 24 && ms[0].Y == 20, ms.Count > 0 ? $"{ms[0].X},{ms[0].Y}" : "none");
            check("IoU calc", ms.Count > 0 && Math.Abs(TemplateMatching.IoU(ms[0], ms[0]) - 1f) < 0.01f, "");

            // 2) rotation-invariant matching
            var t24 = new byte[24 * 24];
            for (int y = 0; y < 24; y++)
                for (int x = 0; x < 24; x++)
                {
                    bool lshape = (x < 2 && y < 22) || (y < 2 && x < 22);
                    t24[y * 24 + x] = lshape ? (byte)200 : (byte)80;
                }
            var rot90 = TemplateMatching.RotateGray(t24, 24, 24, 90);
            var rotM = TemplateMatching.RotationInvariant(rot90, 24, 24, t24, 24, 24, angleStepDeg: 15, threshold: 0.5f, topN: 5);
            check("rot cand 90+-15", rotM.Any(m => Math.Abs(Math.Abs(m.AngleDeg) - 90) <= 15 || Math.Abs(Math.Abs(m.AngleDeg) - 270) <= 15),
                string.Join(",", rotM.Take(3).Select(m => m.AngleDeg.ToString("0"))));
            check("rot dim", rot90.Length == 24 * 24, rot90.Length.ToString());

            // 3) ORB-style feature matching
            var corner = new byte[24 * 24];
            for (int i = 0; i < 24 * 24; i++) corner[i] = 80;
            for (int k = 0; k < 8; k++)
            {
                corner[12 * 24 + 12 + k] = 240;
                corner[(12 + k) * 24 + 12] = 240;
            }
            var fT = TemplateMatching.OrbFeatures(corner, 24, 24);
            var fI = TemplateMatching.OrbFeatures(corner, 24, 24);
            check("feat extract", fT.Count > 0 && fI.Count > 0, $"{fT.Count}/{fI.Count}");
            var orbM = TemplateMatching.OrbMatch(fT.ToArray(), fI.ToArray());
            check("same-image matches", orbM.Count >= 3, orbM.Count.ToString());

            // 4) Input Overlay preset parse
            var defs = KeyOverlay.ParsePreset("""{"key_definitions":[{"key":"A","pos":{"x":8,"y":8},"size":{"w":24,"h":24}},{"key":"Q","pos":{"x":40,"y":8},"size":{"w":24,"h":24}}]}""");
            check("preset parse", defs.Count == 2 && defs[0].Key == "A" && defs[0].W == 24, defs.Count.ToString());

            // 5) key timeline from overlay frames
            var keyT = new Dictionary<string, byte[]> { ["A"] = keyIcon(24, 24, 240, 192), ["Q"] = keyIcon(24, 24, 190, 152) };
            var frames = new List<RgbFrame>();
            for (int fi = 0; fi < 25; fi++)
            {
                double t = fi / 10.0;
                frames.Add(overlayFrame(128, 72, "A", t >= 0.2 && t <= 1.0, "Q", t >= 1.2 && t <= 2.0));
            }
            var evts = new KeyOverlay.KeyTimelineExtractor(0.7f, keyRegions: new Dictionary<string, (int X, int Y)> { ["A"] = (8, 8), ["Q"] = (40, 8) }).Extract(frames.ToArray(), 10.0, keyT, 24, 24);
            check("timeline events", evts.Count >= 4, evts.Count.ToString());
            check("A press at 0.2", evts.Any(e => e.Key == "A" && e.Pressed && Math.Abs(e.TimeSec - 0.2) < 0.15), "");
            check("Q release at 2.0", evts.Any(e => e.Key == "Q" && !e.Pressed && Math.Abs(e.TimeSec - 2.0) < 0.15), "");

            // 6) battle script generation
            var cmds = BattleScriptGenerator.FromKeyTimeline(evts);
            check("cmd mapping", cmds.Count > 0 && cmds.All(c => !string.IsNullOrEmpty(c.Op)), cmds.Count.ToString());
            var txt = BattleScriptGenerator.ToBattleScriptTxt(cmds);
            var bodyLines = txt.Split('\n').Count(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("#"));
            check("TXT script", txt.Contains("# BetterGI") && bodyLines == cmds.Count, $"{bodyLines}/{cmds.Count}");
            check("key map", BattleScriptGenerator.MapKey("E") == "skill" && BattleScriptGenerator.MapKey("Q") == "burst" && BattleScriptGenerator.MapKey("A") == "attack", "");
            var saved = BattleScriptGenerator.SaveToAutoFight(Path.Combine(Path.GetTempPath(), "bgi_auto", "User"), "RecognitionTest", cmds);
            check("save AutoFight", System.IO.File.Exists(saved), saved);

            // 7) DTW alignment
            var fast = new double[] { 0, 0.5, 1.0, 1.6, 2.1, 2.8, 3.2, 3.9, 4.4, 5.0 };
            var slow = fast.Select(x => x * 1.5).ToArray();
            var dtw = Timing.Dtw(fast, slow);
            check("DTW path", dtw.Path.Count >= fast.Length, dtw.Path.Count.ToString());
            check("DTW finite", double.IsFinite(dtw.Distance), dtw.Distance.ToString("0.00"));
            var sd = Timing.SparseDtw(fast, slow);
            check("sparse DTW", sd.Path.Count > 0, sd.Path.Count.ToString());
            var align = Timing.AlignSources(new List<Timing.AlignedSource>
            {
                new("keys", fast.ToList()), new("speech", slow.ToList())
            }, new Timing.AlignedSource("ref", fast.ToList()));
            check("align report", align.Mapped.ContainsKey("keys") && align.Mapped.ContainsKey("speech") && !string.IsNullOrEmpty(align.Report), "");

            // 8) adapter slots
            var yolo = new Adapters.YoloAdapter();
            var wf = MakeFrame(320, 180, 150, 80, 40);
            var boxes = yolo.Detect(wf);
            check("YOLO fallback enemy", boxes.Any(b => b.Class == "enemy"), string.Join(",", boxes.Select(b => b.Class)));
            check("key phrases", Adapters.WhisperAdapter.ExtractKeyPhrases("先开盾，接Q，切人放技能").Count >= 3, "");
            var map = Adapters.MapLocator.Locate(wf, (40, 20, 60, 60));
            check("map locate", map.Confidence >= 0 && map.X is >= 0 and <= 1, $"{map.X:0.00},{map.Y:0.00}");

            // 9) end-to-end pipeline
            var chA = RecognitionPipeline.RunChannelA(frames.ToArray(), 10.0, keyT, 24, 24, Path.Combine(Path.GetTempPath(), "bgi_auto", "User"), "ChannelA", new Dictionary<string, (int X, int Y)> { ["A"] = (8, 8), ["Q"] = (40, 8) });
            check("chA script", chA.Channel == "A-key-overlay" && chA.BattleScript.Contains("BetterGI"), chA.Channel);
            var f1 = MakeFrame(320, 180, -100, -100, 40);
            var f2 = MakeFrame(320, 180, 150, 80, 40);
            var chB = RecognitionPipeline.RunChannelB(new[] { f1, f2 });
            check("chB local", chB.Channel == "B-local" && !string.IsNullOrEmpty(chB.Report), chB.Channel);
            var chBv = RecognitionPipeline.RunChannelB(new[] { f1, f2 }, f => ("attack", 0.8, 0.9));
            check("chB vlm slot", chBv.Channel == "B-vlm-infer" && chBv.Commands[0].Op == "attack", chBv.Channel);
        }

        Console.WriteLine("== 开源整合扩展 (NitroGen桥接/运行时防御/规划器/数据集) ==");
        {
            // 1) NitroGenBridge（serve.py 接口桥接）
            var bridge = new NitroGenBridge();
            var info = bridge.Info();
            check("bridge info", info is not null && !string.IsNullOrEmpty(info.CheckpointPath), info.CheckpointPath);
            check("local fallback infer", bridge.Infer(new[] { MakeFrame(320, 180, 10, 5, 40), MakeFrame(320, 180, 150, 80, 40) }).Mode.Contains("local"), "");
            check("startup cmd", bridge.StartupCommand.Contains("serve.py") || bridge.StartupCommand.Contains("hf download"), "");
            var b64 = NitroGenBridge.ToBase64(MakeFrame(64, 36, 10, 5, 10), 64, 36);
            check("frame to base64", !string.IsNullOrEmpty(b64) && b64.Length > 100, b64.Length.ToString());

            // 2) RuntimeGuard（ActFovea 风格）
            var guard = new RuntimeGuard();
            var block = new ActionBlock();
            block.Data[4 + NitroGenConst.Btn.A] = 1f;
            var frozen = new float[32 * 18];
            for (int i = 0; i < 32 * 18; i++) frozen[i] = 150;
            for (int i = 0; i < 13; i++) guard.Evaluate(frozen, block);
            var vf = guard.Evaluate(frozen, block);
            check("frozen detect", vf.Kind == RuntimeGuard.DisturbanceKind.FrozenObservation && vf.Recoverable, vf.Kind.ToString());
            guard.Reset();
            var prev = new float[32 * 18];
            var cur = new float[32 * 18];
            for (int i = 0; i < 32 * 18; i++) { prev[i] = 100; cur[i] = i % 7 == 0 ? 200f : 100f; }
            check("normal observation", guard.Evaluate(cur, block).Kind == RuntimeGuard.DisturbanceKind.None, "");
            guard.Reset();
            var noisy = new ActionBlock();
            var rnd = new Random(7);
            for (int i = 0; i < noisy.Data.Length; i++) noisy.Data[i] = (float)rnd.NextDouble();
            for (int i = 0; i < 6; i++) guard.Evaluate(i % 2 == 0 ? cur : prev, block);
            var vd = guard.Evaluate(cur, noisy);
            check("drift detect", vd.Kind == RuntimeGuard.DisturbanceKind.ActionDrift, vd.Kind.ToString());

            // 3) PlannerGuard（Harness VLA 风格）
            check("battle bind", PlannerGuard.Bind("帮我打这个Boss，用元素战技", PlannerGuard.Phase.Battle).Any(s => s.Action == "battle"), "");
            check("collect bind", PlannerGuard.Bind("去采集水晶矿", PlannerGuard.Phase.Navigate).Any(s => s.Action == "interact"), "");
            check("vla battle", PlannerGuard.ShouldCallVla(PlannerGuard.Phase.Battle), "");
            check("vla nav off", !PlannerGuard.ShouldCallVla(PlannerGuard.Phase.Navigate), "");
            check("phase classify", PlannerGuard.Classify(true, false, false) == PlannerGuard.Phase.Battle
                && PlannerGuard.Classify(false, true, false) == PlannerGuard.Phase.Idle
                && PlannerGuard.Classify(false, false, true) == PlannerGuard.Phase.Dialogue, "");

            // 4) DatasetImporter（WildWorld / EgoCS-400K）
            var json = """{"samples":[{"t":0.0,"action":[0.5,0.5,0.0,0.0,1.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0],"caption":"普通攻击"},{"t":0.5,"action":[0.5,0.5,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0]}]}""";
            var samples = DatasetImporter.ParseJson(json);
            check("json samples", samples.Count == 2 && samples[0].Action is not null, samples.Count.ToString());
            check("caption parse", samples[0].Caption == "普通攻击", samples[0].Caption ?? "null");
            var lines = DatasetImporter.ToTrainLines(samples);
            check("train lines", lines.Count == 2 && lines[0].Contains("0.000") && lines[0].Contains("PLACEHOLDER"), lines.Count.ToString());
            var plines = DatasetImporter.ParseLines("0.0\tFRAME\t0.5,0.5,0.0,0.0,1.0\n1.0\tFRAME\t0.5,0.5,0.0,0.0,0.0");
            check("lines parse", plines.Count == 2 && plines[1].Action?[4] == 0.0, plines.Count.ToString());
            var tmpIn = Path.Combine(Path.GetTempPath(), "bgi_ds.json");
            File.WriteAllText(tmpIn, json);
            var tmpOut = Path.Combine(Path.GetTempPath(), "bgi_ds_train.txt");
            check("file import", DatasetImporter.ImportFile(tmpIn, tmpOut) == 2 && File.Exists(tmpOut), "");
        }

        Console.WriteLine("== 攻略抓帧 (重复第一秒修复) ==");
        {
            var times = GuideFrameCapture.ComputeSampleTimes(40.0);
            check("采样 10 帧", times.Count == 10, times.Count.ToString());
            check("首帧 0", times[0] == 0, times[0].ToString());
            check("末帧 0.97*dur", Math.Abs(times[^1] - 38.8) < 0.01, times[^1].ToString());
            check("单调递增", times.Zip(times.Skip(1), (a, b) => b > a).All(x => x), "");
            var t0 = GuideFrameCapture.ComputeSampleTimes(0);
            check("无时长 6 帧全 0", t0.Count == 6 && t0.All(x => x == 0), t0.Count.ToString());

            var js = GuideFrameCapture.BuildSeekScript(12.5);
            check("seek 脚本不变式小数", js.Contains("v.currentTime=12.500"), "");
            check("seek 脚本等待到位", js.Contains("Math.abs(v.currentTime-12.500)<0.05"), "");
            var cjs = GuideFrameCapture.BuildCanvasScript(0.75);
            check("canvas 脚本", cjs.Contains("toDataURL('image/jpeg'") && cjs.Contains("0.750"), "");

            var json = """{"ok":true,"data":"data:image/jpeg;base64,/9j/4AAQSkZJRg==","w":640,"h":360}""";
            var tmp = Path.Combine(Path.GetTempPath(), "bgi_frame_check.jpg");
            check("canvas 解析落盘", GuideFrameCapture.TrySaveCanvasFrame(json, tmp) && File.Exists(tmp), "");
            check("canvas 解析失败", !GuideFrameCapture.TrySaveCanvasFrame("""{"ok":false,"err":"cors"}""", tmp), "");

            var a = new byte[32 * 18]; var b = new byte[32 * 18]; var c2 = new byte[32 * 18];
            for (int i = 0; i < a.Length; i++) { a[i] = 100; b[i] = 101; c2[i] = 210; }
            check("重复判定", GuideFrameCapture.IsDuplicate(a, b), "");
            check("差异判定", !GuideFrameCapture.IsDuplicate(a, c2), "");
            check("缩略图差异", GuideFrameCapture.ThumbDiff(a, c2) > 0.4f, GuideFrameCapture.ThumbDiff(a, c2).ToString("0.00"));

            var tf = GuideFrameCapture.ToTimeFrames(new List<double> { 0, 10 }, new List<string> { "x.jpg", "y.jpg" });
            check("时间帧映射", tf.Count == 2 && tf[1].TimeSec == 10 && tf[1].Path == "y.jpg", "");
        }

        Console.WriteLine("== 插件框架 (全部功能以插件形式融入) ==");
        {
            var pm = new PluginManager();
            check("内置插件 ≥ 32", pm.Plugins.Count >= 32, pm.Plugins.Count.ToString());
            check("26 方案模块全覆盖", BuiltinPlugins.CreateAll().Count(p => p.Name.StartsWith("mod.")) == 26,
                BuiltinPlugins.CreateAll().Count(p => p.Name.StartsWith("mod.")).ToString());
            check("核心插件不可禁用", pm.Get("core.localai") is { CanDisable: false }, "");
            check("启用计数", pm.EnabledCount >= 30, pm.EnabledCount.ToString());
            var nitrogen = pm.Get("core.nitrogen");
            check("NitroGen 插件存在", nitrogen is not null, nitrogen?.Name ?? "null");
            var msg = pm.Disable("mod.coach");
            check("禁用可禁用插件", msg.StartsWith("已禁用") && pm.Get("mod.coach")!.Status == PluginStatus.Disabled, msg);
            check("禁用后计数", pm.DisabledCount >= 1, pm.DisabledCount.ToString());
            var msg2 = pm.Enable("mod.coach");
            check("重新启用", msg2.StartsWith("已启用") && pm.Get("mod.coach")!.Status == PluginStatus.Enabled, msg2);
            var deny = pm.Disable("core.localai");
            check("核心插件拒绝禁用", deny.Contains("不可禁用"), deny);
            check("扫描外置幂等", pm.ScanExternal() >= 0, pm.ScanExternal().ToString());
            check("Summary 可读", !string.IsNullOrEmpty(pm.Summary()), pm.Summary());
        }

        Console.WriteLine("== 回归测试集 (5 类基准 + CI) ==");
        {
            var suite = new RegressionSuite();
            var tuples = RegressionSuite.StandardCases;
            check("标准用例 10 个", tuples.Length == 10, tuples.Length.ToString());
            var cases = tuples.Select(t => new RegressionCase(t.Id, t.Type, "", "", "baseline")).ToList();
            var results = suite.Run(cases, cc => (completion: 0.85, deviation: 40.0, sync: 0.8, recovery: 0.9, duration: 120.0));
            check("执行 10 条结果", results.Count == 10, results.Count.ToString());
            check("通过率 >= 0.75", suite.PassRate >= 0.75, suite.PassRate.ToString("0.00"));
            check("Markdown 报告含结论", suite.ReportMarkdown().Contains("回归"), suite.ReportMarkdown().Substring(0, 40));
            var junit = suite.ReportJunitXml();
            check("JUnit XML 可解析", junit.Contains("testsuite"), junit.Substring(0, 60));
        }

        Console.WriteLine("== 仓库直连 (功能模块 19) ==");
        {
            var repo = new Services.Community.ScriptRepoClient();
            check("仓库客户端可实例化", repo.All != null, "");
            var t = repo.RefreshAsync(); t.Wait();
            var r = t.Result;
            check("刷新返回结果", !string.IsNullOrEmpty(r.Message), r.Message);
        }
    }

    /// <summary>合成测试帧：背景色 + 暖色块（位置可控，默认 80×80 → 暖色占比 >9% 触发攻击）</summary>
    private static RgbFrame MakeFrame(int w, int h, int warmX, int warmY, int block = 80)
    {
        var data = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                bool warm = x >= warmX && x < warmX + block && y >= warmY && y < warmY + block;
                data[i] = warm ? (byte)220 : (byte)40;     // R
                data[i + 1] = warm ? (byte)60 : (byte)80;  // G
                data[i + 2] = warm ? (byte)50 : (byte)120; // B
                data[i + 3] = 255;
            }
        }
        return new RgbFrame(data, w, h);
    }
}
