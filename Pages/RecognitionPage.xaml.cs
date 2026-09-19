using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.NitroGen;
using BetterGIProWpf.Services.Recognition;
using BetterGIProWpf.Services.EndToEnd;
using BetterGIProWpf.Services;

namespace BetterGIProWpf.Pages;

public partial class RecognitionPage : Page
{
    public RecognitionPage() { InitializeComponent(); }

    private void Append(string s) { ResultText.Text += s + Environment.NewLine; ResultText.ScrollToEnd(); }
    private void Reset() => ResultText.Text = "";

    private void Template_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        var img = new byte[64 * 64]; var tmpl = new byte[16 * 16];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) img[y * 64 + x] = 40;
        for (int y = 20; y < 36; y++) for (int x = 24; x < 40; x++) img[y * 64 + x] = 220;
        for (int i = 0; i < tmpl.Length; i++) tmpl[i] = 220;
        var matches = TemplateMatching.MultiScale(img, 64, 64, tmpl, 16, 16, 0.8f, 1.2f, 0.85f, 3);
        Append("【多尺度模板匹配】");
        if (matches.Count > 0) { var m = matches[0]; Append($"命中：({m.X},{m.Y}) 缩放 {m.Scale:0.00} 分 {m.Score:0.000} ✓"); }
        else Append("未命中");
        Append("");
    }

    private void Rotate_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        var tmpl = new byte[24 * 24];
        for (int y = 4; y < 20; y++) for (int x = 4; x < 20; x++) tmpl[y * 24 + x] = 200;
        for (int y = 4; y < 8; y++) for (int x = 4; x < 20; x++) tmpl[y * 24 + x] = 60;
        var rotated = TemplateMatching.RotateGray(tmpl, 24, 24, 90);
        var matches = TemplateMatching.RotationInvariant(rotated, 24, 24, tmpl, 24, 24, 15, 0.6f, 5);
        Append("【旋转不变匹配】");
        foreach (var m in matches.Take(3)) Append($"角度 {m.AngleDeg:0}° 分 {m.Score:0.000}");
        Append("");
    }

    private void Orb_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        var img = new byte[64 * 64]; var tmpl = new byte[64 * 64];
        for (int i = 0; i < 64 * 64; i++) { img[i] = 80; tmpl[i] = 80; }
        for (int k = 0; k < 8; k++)
        {
            tmpl[32 * 64 + 32 + k] = 240; tmpl[(32 + k) * 64 + 32] = 240;
            img[32 * 64 + 32 + k] = 240; img[(32 + k) * 64 + 32] = 240;
        }
        var fT = TemplateMatching.OrbFeatures(tmpl, 64, 64);
        var fI = TemplateMatching.OrbFeatures(img, 64, 64);
        var matched = TemplateMatching.OrbMatch(fT.ToArray(), fI.ToArray());
        Append("【ORB 特征匹配】");
        Append($"模板 {fT.Count} / 图 {fI.Count} / 匹配 {matched.Count}");
        Append("");
    }

    private void Dtw_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        var fast = new double[] { 0, 0.5, 1.0, 1.6, 2.1, 2.8, 3.2, 3.9, 4.4, 5.0 };
        var slow = fast.Select(t => t * 1.5).ToArray();
        var dtw = Timing.Dtw(fast, slow);
        Append("【DTW 时序对齐】");
        Append($"DTW 距离 {dtw.Distance:0.000} · 路径长度 {dtw.Path.Count}");
        Append(RecognitionPipeline.AlignDemo(fast, slow));
        Append("");
    }

    private void Overlay_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        var keyTemplates = new Dictionary<string, byte[]>();
        keyTemplates["A"] = IconTemplate(24, 24, 240);
        keyTemplates["Q"] = IconTemplate(24, 24, 190);
        var frames = new List<RgbFrame>(); var fps = 10.0;
        for (int fi = 0; fi < 25; fi++)
        {
            double t = fi / fps;
            frames.Add(MakeOverlayFrame(128, 72, t >= 0.2 && t <= 1.0, t >= 1.2 && t <= 2.0));
        }
        var extractor = new KeyOverlay.KeyTimelineExtractor(0.7f, 120f,
            new Dictionary<string, (int X, int Y)> { ["A"] = (8, 8), ["Q"] = (40, 8) });
        var events = extractor.Extract(frames.ToArray(), fps, keyTemplates, 24, 24);
        var cmds = BattleScriptGenerator.FromKeyTimeline(events);
        Append("【通道A：悬浮窗→战斗脚本】");
        Append($"按键事件 {events.Count} 个，战斗指令 {cmds.Count} 条");
        Append(BattleScriptGenerator.ToBattleScriptTxt(cmds));
        try { Append("已写入: " + BattleScriptGenerator.SaveToAutoFight(OutRootBox.Text.Trim(), "演示", cmds)); }
        catch (Exception ex) { Append("写入失败: " + ex.Message); }
        Append("");
    }

    private void ChannelB_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        var f1 = MakeChannelBFrame(320, 180, -100, -100);
        var f2 = MakeChannelBFrame(320, 180, 150, 80);
        var result = RecognitionPipeline.RunChannelB(new[] { f1, f2 });
        Append("【通道B：无悬浮窗动作推断】");
        Append(result.Report);
        Append("");
    }

    private void Slot_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        var yolo = new Adapters.YoloAdapter();
        var f = MakeChannelBFrame(320, 180, 150, 80);
        var boxes = yolo.Detect(f);
        Append("【适配器插槽自检】");
        Append($"YOLO: {(yolo.OnnxAvailable ? "ONNX 就位" : "本地兜底")}");
        Append($"关键短语: {string.Join("、", Adapters.WhisperAdapter.ExtractKeyPhrases("先开盾，然后接Q"))}");
        Append("");
    }

    private void Bridge_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        var bridge = new NitroGenBridge();
        Append("【NitroGen 桥接】");
        Append($"checkpoint: {(bridge.CheckpointAvailable ? "就位" : "未就位")}");
        Append($"一键启动: {bridge.StartupCommand}");
        Append("");
    }

    private void Guard_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        var guard = new RuntimeGuard();
        var block = new ActionBlock();
        block.Data[4 + NitroGenConst.Btn.A] = 1f;
        var cur = new float[32 * 18];
        for (int i = 0; i < 32 * 18; i++) cur[i] = i % 7 == 0 ? 200f : 100f;
        for (int i = 0; i < 8; i++) guard.Evaluate(cur, block);
        var v = guard.Evaluate(cur, block);
        Append($"正常: {v.Kind} 新鲜度 {v.Freshness:0.00}");
        var frozen = new float[32 * 18];
        for (int i = 0; i < 32 * 18; i++) frozen[i] = 150;
        for (int i = 0; i < 12; i++) guard.Evaluate(frozen, block);
        var v2 = guard.Evaluate(frozen, block);
        Append($"冻结: {v2.Kind} 新鲜度 {v2.Freshness:0.00}");
        Append("");
    }

    private void Planner_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        Append("【规划器守护】");
        var battle = PlannerGuard.Bind("帮我打Boss，用元素战技", PlannerGuard.Phase.Battle);
        Append("战斗: " + string.Join(" → ", battle));
        Append("");
    }

    private void Dataset_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        Append("【数据集导入】");
        var json = "{\"samples\":[{\"t\":0.0,\"action\":[0.5,0.5,0,0,1]},{\"t\":0.5,\"action\":[0.5,0.5,0,0,0]}]}";
        var samples = DatasetImporter.ParseJson(json);
        Append($"解析 {samples.Count} 条");
        Append("");
    }

    private async void DualTrack_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        Append("【双轨联动】");
        var agent = new MockEndToEndAgent(); agent.Load();
        var exec = new HybridExecutor(agent, new KeyboardVirtualGamepad()) { Mode = BetterGIProWpf.Services.EndToEnd.TrackMode.Hybrid };
        for (int i = 0; i < 3; i++)
        {
            var frame = MakeChannelBFrame(320, 180, 150, 80);
            var out_ = await agent.InferAsync(frame.Data, 320, 180);
            var track = out_.Confidence < exec.ConfidenceThreshold ? "回退主轨" : "辅轨执行";
            Append($"第{i + 1}帧: 置信 {out_.Confidence:0.00} → {track}");
        }
        Append("");
    }

    private static byte[] IconTemplate(int w, int h, byte v)
    {
        var t = new byte[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        { bool border = x < 2 || y < 2 || x >= w - 2 || y >= h - 2; t[y * w + x] = border ? v : (byte)(v * 4 / 5); }
        return t;
    }

    private static RgbFrame MakeOverlayFrame(int w, int h, bool aDown, bool qDown)
    {
        var data = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++) { data[i * 4] = 30; data[i * 4 + 1] = 30; data[i * 4 + 2] = 40; data[i * 4 + 3] = 255; }
        FillKeyIcon(data, w, 8, 8, 24, aDown ? (byte)240 : (byte)40);
        FillKeyIcon(data, w, 40, 8, 24, qDown ? (byte)190 : (byte)40);
        return new RgbFrame(data, w, h);
    }

    private static void FillKeyIcon(byte[] data, int w, int kx, int ky, int size, byte v)
    {
        for (int y = ky; y < ky + size; y++) for (int x = kx; x < kx + size; x++)
        {
            bool border = x - kx < 2 || y - ky < 2 || x - kx >= size - 2 || y - ky >= size - 2;
            byte val = v <= 40 ? v : border ? v : (byte)(v * 4 / 5);
            int i = (y * w + x) * 4;
            data[i] = val; data[i + 1] = val; data[i + 2] = val;
        }
    }

    private static RgbFrame MakeChannelBFrame(int w, int h, int warmX, int warmY)
    {
        var data = new byte[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int i = (y * w + x) * 4;
            bool warm = x >= warmX && x < warmX + 40 && y >= warmY && y < warmY + 40;
            data[i] = warm ? (byte)220 : (byte)40;
            data[i + 1] = warm ? (byte)60 : (byte)80;
            data[i + 2] = warm ? (byte)50 : (byte)120;
            data[i + 3] = 255;
        }
        return new RgbFrame(data, w, h);
    }
}
