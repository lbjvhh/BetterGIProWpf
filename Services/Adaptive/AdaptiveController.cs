using System;
using System.Collections.Generic;
using System.Linq;

namespace BetterGIProWpf.Services.Adaptive;

/// <summary>运行时异常类别（供异常-策略映射表索引）。</summary>
public enum AnomalyKind
{
    LowConfidence,        // NitroGen 置信度低于阈值
    InferenceLag,         // 推理延迟持续超阈值
    ScriptStuck,          // 脚本卡死（角色傻站/路径卡住）
    PositionLost,         // 位置获取失败（getPositionFromMap 返回 null）
    InputRisk,            // 输入行为风险评分升高
    NetworkFlap,          // 网络波动/掉线
    UiDrift,              // UI 元素偏移（游戏版本更新）
    AlignmentDrift,       // 与计划路径逐渐偏离（对齐损失超阈值）
    RepeatedFailure,      // 同一任务节点重复失败
    FrozenFrame           // 画面冻结（截图器故障/加载卡住）
}

/// <summary>应对策略。</summary>
public enum Strategy
{
    Noop,                 // 观察
    LowerInferFreq,       // 降低推理频率
    IncreaseHumanize,     // 增强拟人化噪声
    FallbackToScript,     // 回退到脚本执行模式
    ResetView,            // 重置视角/坐标
    RetryNode,            // 重试当前节点
    SwitchCapture,        // 切换截图通道
    Rollback,             // 进度感知回滚
    EmergencyStop,        // 紧急停机
    NotifyUser            // 通知用户
}

/// <summary>一次执行检查点（进度感知回滚的候选恢复点）。</summary>
public sealed record Checkpoint(
    int Id,
    string Node,
    DateTime Time,
    double Progress,      // 0..1 全局进度
    double AlignmentScore,// 与目标轨迹的对齐度（高=好）
    string? SnapshotPath)
{
    public bool IsReliableBase { get; set; } = true;
}

/// <summary>
/// 中央自适应控制器（方案六章）。
/// 双控制器框架：
///  - EventTrigger（高频）：监控事件 → 触发对应动作（异常-策略映射表）
///  - StrategicController（低频）：全局规划、失败分析、恢复决策（进度感知回滚）
/// 动态适配器切换：对齐损失超阈值 → 更保守策略。
/// </summary>
public class AdaptiveController
{
    /// <summary>异常-策略映射表（可配置优先级；用户可自定义哪些自动执行）。</summary>
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

    /// <summary>手动覆盖：禁用某些策略（用户配置）。</summary>
    public HashSet<Strategy> DisabledStrategies { get; } = new();

    /// <summary>对齐损失阈值（动态适配器切换）。</summary>
    public double AlignmentDriftThreshold { get; set; } = 0.45;

    private readonly List<Checkpoint> _checkpoints = new();
    private int _checkpointSeq;
    private readonly Dictionary<AnomalyKind, int> _anomalyCount = new();
    private bool _inRecovery;

    // ---------- EventTrigger：高频事件监控 ----------

    /// <summary>事件触发入口：登记异常 → 依据策略映射返回动作序列。</summary>
    public Strategy[] OnEvent(AnomalyKind kind, double severity = 0.5)
    {
        _anomalyCount[kind] = _anomalyCount.GetValueOrDefault(kind) + 1;
        var strategies = PolicyMap.TryGetValue(kind, out var s) ? s : new[] { Strategy.NotifyUser };
        return strategies.Where(x => !DisabledStrategies.Contains(x)).ToArray();
    }

    // ---------- StrategicController：低频全局规划与恢复 ----------

    /// <summary>登记检查点（进度感知回滚的候选恢复点）。</summary>
    public Checkpoint AddCheckpoint(string node, double progress, double alignmentScore, string? snapshotPath = null)
    {
        var cp = new Checkpoint(++_checkpointSeq, node, DateTime.Now, progress, alignmentScore, snapshotPath);
        _checkpoints.Add(cp);
        if (_checkpoints.Count > 20) _checkpoints.RemoveAt(0);
        return cp;
    }

    /// <summary>
    /// 进度感知回滚：判定当前状态是否仍是继续执行的可靠基础；
    /// 不是则返回最近一个可恢复检查点（对齐度最高的有效点），调用方据此重启。
    /// </summary>
    public Checkpoint? ProgressAwareRollback(string currentNode, double currentProgress, double currentAlignment)
    {
        if (currentAlignment >= AlignmentDriftThreshold)
        {
            _inRecovery = false;
            return null; // 仍在可靠轨迹上
        }
        var candidate = _checkpoints
            .Where(c => c.IsReliableBase && c.Progress <= currentProgress)
            .OrderByDescending(c => c.Progress)          // 优先回滚到离当前最近的进度
            .ThenByDescending(c => c.AlignmentScore)
            .FirstOrDefault();
        _inRecovery = candidate != null;
        return candidate;
    }

    public bool InRecovery => _inRecovery;

    /// <summary>标记某检查点不再可靠（回滚后）。</summary>
    public void MarkUnreliable(int checkpointId)
    {
        var cp = _checkpoints.FirstOrDefault(c => c.Id == checkpointId);
        if (cp != null) cp.IsReliableBase = false;
    }

    /// <summary>当前异常计数快照。</summary>
    public IReadOnlyDictionary<AnomalyKind, int> AnomalyCounts => _anomalyCount;

    /// <summary>重置运行状态（新任务开始）。</summary>
    public void Reset()
    {
        _checkpoints.Clear();
        _checkpointSeq = 0;
        _anomalyCount.Clear();
        _inRecovery = false;
    }
}
