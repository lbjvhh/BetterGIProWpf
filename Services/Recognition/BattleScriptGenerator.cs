using System.Text;

namespace BetterGIProWpf.Services.Recognition;

public static class BattleScriptGenerator
{
    public sealed record Command(string Op, double DurationSec, string? Param = null)
    {
        public override string ToString() => Param is null ? $"{Op};{DurationSec:0.###}" : $"{Op};{DurationSec:0.###};{Param}";
    }

    public static string MapKey(string key) => key.ToUpperInvariant() switch
    {
        "A" or "MOUSE0" => "attack",
        "Q" => "burst",
        "E" => "skill",
        "SPACE" => "jump",
        "SHIFT" => "sprint",
        "F" => "interact",
        _ => "wait",
    };

    public static List<Command> FromKeyTimeline(IEnumerable<KeyOverlay.KeyEvent> events, double holdMinSec = 0.15)
    {
        var cmds = new List<Command>(); var downAt = new Dictionary<string, double>();
        foreach (var e in events.OrderBy(e => e.TimeSec))
        {
            if (e.Pressed) downAt[e.Key] = e.TimeSec;
            else if (downAt.TryGetValue(e.Key, out var start))
            {
                double dur = e.TimeSec - start; downAt.Remove(e.Key);
                if (dur < holdMinSec) continue;
                cmds.Add(new Command(MapKey(e.Key), Math.Max(0.2, dur)));
            }
        }
        return cmds;
    }

    public static string ToBattleScriptTxt(IEnumerable<Command> cmds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# BetterGI AutoFight");
        foreach (var c in cmds) sb.AppendLine(c.ToString());
        return sb.ToString();
    }

    public static string SaveToAutoFight(string userRoot, string fileName, IEnumerable<Command> cmds)
    {
        var dir = Path.Combine(userRoot, "AutoFight"); Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName.EndsWith(".txt") ? fileName : fileName + ".txt");
        File.WriteAllText(path, ToBattleScriptTxt(cmds), Encoding.UTF8);
        return path;
    }
}
