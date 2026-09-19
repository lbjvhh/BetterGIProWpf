namespace BetterGIProWpf.Services.Media;
public enum NarrationStyle { Tutorial, BattleReport, Entertainment, Brief }
public record NarrationLine(double Start, double End, string Text);
public class NarrationGenerator {
    public interface ITts { Task<byte[]> SynthAsync(string text, string voice, string style); }
    public ITts? Tts { get; set; }
    public string Voice { get; set; } = "讲解员-男";
    public event Action<string>? Log;
    public record GameEvent(double Start, double End, string Kind, string Desc);
    public List<NarrationLine> Generate(IReadOnlyList<GameEvent> ev, NarrationStyle st) {
        var l = new List<NarrationLine>();
        foreach (var e in ev) { var t = st switch { NarrationStyle.Tutorial=>$"接下来我们{e.Desc}。注意{e.Kind}时机。", NarrationStyle.BattleReport=>$"任务{e.Kind}，用时{Math.Round(e.End-e.Start)}秒。", NarrationStyle.Entertainment=>$"看！这里{e.Desc}～", _=>e.Desc }; l.Add(new NarrationLine(e.Start,e.End,t)); }
        Log?.Invoke($"{l.Count} 条解说"); return l;
    }
    public async Task<List<byte[]>> SynthAsync(IReadOnlyList<NarrationLine> lines, string? voice=null) {
        if (Tts==null) { Log?.Invoke("未配置 TTS"); return new(); }
        var v = voice ?? Voice; var a = new List<byte[]>();
        foreach (var l in lines) a.Add(await Tts.SynthAsync(l.Text, v, "narrate"));
        return a;
    }
    public static string ToSrt(IReadOnlyList<NarrationLine> lines) { var sb=new System.Text.StringBuilder(); int i=1; foreach(var l in lines){sb.AppendLine(i++.ToString());var s=TimeSpan.FromSeconds(l.Start);var e=TimeSpan.FromSeconds(l.End);sb.AppendLine($"{s.Hours:00}:{s.Minutes:00}:{s.Seconds:00},{s.Milliseconds:000} --> {e.Hours:00}:{e.Minutes:00}:{e.Seconds:00},{e.Milliseconds:000}");sb.AppendLine(l.Text);sb.AppendLine();} return sb.ToString(); }
}
