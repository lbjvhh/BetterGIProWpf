using System.Text.Json;

namespace BetterGIProWpf.Services.Pathfinding;

public class StuckDetector
{
    public enum Terrain { Narrow, Water, Mountain, Indoor, Plain }
    public enum EscapeAction { DirectionCombo, Jump, Dash, TeleportAnchor, SwitchCharacter }
    public record Position(double X, double Y, double T);
    private readonly List<Position> _trace = new();
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

    public void Update(double x, double y)
    {
        lock (_lock) { _trace.Add(new Position(x, y, _trace.Count == 0 ? 0 : _trace[^1].T + 0.5)); if (_trace.Count > 120) _trace.RemoveAt(0); IsStuck = DetectStuck(); IsSpinning = DetectSpin(); }
    }

    public bool DetectStuck()
    {
        if (_trace.Count < 5) return false;
        var recent = _trace.TakeLast(5).ToArray();
        var dist = Math.Sqrt(Math.Pow(recent[^1].X - recent[0].X, 2) + Math.Pow(recent[^1].Y - recent[0].Y, 2));
        return dist < 2.0;
    }

    public bool DetectSpin()
    {
        if (_trace.Count < 8) return false;
        var recent = _trace.TakeLast(8).ToArray();
        double angleSum = 0; double? prev = null;
        for (var i = 1; i < recent.Length; i++)
        {
            var dx = recent[i].X - recent[i - 1].X; var dy = recent[i].Y - recent[i - 1].Y;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) continue;
            var ang = Math.Atan2(dy, dx);
            if (prev.HasValue) { var d = ang - prev.Value; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI; angleSum += Math.Abs(d); }
            prev = ang;
        }
        return angleSum > Math.PI * 2 - 0.05;
    }

    public EscapeAction? Escape(Terrain terrain)
    {
        IsStuck = false; IsSpinning = false;
        var actions = _priority.GetValueOrDefault(terrain, _priority[Terrain.Plain]);
        foreach (var a in actions) { Log?.Invoke($"[脱离] {a}"); System.Threading.Thread.Sleep(60); if (!DetectStuck()) return a; }
        Log?.Invoke("[脱离] 全部失败，跳过路径点");
        return null;
    }

    public string ExportLog() => JsonSerializer.Serialize(new { escapes = _trace.Count });
}
