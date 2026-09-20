using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterGIProWpf.Services.NitroGen;

public enum ExecutionMode { EndToEnd, Script, Hybrid }

public record NitroGenAction(float LeftStickX, float LeftStickY, float RightStickX, float RightStickY, bool[] Buttons, double Confidence);

public class DualTrackController
{
    private readonly Queue<double> _recentConfidence = new();
    private readonly int _historySize = 10;
    public double Threshold { get; set; } = 0.6;
    public ExecutionMode Mode { get; private set; } = ExecutionMode.Script;

    public void SetMode(ExecutionMode mode) { Mode = mode; }

    public ExecutionMode Evaluate(NitroGenAction action)
    {
        double confidence = ComputeConfidence(action);
        _recentConfidence.Enqueue(confidence);
        if (_recentConfidence.Count > _historySize) _recentConfidence.Dequeue();
        double avgConfidence = _recentConfidence.Average();
        if (Mode == ExecutionMode.EndToEnd)
        {
            int lowCount = _recentConfidence.Count(c => c < Threshold);
            if (lowCount >= 3) { Mode = ExecutionMode.Script; }
        }
        return Mode;
    }

    private double ComputeConfidence(NitroGenAction action)
    {
        double stickMagnitude = Math.Sqrt(action.LeftStickX * action.LeftStickX + action.LeftStickY * action.LeftStickY);
        double stickConfidence = 1.0 - Math.Min(1.0, stickMagnitude / 1.0);
        int buttonCount = action.Buttons.Count(b => b);
        double buttonConfidence = buttonCount <= 2 ? 0.9 : 0.5;
        return stickConfidence * 0.3 + buttonConfidence * 0.2 + action.Confidence * 0.5;
    }

    public double[] GetConfidenceHistory() => _recentConfidence.ToArray();
}
