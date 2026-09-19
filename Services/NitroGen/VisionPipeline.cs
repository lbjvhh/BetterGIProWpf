namespace BetterGIProWpf.Services.NitroGen;

public sealed class VisionPipeline
{
    private readonly LocalVisionEngine _engine = new();
    private readonly EventGate _gate = new();
    private readonly ActionBuffer _buffer = new();
    private readonly ConfidenceScorer _scorer = new();
    public string Mode { get; private set; } = "idle";
    public float LastConfidence { get; private set; }
    public float LastComplexity { get; private set; }
    public string LastPrecision { get; private set; } = ComplexityAnalyzer.Int8;
    public int InferCount { get; private set; }

    public sealed record StepOut(ActionBlock? Block, float[]? NextAction, string Mode, float Confidence, string GateReason, string Precision);

    public StepOut Process(RgbFrame frame)
    {
        var thumb = LocalVisionEngine.ToGrayThumb(frame);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var decision = _gate.Decide(thumb, now);
        if (decision.ShouldInfer)
        {
            var infer = _engine.Infer(new[] { frame, frame });
            var conf = _scorer.Evaluate(infer.Actions);
            LastConfidence = conf.Value;
            var metrics = ComplexityAnalyzer.Analyze(frame.Data, frame.Width, frame.Height);
            LastComplexity = metrics.Complexity;
            var pc = ComplexityAnalyzer.ChoosePrecision(metrics, infer.Events?.Motion ?? 0f);
            LastPrecision = pc.Precision;
            _buffer.Push(infer.Actions, now);
            InferCount++;
            return new StepOut(infer.Actions, _buffer.Next(now), infer.Mode, conf.Value, decision.Reason, pc.Precision);
        }
        return new StepOut(null, _buffer.Next(now), Mode, LastConfidence, decision.Reason, LastPrecision);
    }

    public void Reset() { _engine.Reset(); _gate.Reset(); _buffer.Reset(); _scorer.Reset(); Mode = "idle"; InferCount = 0; }
}
