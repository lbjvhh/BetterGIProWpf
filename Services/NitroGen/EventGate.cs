using System;

namespace BetterGIProWpf.Services.NitroGen;

public sealed class EventGate
{
    public float Threshold { get; }
    public int MinIntervalMs { get; }
    public int MaxIntervalMs { get; }

    private float[]? _lastThumb;
    private long _lastInfer;

    public EventGate(float threshold = 0.08f, int minIntervalMs = 120, int maxIntervalMs = 2000)
    {
        Threshold = threshold; MinIntervalMs = minIntervalMs; MaxIntervalMs = maxIntervalMs;
    }

    public static float Diff(float[]? prev, float[] cur, int tw = 32, int th = 18)
    {
        if (prev is null) return 1f;
        double sum = 0; int n = 0;
        for (int i = 0; i < tw * th; i += 2)
        {
            sum += Math.Abs(prev[i] - cur[i]);
            n++;
        }
        return n == 0 ? 1f : (float)(sum / n / 255.0);
    }

    public sealed record Decision(bool ShouldInfer, string Reason, float Diff);

    public Decision Decide(float[]? thumb, long nowMs)
    {
        float diff = Diff(_lastThumb, thumb!);
        long sinceLast = nowMs - _lastInfer;
        bool shouldInfer = false;
        string reason = "buffered";
        if (diff > Threshold && sinceLast >= MinIntervalMs) { shouldInfer = true; reason = "event"; }
        else if (sinceLast >= MaxIntervalMs) { shouldInfer = true; reason = "heartbeat"; }
        if (shouldInfer) _lastInfer = nowMs;
        _lastThumb = thumb;
        return new Decision(shouldInfer, reason, (float)Math.Round(diff, 4));
    }

    public void Reset() { _lastThumb = null; _lastInfer = 0; }
}

public sealed class ActionBuffer
{
    private ActionBlock? _actions;
    private int _cursor;

    public void Push(ActionBlock actions, long nowMs)
    {
        _actions = actions; _cursor = 0;
    }

    public float[]? Next(long nowMs)
    {
        if (_actions is null || _cursor >= NitroGenConst.BlockSteps) return null;
        var step = _actions.Step(_cursor);
        _cursor++;
        return step;
    }

    public int Remaining => _actions is null ? 0 : NitroGenConst.BlockSteps - _cursor;
    public void Reset() { _actions = null; _cursor = 0; }
}
