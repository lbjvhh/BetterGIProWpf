using System.Collections.Concurrent;

namespace BetterGIProWpf.Services.Companion;

/// <summary>语音识别接口（模块1：AI 游戏队友）。</summary>
public interface IAsrProvider
{
    Task<string> RecognizeAsync(byte[] pcm16k, int sampleRate);
}

/// <summary>文本转语音接口（模块1/2/10 共用）。</summary>
public interface ITtsProvider
{
    Task SpeakAsync(string text, string voice = "default");
}

public enum CompanionTaskType { Explore, Combat, Gather, Teleport, Follow }
public record CompanionTask(CompanionTaskType Type, string Goal, string? Target = null, string? Action = null);
public record ParsedCommand(CompanionTaskType? TaskType, string? Target, string Raw);

/// <summary>AI 游戏队友（模块1）：半自动/协作模式。</summary>
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

    public async Task HandleVoiceCommandAsync(byte[] pcm16k, int sampleRate)
    {
        if (Asr == null) { Log?.Invoke("未配置语音识别（ASR）"); return; }
        var text = await Asr.RecognizeAsync(pcm16k, sampleRate);
        Log?.Invoke($"[ASR {pcm16k.Length/2d/sampleRate:0.00}s] {text}");
        HandleTextCommand(text);
    }

    public void TakeoverControl()
    {
        Takeover = TakeoverState.Requested;
        Mode = BehaviorMode.Follow;
        CurrentTask = null;
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
                if (ok) { CompletedCount++; Log?.Invoke($"✔ 子任务完成: {task.Goal}"); }
                else { FailedCount++; Log?.Invoke($"✘ 子任务失败: {task.Goal}"); }
            }
            catch (Exception ex) { FailedCount++; Log?.Invoke($"✘ 子任务异常: {ex.Message}"); }
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
        if (ContainsAny(t, "打", "击杀", "消灭", "清怪", "战斗", "打怪")) return new ParsedCommand(CompanionTaskType.Combat, target, t);
        if (ContainsAny(t, "采", "捡", "收集", "拾取")) return new ParsedCommand(CompanionTaskType.Gather, target, t);
        if (ContainsAny(t, "传送", "去", "前往", "到", "锚点", "神像")) return new ParsedCommand(CompanionTaskType.Teleport, target, t);
        if (ContainsAny(t, "探索", "找", "搜")) return new ParsedCommand(CompanionTaskType.Explore, target, t);
        return new ParsedCommand(null, null, t);
    }

    public static string? ExtractTarget(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text, "(?:去|到|前往|打|击杀|采集|传送|找)([\\u4e00-\\u9fa5A-Za-z0-9]{2,12})");
        return m.Success ? m.Groups[1].Value.TrimEnd('的', '那', '这') : null;
    }

    private static bool ContainsAny(string text, params string[] keys) => keys.Any(text.Contains);
    public IReadOnlyList<string> SnapshotLog() { lock (_lock) return _eventLog.ToArray(); }

    public async Task SpeakAsync(string message)
    {
        Announcement?.Invoke(message);
        if (Tts != null) await Tts.SpeakAsync(message);
    }
}
