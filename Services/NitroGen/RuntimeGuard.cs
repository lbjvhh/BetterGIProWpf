using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterGIProWpf.Services.NitroGen;

public sealed class RuntimeGuard
{
    public enum DisturbanceKind { None, FrozenObservation, VisualLatency, ActionDrift, PatchDamage }
    public sealed record Verdict(DisturbanceKind Kind, bool Recoverable, float Freshness, float Drift, string Advice);

    private readonly LinkedList<float[]> _recentThumbs = new();
    private readonly LinkedList<float> _recentEntropy = new();
    private const int WindowSize = 12;

    public Verdict Evaluate(float[] thumb, ActionBlock proposed, float[]? prevThumb = null)
    {
        _recentThumbs.AddLast((float[])thumb.Clone());
        while (_recentThumbs.Count > WindowSize) _recentThumbs.RemoveFirst();
        float freshness = 1f;
        if (_recentThumbs.Count >= 2)
        {
            var last = _recentThumbs.Last!.Previous!.Value;
            freshness = EventGate.Diff(last, thumb);
        }
        float entropy = proposed.Entropy();
        _recentEntropy.AddLast(entropy);
        while (_recentEntropy.Count > WindowSize) _recentEntropy.RemoveFirst();
        float drift = 0f;
        if (_recentEntropy.Count >= 3)
        {
            double mean = _recentEntropy.Take(_recentEntropy.Count - 1).Average();
            drift = (float)Math.Abs(entropy - mean) / Math.Max(0.001f, (float)mean);
        }
        DisturbanceKind kind;
        bool recoverable;
        string advice;
        if (freshness < 0.05f)
        {
            kind = _recentThumbs.Count >= WindowSize ? DisturbanceKind.FrozenObservation : DisturbanceKind.VisualLatency;
            recoverable = true;
            advice = "观察冻结：暂停动作输出，等待新帧到达或重捕获画面";
        }
        else if (drift > 1.2f)
        {
            kind = DisturbanceKind.ActionDrift;
            recoverable = drift < 2.5f;
            advice = recoverable ? "动作漂移：拒绝异常块，回退到上一已验证动作" : "动作严重漂移：切换保守模式";
        }
        else
        {
            kind = DisturbanceKind.None; recoverable = true; advice = "观察新鲜、动作一致，正常运行";
        }
        return new Verdict(kind, recoverable, (float)Math.Round(freshness, 3), (float)Math.Round(drift, 3), advice);
    }

    public void Reset() { _recentThumbs.Clear(); _recentEntropy.Clear(); }
}
