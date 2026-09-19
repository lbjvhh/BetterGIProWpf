using System.Text.Json;

namespace BetterGIProWpf.Services.Media;

public enum NarrationStyle { Tutorial, BattleReport, Entertainment, Brief }
public record NarrationLine(double StartSec, double EndSec, string Text);

public class NarrationGenerator
{
    public interface ITtsBackend { Task<byte[]> SynthesizeAsync(string text, string voice, string style); }
    public ITtsBackend? Tts { get; set; }
    public IReadOnlyList<string> Voices { get; set; } = new[] { "解说员男", "解说员女" };
    public string SelectedVoice { get; set; } = "解说员男";
    public event Action<string>? Log;

    public record GameEvent(double StartSec, double EndSec, string Kind, string Description);

    public List<NarrationLine> Generate(IReadOnlyList<GameEvent> events, NarrationStyle style)
    {
        var lines = new List<NarrationLine>();
        foreach (var e in events)
        {
            var text = style switch
            {
                NarrationStyle.Tutorial => $"接下来我们{e.Description}。注意{e.Kind}的时机，紧跟视频节奏。",
                NarrationStyle.BattleReport => $"任务执行{e.Kind}，用时 {Math.Round(e.EndSec - e.StartSec)} 秒。",
                NarrationStyle.Entertainment => $"看，这里{e.Description}，操作相当丝滑。",
                _ => e.Description
            };
            lines.Add(new NarrationLine(e.StartSec, e.EndSec, text));
        }
        Log?.Invoke($"已生成 {lines.Count} 条解说词（风格 {style}）");
        return lines;
    }

    public async Task<List<NarrationLine>> GenerateAiAsync(IReadOnlyList<GameEvent> events, NarrationStyle style)
    {
        if (AppConfig.Ai.UseExternal && !string.IsNullOrWhiteSpace(AppConfig.Ai.ApiKey))
        {
            try
            {
                var styleName = style switch
                {
                    NarrationStyle.Tutorial => "教程型：清晰、步骤化、提示关键时机",
                    NarrationStyle.BattleReport => "战报型：简洁、报数据、有节奏感",
                    NarrationStyle.Entertainment => "娱乐型：生动、有情绪、带感叹",
                    _ => "简短型：一句话概括"
                };
                var sys = $"你是游戏视频解说词生成器。风格：{styleName}。输入 JSON 数组含 start/end/kind/description，输出 JSON 数组含 start/end/text，text 20-50 字。只输出 JSON。";
                var input = JsonSerializer.Serialize(events.Select(e => new { start = e.StartSec, end = e.EndSec, kind = e.Kind, description = e.Description }));
                var raw = await AppState.Ai.ChatAsync(sys, input);
                raw = raw.Trim();
                if (raw.StartsWith("```")) { raw = raw.Split('\n', 2)[1..][0]; raw = raw.TrimEnd('`').Trim(); }
                var arr = JsonSerializer.Deserialize<List<NarrationDto>>(raw) ?? new();
                return arr.Select(d => new NarrationLine(d.start, d.end, d.text ?? "")).ToList();
            }
            catch (Exception ex) { Log?.Invoke("LLM 解说失败，回退本地：" + ex.Message); }
        }
        return Generate(events, style);
    }

    private class NarrationDto { public double start { get; set; } public double end { get; set; } public string? text { get; set; } }

    public async Task<List<byte[]>> SynthesizeAsync(IReadOnlyList<NarrationLine> lines, string? voice = null)
    {
        if (Tts == null) { Log?.Invoke("未配置 TTS 后端"); return new(); }
        var v = voice ?? SelectedVoice;
        var audios = new List<byte[]>();
        foreach (var l in lines) audios.Add(await Tts.SynthesizeAsync(l.Text, v, "narrate"));
        Log?.Invoke($"已合成 {audios.Count} 段音频（音色 {v}）");
        return audios;
    }

    public static string ToSrt(IReadOnlyList<NarrationLine> lines)
    {
        var sb = new System.Text.StringBuilder();
        var i = 1;
        foreach (var l in lines)
        {
            sb.AppendLine(i++.ToString());
            sb.AppendLine($"{Ts(l.StartSec)} --> {Ts(l.EndSec)}");
            sb.AppendLine(l.Text); sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Ts(double sec)
    {
        var t = TimeSpan.FromSeconds(sec);
        return $"{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
    }
}
