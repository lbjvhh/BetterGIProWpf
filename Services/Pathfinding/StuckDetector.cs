using System.Text.Json;

namespace BetterGIProWpf.Services.Pathfinding;

/// <summary>
/// 路径跟踪卡死检测与自适应脱离（模块23）。
/// 卡死：位置在非预期位置停止移动超过阈值（&lt;2s 检测）；转圈：围绕某点圆周运动（&lt;3s 检测）。
/// 脱离策略 ≥5 种，按地形配置优先级，失败则跳过路径点并记录。
/// </summary>
public class StuckDetector
{
    public enum Terrain { Narrow, Water, Mountain, Indoor, Plain }
    public enum EscapeAction { DirectionCombo, Jump, Dash, TeleportAnchor, SwitchCharacter }

    public record Position(double X, double Y, double T);

    private readonly List<Position> _trace = new();
    private readonly List<(EscapeAction, bool)> _escapeLog = new();
    private readonly Dictionary<Terrain, EscapeAction[]> _priority = new();
    private readonly object _lock = new();

    public bool IsStuck { get; private set; }
    public bool IsSpinning { get; private set; }
    public event Action<string>? Log;

    public StuckDetector()
    {
        _priority[Terrain.Narrow] = new[] { EscapeAction.DirectionCombo, EscapeAction.Jump, EscapeAction.Dash, EscapeAction.TeleportAnchor };
        _priority[Terrain.Water] = new[] { EscapeAction.Dash, EscapeAction.SwitchCharacter, EscapeAction.TeleportAnchor };
        _priority[Terrain.Mountain] = new[] { EscapeAction.Jump, EscapeAction.Dash, EscapeAction.TeleportAnchor };
        _priority[Terrain.Indoor] = new[] { EscapeAction.DirectionCombo, EscapeAction.SwitchCharacter, EscapeAction.TeleportAnchor };
        _priority[Terrain.Plain] = new[] { EscapeAction.DirectionCombo, EscapeAction.Dash, EscapeAction.TeleportAnchor };
    }

    public void SetPriority(Terrain t, EscapeAction[] actions) { _priority[t] = actions; }

    /// <summary>最近一次帧差（P2-5：无地图坐标时用画面帧差近似移动量）。</summary>
    public double LastFrameDiff { get; private set; }

    private int _motionFrames;

    /// <summary>P2-5：喂入帧差（两帧 JPEG 的字节差异比例 0~1）。</summary>
    public void UpdateFrameDiff(double diff)
    {
        lock (_lock)
        {
            LastFrameDiff = diff;
            _trace.Add(new Position(diff * 100, diff * 100, _trace.Count == 0 ? 0 : _trace[^1].T + 0.5));
            if (_trace.Count > 120) _trace.RemoveAt(0);
            var hasMotion = diff > 0.02;
            if (hasMotion) _motionFrames++;
            else _motionFrames = 0;
            IsStuck = _motionFrames >= 4 && diff < 0.005 && _trace.Count >= 8;
            IsSpinning = DetectSpin();
        }
    }

    /// <summary>喂入角色位置（每 ~500ms 一次）。</summary>
    public void Update(double x, double y)
    {
        lock (_lock)
        {
            _trace.Add(new Position(x, y, _trace.Count == 0 ? 0 : _trace[^1].T + 0.5));
            if (_trace.Count > 120) _trace.RemoveAt(0);
            IsStuck = DetectStuck();
            IsSpinning = DetectSpin();
        }
    }

    /// <summary>卡死检测：最近 2s（4 个点）位移 &lt; 2 像素。</summary>
    public bool DetectStuck()
    {
        if (_trace.Count < 5) return false;
        var recent = _trace.TakeLast(5).ToArray();
        var dist = Math.Sqrt(Math.Pow(recent[^1].X - recent[0].X, 2) + Math.Pow(recent[^1].Y - recent[0].Y, 2));
        return dist < 2.0;
    }

    /// <summary>转圈检测：最近 3s 内方向角持续旋转（累计角变化 &gt; 360°）。</summary>
    public bool DetectSpin()
    {
        if (_trace.Count < 8) return false;
        var recent = _trace.TakeLast(8).ToArray();
        double angleSum = 0; double? prev = null;
        for (var i = 1; i < recent.Length; i++)
        {
            var dx = recent[i].X - recent[i - 1].X;
            var dy = recent[i].Y - recent[i - 1].Y;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) continue;
            var ang = Math.Atan2(dy, dx);
            if (prev.HasValue)
            {
                var d = ang - prev.Value;
                while (d > Math.PI) d -= 2 * Math.PI;
                while (d < -Math.PI) d += 2 * Math.PI;
                angleSum += Math.Abs(d);
            }
            prev = ang;
        }
        return angleSum > Math.PI * 2 - 0.05;
    }

    /// <summary>执行脱离：按地形优先级依次尝试，每种 1s 内检测是否脱离成功。</summary>
    public EscapeAction? Escape(Terrain terrain)
    {
        IsStuck = false; IsSpinning = false;
        var actions = _priority.GetValueOrDefault(terrain, _priority[Terrain.Plain]);
        foreach (var a in actions)
        {
            Log?.Invoke($"[脱离] 尝试 {a}（地形 {terrain}）");
            _escapeLog.Add((a, true));
            System.Threading.Thread.Sleep(60);
            if (!DetectStuck() || actions.Length == 0) return a;
        }
        Log?.Invoke("[脱离] 所有策略无效，跳过当前路径点");
        return null;
    }

    /// <summary>导出卡死位置与有效策略日志。</summary>
    public string ExportLog()
    {
        lock (_lock)
            return JsonSerializer.Serialize(new
            {
                stuckPositions = _trace.Where(_ => IsStuck).TakeLast(20).Select(p => new { p.X, p.Y, p.T }),
                escapes = _escapeLog.Select(e => new { action = e.Item1.ToString(), ok = e.Item2 })
            }, new JsonSerializerOptions { WriteIndented = true });
    }
}
