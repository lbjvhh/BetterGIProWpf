using System.Text;
using BetterGIProWpf.Services.NitroGen;

namespace BetterGIProWpf.Services.Recognition;

public sealed class RecognitionPipeline
{
    public sealed record PipelineResult(string Channel, List<KeyOverlay.KeyEvent> KeyEvents, List<BattleScriptGenerator.Command> Commands, string BattleScript, string Report);

    public static PipelineResult RunChannelA(RgbFrame[] frames, double fps, IReadOnlyDictionary<string, byte[]> keyTemplates, int tw, int th, string userRoot, string scriptName, IReadOnlyDictionary<string, (int X, int Y)>? keyRegions = null)
    {
        var extractor = new KeyOverlay.KeyTimelineExtractor(keyRegions: keyRegions);
        var events = extractor.Extract(frames, fps, keyTemplates, tw, th);
        var cmds = BattleScriptGenerator.FromKeyTimeline(events);
        var path = BattleScriptGenerator.SaveToAutoFight(userRoot, scriptName, cmds);
        return new PipelineResult("A", events, cmds, BattleScriptGenerator.ToBattleScriptTxt(cmds), $"写入 {path}");
    }

    public static PipelineResult RunChannelB(RgbFrame[] frames, Func<RgbFrame[], (string Action, double Duration, double Confidence)>? vlmInfer = null)
    {
        if (vlmInfer != null && frames.Length >= 2)
        {
            var (action, dur, conf) = vlmInfer(frames);
            var cmds = new List<BattleScriptGenerator.Command> { new(action, Math.Max(0.3, dur)) };
            return new PipelineResult("B", new(), cmds, BattleScriptGenerator.ToBattleScriptTxt(cmds), $"VLM {action} {conf:0.00}");
        }
        if (frames.Length >= 2)
        {
            var engine = new LocalVisionEngine();
            var r = engine.Infer(new[] { frames[0], frames[^1] });
            var cmds = new List<BattleScriptGenerator.Command>();
            if (r.Actions.IsButton(0, NitroGenConst.Btn.A)) cmds.Add(new BattleScriptGenerator.Command("attack", 0.8));
            return new PipelineResult("B-local", new(), cmds, BattleScriptGenerator.ToBattleScriptTxt(cmds), $"本地 {r.Mode}");
        }
        return new PipelineResult("none", new(), new(), "", "帧不足");
    }
}
