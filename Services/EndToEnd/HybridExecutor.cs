using BetterGIProWpf.Services.Automation;

namespace BetterGIProWpf.Services.EndToEnd;

public enum TrackMode
{
    TaskTrack, EndToEndTrack, Hybrid,
}

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
                if (current != TrackMode.EndToEndTrack) { TrackSwitched?.Invoke("切换辅轨"); current = TrackMode.EndToEndTrack; }
                _gamepad.SetState(output);
                Log?.Invoke($"辅轨: 置信 {output.Confidence:P0} · {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                if (Mode == TrackMode.Hybrid && current != TrackMode.TaskTrack)
                {
                    TrackSwitched?.Invoke($"置信 {output.Confidence:P0} < 阈值，回退主轨");
                    current = TrackMode.TaskTrack;
                }
                _gamepad.SetState(new GamePadOutput());
            }
            await Task.Delay(interval, token);
        }
    }

    public void Stop() => _cts?.Cancel();
}
