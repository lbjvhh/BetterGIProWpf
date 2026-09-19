using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterGIProWpf.Services.Adaptive;

public enum AnomalyKind
{
    LowConfidence, InferenceLag, ScriptStuck, PositionLost, InputRisk,
    NetworkFlap, UiDrift, AlignmentDrift, RepeatedFailure, FrozenFrame
}

public enum Strategy
{
    Noop, LowerInferFreq, IncreaseHumanize, FallbackToScript,
    ResetView, RetryNode, SwitchCapture, Rollback, EmergencyStop, NotifyUser
}

public sealed record Checkpoint(
    int Id, string Node, DateTime Time, double Progress,
    double AlignmentScore, string? SnapshotPath)
{
    public bool IsReliableBase { get; set; } = true;
}

public class AdaptiveController
{
    public Dictionary<AnomalyKind, Strategy[]> PolicyMap { get; } = new()
    {
        [AnomalyKind.LowConfidence] = new[] { Strategy.FallbackToScript, Strategy.LowerInferFreq },
        [AnomalyKind.InferenceLag] = new[] { Strategy.LowerInferFreq, Strategy.RetryNode },
        [AnomalyKind.ScriptStuck] = new[] { Strategy.ResetView, Strategy.RetryNode, Strategy.FallbackToScript },
        [AnomalyKind.PositionLost] = new[] { Strategy.ResetView, Strategy.RetryNode },
        [AnomalyKind.InputRisk] = new[] { Strategy.IncreaseHumanize, Strategy.LowerInferFreq },
        [AnomalyKind.NetworkFlap] = new[] { Strategy.RetryNode, Strategy.NotifyUser },
        [AnomalyKind.UiDrift] = new[] { Strategy.ResetView, Strategy.NotifyUser },
        [AnomalyKind.AlignmentDrift] = new[] { Strategy.Rollback, Strategy.IncreaseHumanize },
        [AnomalyKind.RepeatedFailure] = new[] { Strategy.FallbackToScript, Strategy.NotifyUser },
        [AnomalyKind.FrozenFrame] = new[] { Strategy.SwitchCapture, Strategy.EmergencyStop },
    };

    public HashSet<Strategy> DisabledStrategies { get; } = new();
    public double AlignmentDriftThreshold { get; set; } = 0.45;

    private readonly List<Checkpoint> _checkpoints = new();
    private int _checkpointSeq;
    private readonly Dictionary<AnomalyKind, int> _anomalyCount = new();
    private bool _inRecovery;

    public Strategy[] OnEvent(AnomalyKind kind, double severity = 0.5)
    {
        _anomalyCount[kind] = _anomalyCount.GetValueOrDefault(kind) + 1;
        var strategies = PolicyMap.TryGetValue(kind, out var s) ? s : new[] { Strategy.NotifyUser };
        return strategies.Where(x => !DisabledStrategies.Contains(x)).ToArray();
    }

    public Checkpoint AddCheckpoint(string node, double progress, double alignmentScore, string? snapshotPath = null)
    {
        var cp = new Checkpoint(++_checkpointSeq, node, DateTime.Now, progress, alignmentScore, snapshotPath);
        _checkpoints.Add(cp);
        if (_checkpoints.Count > 20) _checkpoints.RemoveAt(0);
        return cp;
    }

    public Checkpoint? ProgressAwareRollback(string currentNode, double currentProgress, double currentAlignment)
    {
        if (currentAlignment >= AlignmentDriftThreshold) { _inRecovery = false; return null; }
        var candidate = _checkpoints
            .Where(c => c.IsReliableBase && c.Progress <= currentProgress)
            .OrderByDescending(c => c.Progress)
            .ThenByDescending(c => c.AlignmentScore)
            .FirstOrDefault();
        _inRecovery = candidate != null;
        return candidate;
    }

    public bool InRecovery => _inRecovery;

    public void MarkUnreliable(int checkpointId)
    {
        var cp = _checkpoints.FirstOrDefault(c => c.Id == checkpointId);
        if (cp != null) cp.IsReliableBase = false;
    }

    public IReadOnlyDictionary<AnomalyKind, int> AnomalyCounts => _anomalyCount;

    public void Reset()
    {
        _checkpoints.Clear();
        _checkpointSeq = 0;
        _anomalyCount.Clear();
        _inRecovery = false;
    }
}
