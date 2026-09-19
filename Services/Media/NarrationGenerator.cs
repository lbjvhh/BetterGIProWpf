namespace BetterGIProWpf.Services.Media;

public enum NarrationStyle { Tutorial, BattleReport, Entertainment, Brief }
public record NarrationLine(double StartSec, double EndSec, string Text);

public class NarrationGenerator
{
    public interface ITtsBackend { Task<byte[]> SynthesizeAsync(string text, string voice, string style); }
    public ITtsBackend? Tts { get; set; }
    public IReadOnlyList<string> Voices { get; set; } = new[] { "男", "女" };
    public string SelectedVoice { get; set; } = "男";
    public event Action<string>? Log;
    public record GameEvent(double StartSec, double EndSec, string Kind, string Description);

    public List<NarrationLine> Generate(IReadOnlyList<GameEvent> events, NarrationStyle style)
    {
        var lines = new List<NarrationLine>();
        foreach (var e in events)
        {
            var text = style switch
            {
                NarrationStyle.Tutorial => $"接下来{e.Description}。注意时机。",
                NarrationStyle.BattleReport => $"{e.Kind}，用时 {Math.Round(e.EndSec - e.StartSec)} 秒。",
                NarrationStyle.Entertainment => $"看！{e.Description}，操作丝滑～",
                _ => e.Description
            };
            lines.Add(new NarrationLine(e.StartSec, e.EndSec, text));
        }
        return lines;
    }

    public async Task<List<byte[]>> SynthesizeAsync(IReadOnlyList<NarrationLine> lines, string? voice = null)
    {
        if (Tts == null) return new();
        var v = voice ?? SelectedVoice; var audios = new List<byte[]>();
        foreach (var l in lines) audios.Add(await Tts.SynthesizeAsync(l.Text, v, "narrate"));
        return audios;
    }

    public static string ToSrt(IReadOnlyList<NarrationLine> lines)
    {
        var sb = new System.Text.StringBuilder(); var i = 1;
        foreach (var l in lines) { sb.AppendLine(i++.ToString()); sb.AppendLine($"{Ts(l.StartSec)} --> {Ts(l.EndSec)}"); sb.AppendLine(l.Text); sb.AppendLine(); }
        return sb.ToString();
    }

    private static string Ts(double sec) { var t = TimeSpan.FromSeconds(sec); return $"{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}"; }
}
