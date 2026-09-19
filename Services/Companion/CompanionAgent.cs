using System.Collections.Concurrent;

namespace BetterGIProWpf.Services.Companion;

public interface IAsrProvider { Task<string> RecognizeAsync(byte[] pcm16k, int sampleRate); }
public interface ITtsProvider { Task SpeakAsync(string text, string voice = "default"); }

public enum CompanionTaskType { Explore, Combat, Gather, Teleport, Follow }
public record CompanionTask(CompanionTaskType Type, string Goal, string? Target = null, string? Action = null);
public record ParsedCommand(CompanionTaskType? TaskType, string? Target, string Raw);

public class CompanionAgent
{
    public enum BehaviorMode { Follow, Autonomous }
    public enum TakeoverState { None, Requested, Active }

    private readonly ConcurrentQueue<CompanionTask> _taskQueue = new();
    private readonly List<string> _eventLog = new();
    private readonly object _lock = new();

    public BehaviorMode Mode { get; private set; } = BehaviorMode.Follow;
    public TakeoverState Takeover { get; private set; } = TakeoverState.None;
    public CompanionTask? CurrentTask { get; private set; }
    public int CompletedCount { get; private set; }
    public int FailedCount { get; private set; }
    public bool IsBusy => CurrentTask != null;
    public IAsrProvider? Asr { get; set; }
    public ITtsProvider? Tts { get; set; }
    public Func<string, Task<bool>>? TaskExecutor { get; set; }

    public event Action<string>? Log;
    public event Action<string>? Announcement;

    public void HandleTextCommand(string text)
    {
        var parsed = ParseCommand(text);
        if (parsed.TaskType == null) { Log?.Invoke($"未能理解指令: {text}"); return; }
        var task = new CompanionTask(parsed.TaskType.Value, text, parsed.Target);
        _taskQueue.Enqueue(task);
        Log?.Invoke($"已接受指令 → {task.Type}: {text}");
        _ = RunLoopAsync();
    }

    public async Task<ParsedCommand> ParseCommandAsync(string text)
    {
        if (AppConfig.Ai.UseExternal && !string.IsNullOrWhiteSpace(AppConfig.Ai.ApiKey) && !string.IsNullOrWhiteSpace(text))
        {
            try
            {
                var sys = "你是原神游戏队友的意图解析器。把玩家指令解析为 JSON：{\"taskType\":\"Explore|Combat|Gather|Teleport|Follow\",\"target\":\"目标对象或地点\"}。若无法识别，taskType 为 null。只输出 JSON。";
                var raw = await AppState.Ai.ChatAsync(sys, text);
                raw = raw.Trim();
                if (raw.StartsWith("```")) { raw = raw.Split('\n', 2)[1..][0]; raw = raw.TrimEnd('`').Trim(); }
                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.TryGetProperty("taskType", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var s = t.GetString();
                    if (Enum.TryParse<CompanionTaskType>(s, true, out var tt))
                    {
                        var target = root.TryGetProperty("target", out var gt) ? gt.GetString() : null;
                        return new ParsedCommand(tt, target, text);
                    }
                }
            }
            catch { }
        }
        return ParseCommand(text);
    }

    public async Task HandleVoiceCommandAsync(byte[] pcm16k, int sampleRate)
    {
        if (Asr == null) { Log?.Invoke("未配置语音识别（ASR）"); return; }
        var text = await Asr.RecognizeAsync(pcm16k, sampleRate);
        Log?.Invoke($"[ASR {pcm16k.Length / 2d / sampleRate:0.00}s] {text}");
        HandleTextCommand(text);
    }

    public void TakeoverControl()
    {
        Takeover = TakeoverState.Requested; Mode = BehaviorMode.Follow; CurrentTask = null;
        while (_taskQueue.TryDequeue(out _)) { }
        Log?.Invoke("玩家接管控制权，AI 队友已停止当前动作并切换为跟随");
    }

    public void SwitchMode(BehaviorMode mode) { Mode = mode; Log?.Invoke($"行为模式切换为 {mode}"); }
    public double SuccessRate => CompletedCount + FailedCount == 0 ? 1.0 : (double)CompletedCount / (CompletedCount + FailedCount);

    private async Task RunLoopAsync()
    {
        while (_taskQueue.TryDequeue(out var task))
        {
            if (Takeover != TakeoverState.None) return;
            CurrentTask = task;
            Log?.Invoke($"开始执行子任务: {task.Goal}");
            try
            {
                var ok = TaskExecutor == null ? ExecuteFallback(task) : await TaskExecutor(task.Goal);
                if (ok) { CompletedCount++; Log?.Invoke($"✔ 子任务完成 {task.Goal}"); }
                else { FailedCount++; Log?.Invoke($"✘ 子任务失败 {task.Goal}"); }
            }
            catch (Exception ex) { FailedCount++; Log?.Invoke($"✘ 子任务异常 {ex.Message}"); }
            finally { CurrentTask = null; }
        }
    }

    private bool ExecuteFallback(CompanionTask task) { Log?.Invoke($"[内置执行器] {task.Type} → {task.Goal}"); return true; }

    public static ParsedCommand ParseCommand(string text)
    {
        var t = text.Trim();
        if (string.IsNullOrEmpty(t)) return new ParsedCommand(null, null, t);
        var target = ExtractTarget(t);
        if (ContainsAny(t, "跟随", "跟着我", "跟紧")) return new ParsedCommand(CompanionTaskType.Follow, target, t);
        if (ContainsAny(t, "打", "出击", "消灭", "清理", "战斗", "打怪")) return new ParsedCommand(CompanionTaskType.Combat, target, t);
        if (ContainsAny(t, "采", "捡", "收集", "拿取")) return new ParsedCommand(CompanionTaskType.Gather, target, t);
        if (ContainsAny(t, "传送", "去", "前往", "到", "锚点", "神像")) return new ParsedCommand(CompanionTaskType.Teleport, target, t);
        if (ContainsAny(t, "探索", "找", "寻")) return new ParsedCommand(CompanionTaskType.Explore, target, t);
        return new ParsedCommand(null, null, t);
    }

    public static string? ExtractTarget(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text, "[\"「『]([^\"」』]+)[\"」』]");
        if (m.Success) return m.Groups[1].Value;
        m = System.Text.RegularExpressions.Regex.Match(text, "(?:去|到|前往|找|出击|采集|传送)[\\u4e00-\\u9fa5A-Za-z0-9]{2,12}");
        return m.Success ? m.Value.TrimStart('去', '到', '找', '传', '前').TrimEnd('的', '那', '边') : null;
    }

    private static bool ContainsAny(string text, params string[] keys) => keys.Any(text.Contains);
    public IReadOnlyList<string> SnapshotLog() { lock (_lock) return _eventLog.ToArray(); }

    public async Task SpeakAsync(string message)
    {
        Announcement?.Invoke(message);
        if (Tts != null) await Tts.SpeakAsync(message);
    }
}
