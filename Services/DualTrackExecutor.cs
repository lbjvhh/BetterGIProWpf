namespace BetterGIProWpf.Services;

public enum TrackMode { Primary, Secondary, Auto }

public class DualTrackExecutor
{
    private readonly StepExecutor _primary = new();
    private readonly IEndToEndPolicy? _policy;
    private readonly IGameAdapter _adapter;
    private CancellationTokenSource? _cts;

    public TrackMode Mode { get; set; } = TrackMode.Primary;
    public double FallbackThreshold { get; set; } = 0.6;

    public DualTrackExecutor(IGameAdapter adapter, IEndToEndPolicy? policy = null)
    {
        _adapter = adapter; _policy = policy;
    }

    public async Task RunAsync(IList<OperationStep> primarySteps, Action<string> log)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (Mode == TrackMode.Primary || _policy == null || !_policy.IsLoaded)
        {
            log("主轨模式：执行结构化任务序列");
            await _primary.RunAsync(primarySteps, (i, _) => log($"主轨步骤 {i + 1}"), () => log("主轨完成"));
            return;
        }

        log("辅轨模式：端到端策略实时推理");
        while (!token.IsCancellationRequested)
        {
            var frame = await _adapter.CaptureGameFrameAsync();
            var pad = await _policy.InferAsync(frame, token);
            if (Mode == TrackMode.Auto && pad.Confidence < FallbackThreshold)
            {
                log($"辅轨置信度 {pad.Confidence:F2} < 阈值 {FallbackThreshold}，回退主轨");
                await _primary.RunAsync(primarySteps, (i, _) => log($"主轨步骤 {i + 1}"), () => log("主轨完成"));
                return;
            }
            _adapter.ApplyPad(pad);
            await Task.Delay(33, token);
        }
    }

    public void Stop() { _cts?.Cancel(); _primary.Stop(); }
}
