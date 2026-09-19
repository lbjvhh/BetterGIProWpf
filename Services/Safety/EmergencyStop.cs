using System.Text.Json;
namespace BetterGIProWpf.Services.Safety;
public enum EmergencyKind { BanWarning, NetworkError, GameCrash, AntiCheatPopup, LongHang, AbnormalFrame, ProcessExit, InputLost, LoadStuck }
public record EmergencyEvent(EmergencyKind Kind, string Detail, DateTime At);
public class EmergencyStop
{
    public enum AutoAction { StopOnly, Restart, SafePosition }
    private readonly List<EmergencyEvent> _events = new(); private readonly object _lock = new();
    public bool IsStopped { get; private set; }
    public AutoAction Action { get; set; } = AutoAction.StopOnly;
    public event Action<EmergencyEvent>? Alerted; public event Action<string>? Log;
    public bool Raise(EmergencyKind kind, string detail) { lock (_lock) _events.Add(new EmergencyEvent(kind, detail, DateTime.Now)); IsStopped = true; Log?.Invoke($"[应急] {kind}: {detail}"); Alerted?.Invoke(new EmergencyEvent(kind, detail, DateTime.Now)); return true; }
    public bool CheckNetwork(string t) { if (ContainsAny(t, "网络连接","连接失败","timeout","掉线","重连失败")) return Raise(EmergencyKind.NetworkError, t); return false; }
    public bool CheckRiskText(string t) { if (ContainsAny(t, "封禁","违规","踢下线","账号异常","mihoyo shield","security")) return Raise(EmergencyKind.BanWarning, t); return false; }
    public void Resume() { IsStopped = false; Log?.Invoke("已恢复"); }
    public string SaveState(string progress, double ts, string? shot) => JsonSerializer.Serialize(new { savedAt = DateTime.Now, progress, videoTimestampSec = ts, screenshotPath = shot });
    public IReadOnlyList<EmergencyEvent> Events { get { lock (_lock) return _events.ToArray(); } }
    private static bool ContainsAny(string s, params string[] keys) => !string.IsNullOrEmpty(s) && keys.Any(k => s.Contains(k, StringComparison.OrdinalIgnoreCase));
}
