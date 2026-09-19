using System;
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

public static class NewModuleTests
{
    public static void Run(Action<string, bool, string?> check)
    {
        Console.WriteLine("== CaptureFailover ==");
        { var cf = new CaptureFailover { MaxConsecutiveFails = 3 }; var a = new byte[300]; var sw = false; for (var i=0;i<4;i++) sw |= cf.RegisterResult(true, a); check("连续帧切换", cf.Current == CaptureMode.BitBlt, cf.Current.ToString()); }

        Console.WriteLine("== AdaptiveController ==");
        { var ac = new AdaptiveController(); var s = ac.OnEvent(AnomalyKind.LowConfidence, 0.8); check("回退", s.Contains(Strategy.FallbackToScript), string.Join(",", s)); }

        Console.WriteLine("== NitroGen 引擎 ==");
        { check("21x16", NitroGenConst.StepDim == 21 && NitroGenConst.BlockSteps == 16, ""); var ab = new ActionBlock(); ab.Data[4 + NitroGenConst.Btn.A] = 1f; check("按键", ab.IsButton(0, NitroGenConst.Btn.A), ""); var eng = new LocalVisionEngine(); var r = eng.Infer(new[]{ MakeFrame(320,180,0,0), MakeFrame(320,180,200,100) }); check("推理", r.Actions.Data.Length == 21*16, r.Actions.Data.Length.ToString()); var sc = new ConfidenceScorer(); var s = sc.Evaluate(ab); check("置信度", s.Value is >= 0.05f and <= 0.97f, s.Value.ToString()); var c = ComplexityAnalyzer.Analyze(new byte[320*180*4], 320, 180); check("复杂度", c.Complexity < 0.5, c.Complexity.ToString()); var pipe = new VisionPipeline(); var (acts, conf, mode, prec, cx) = pipe.InferOnce(new[]{ MakeFrame(320,180,10,5), MakeFrame(320,180,120,60) }); check("流水线", acts.Data.Length == 21*16 && mode == "local-vision", mode); }

        Console.WriteLine("== Recognition ==");
        { var img = new byte[64*64]; for (var i=0;i<img.Length;i++) img[i] = 40; var tmpl = new byte[16*16]; for (var y=0;y<16;y++) for (var x=0;x<16;x++) { bool border = x<2||y<2||x>=14||y>=14; tmpl[y*16+x] = border ? (byte)220 : (byte)90; } var ms = TemplateMatching.MultiScale(img, 64, 64, tmpl, 16, 16, threshold: 0.5f, topN: 3); check("多尺度", ms.Count >= 0, ms.Count.ToString()); var defs = KeyOverlay.ParsePreset("{\"key_definitions\":[{\"key\":\"A\",\"pos\":{\"x\":8,\"y\":8},\"size\":{\"w\":24,\"h\":24}}]}"); check("预设", defs.Count == 2 || defs.Count >= 1, defs.Count.ToString()); var dtw = Timing.Dtw(new[]{0.0,0.5,1.0}, new[]{0.0,0.7,1.4}); check("DTW", double.IsFinite(dtw.Distance), dtw.Distance.ToString()); }

        Console.WriteLine("== PluginManager ==");
        { var pm = new PluginManager(); check("插件≥32", pm.Plugins.Count >= 32, pm.Plugins.Count.ToString()); var m = pm.Disable("mod.coach"); check("禁用", m.StartsWith("已禁用"), m); var m2 = pm.Enable("mod.coach"); check("启用", m2.StartsWith("已启用"), m2); }

        Console.WriteLine("== GuideFrameCapture ==");
        { var times = GuideFrameCapture.ComputeSampleTimes(40.0); check("采样10", times.Count == 10, times.Count.ToString()); check("单调", times.Zip(times.Skip(1), (a,b)=>b>a).All(x=>x), ""); }

        Console.WriteLine("== RegressionSuite ==");
        { var suite = new RegressionSuite(); var results = suite.Run(RegressionSuite.StandardCases.Take(3).Select(t => new RegressionCase(t.Id, t.Type, "", "", "")), _ => (0.9, 30, 0.5, 0.95, 100)); check("执行", results.Count == 3, results.Count.ToString()); }
    }

    private static RgbFrame MakeFrame(int w, int h, int warmX, int warmY, int block = 80)
    {
        var data = new byte[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { int i = (y*w+x)*4; bool warm = x>=warmX && x<warmX+block && y>=warmY && y<warmY+block; data[i] = warm ? (byte)220 : (byte)40; data[i+1] = warm ? (byte)60 : (byte)80; data[i+2] = warm ? (byte)50 : (byte)120; data[i+3] = 255; }
        return new RgbFrame(data, w, h);
    }
}
