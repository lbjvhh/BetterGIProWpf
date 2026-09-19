using BetterGIProWpf.Services.Automation;

namespace BetterGIProWpf.Services.EndToEnd;

public enum TrackMode { TaskTrack, EndToEndTrack, Hybrid }

/// <summary>双轨执行器：管理主轨与辅轨的协作与回退。</summary>
public class HybridExecutor
{
    private readonly IEndToEndAgent _agent;
    private readonly IVirtualGamepad _gamepad;
    private CancellationTokenSource? _cts;

    public TrackMode Mode { get; set; } = TrackMode.Hybrid;
    public float ConfidenceThreshold { get; set; } = 0.6f;
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
    public event Action<string>? Log;
    public event Action<string>? TrackSwitched;

    public HybridExecutor(IEndToEndAgent agent, IVirtualGamepad gamepad) { _agent = agent; _gamepad = gamepad; }

    public async Task RunE2ELoopAsync(Func<byte[]?> frameSource, int fps = 15)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var interval = TimeSpan.FromMilliseconds(1000.0 / fps);
        var current = Mode;
        while (!token.IsCancellationRequested)
        {
            var frame = frameSource();
            if (frame == null) { await Task.Delay(100, token); continue; }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var output = await _agent.InferAsync(frame, 480, 270);
            sw.Stop();
            if (Mode == TrackMode.EndToEndTrack || (Mode == TrackMode.Hybrid && output.Confidence >= ConfidenceThreshold))
            {
                if (current != TrackMode.EndToEndTrack) { TrackSwitched?.Invoke("已切换到辅轨（端到端代理执行）"); current = TrackMode.EndToEndTrack; }
                _gamepad.SetState(output);
                Log?.Invoke($"辅轨执行: 置信度 {output.Confidence:P0} · 推理 {sw.ElapsedMilliseconds}ms");
            }
            else if (Mode == TrackMode.Hybrid)
            {
                if (current != TrackMode.TaskTrack) { TrackSwitched?.Invoke($"辅轨置信度 {output.Confidence:P0} < {ConfidenceThreshold:P0}，已回退主轨"); current = TrackMode.TaskTrack; }
                _gamepad.SetState(new GamePadOutput());
                Log?.Invoke($"回退主轨: 置信度 {output.Confidence:P0} 低于阈值");
            }
            else { Log?.Invoke("主轨模式：等待步骤执行器任务"); }
            await Task.Delay(interval, token);
        }
    }

    public void Stop() => _cts?.Cancel();
}
