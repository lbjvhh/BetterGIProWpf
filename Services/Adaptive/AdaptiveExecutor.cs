using System.Text.Json;

namespace BetterGIProWpf.Services.Adaptive;

public enum RuntimeAnomaly
{
    PositionNull, TeamSwitchConflict, SkillNotCast, KeyMistouch,
    PathStuck, LoadHang, NetworkJitter, ScriptTimeout, CharacterDead, MapMatchFail
}

public record AdaptationRecord(RuntimeAnomaly Anomaly, string Adjustment, bool Resolved, DateTime At);

public class AdaptiveExecutor
{
    public record FixStrategy(RuntimeAnomaly Anomaly, string Name, string Description,
        Func<string, string> Apply, int MaxRetries = 2);

    private readonly List<FixStrategy> _knowledge = new();
    private readonly List<AdaptationRecord> _history = new();
    private readonly Dictionary<RuntimeAnomaly, int> _successCount = new();
    private readonly object _lock = new();

    public event Action<string>? Log;

    public AdaptiveExecutor()
    {
        Register(new FixStrategy(RuntimeAnomaly.PositionNull, "切换 SIFT 地图匹配",
            "getPositionFromMap 返回 null → 地图匹配改为 SIFT",
            p => p.Replace("\"match\":\"template\"", "\"match\":\"sift\"") ?? p));
        Register(new FixStrategy(RuntimeAnomaly.TeamSwitchConflict, "降低采集物判定优先级",
            "队伍切换与采集物判定冲突",
            p => p.Replace("\"collectFilter\":\"all\"", "\"collectFilter\":\"none\"") ?? p));
        Register(new FixStrategy(RuntimeAnomaly.SkillNotCast, "切换备用战斗策略",
            "角色傻站不释放技能",
            p => p.Replace("\"fightStrategy\":\"main\"", "\"fightStrategy\":\"backup\"") ?? p));
        Register(new FixStrategy(RuntimeAnomaly.KeyMistouch, "按键消抖",
            "ESC 误触被检测为死亡", p => p + "\n\"debounceMs\": 300"));
        Register(new FixStrategy(RuntimeAnomaly.PathStuck, "启用卡死脱离",
            "路径卡墙 → StuckDetector", p => p + "\n\"stuckDetect\": true"));
        Register(new FixStrategy(RuntimeAnomaly.LoadHang, "等待加载",
            "加载卡住", p => p + "\n\"waitForLoad\": true"));
        Register(new FixStrategy(RuntimeAnomaly.ScriptTimeout, "放宽超时",
            "脚本超时", p => p + "\n\"errorThreshold\": 30"));
        Register(new FixStrategy(RuntimeAnomaly.CharacterDead, "传送回锚点",
            "角色死亡", p => p + "\n\"respawn\": \"nearest-anchor\""));
        Register(new FixStrategy(RuntimeAnomaly.MapMatchFail, "降级保守执行",
            "地图匹配失败", p => p + "\n\"conservativeMode\": true"));
        Register(new FixStrategy(RuntimeAnomaly.NetworkJitter, "网络重试",
            "网络波动", p => p + "\n\"retry\": 3"));
    }

    public void Register(FixStrategy s) { lock (_lock) _knowledge.Add(s); }

    public RuntimeAnomaly? Detect(string logLine)
    {
        if (logLine.Contains("getPositionFromMap") && logLine.Contains("null")) return RuntimeAnomaly.PositionNull;
        if (logLine.Contains("队伍切换") && logLine.Contains("冲突")) return RuntimeAnomaly.TeamSwitchConflict;
        if (logLine.Contains("傻站") || (logLine.Contains("战斗") && logLine.Contains("超时"))) return RuntimeAnomaly.SkillNotCast;
        if (logLine.Contains("ESC") && logLine.Contains("误触")) return RuntimeAnomaly.KeyMistouch;
        if (logLine.Contains("卡墙") || (logLine.Contains("路径") && logLine.Contains("卡"))) return RuntimeAnomaly.PathStuck;
        if (logLine.Contains("加载") && logLine.Contains("卡")) return RuntimeAnomaly.LoadHang;
        if (logLine.Contains("网络") && logLine.Contains("波动")) return RuntimeAnomaly.NetworkJitter;
        if (logLine.Contains("超时")) return RuntimeAnomaly.ScriptTimeout;
        if (logLine.Contains("死亡")) return RuntimeAnomaly.CharacterDead;
        if (logLine.Contains("地图匹配失败")) return RuntimeAnomaly.MapMatchFail;
        return null;
    }

    public string? Fix(RuntimeAnomaly anomaly, string scriptConfig, int maxAttempts = 2)
    {
        lock (_lock)
        {
            var candidates = _knowledge.Where(k => k.Anomaly == anomaly)
                .OrderByDescending(k => _successCount.GetValueOrDefault(k.Anomaly)).ToList();
            if (candidates.Count == 0) { Log?.Invoke($"[自适应] 无 {anomaly} 策略"); return null; }
            var attempt = 0;
            foreach (var fix in candidates)
            {
                attempt++;
                if (attempt > maxAttempts) break;
                Log?.Invoke($"[自适应] {anomaly} → {fix.Name}");
                try
                {
                    var applied = fix.Apply(scriptConfig);
                    _history.Add(new AdaptationRecord(anomaly, fix.Name, true, DateTime.Now));
                    _successCount[anomaly] = _successCount.GetValueOrDefault(anomaly) + 1;
                    return applied;
                }
                catch { }
            }
            _history.Add(new AdaptationRecord(anomaly, "全部失败", false, DateTime.Now));
            return null;
        }
    }

    public string ExportHistoryJson() { lock (_lock) return JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true }); }
    public IReadOnlyList<AdaptationRecord> History { get { lock (_lock) return _history.ToArray(); } }
}
