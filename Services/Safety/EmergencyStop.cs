using System.Text.Json;

namespace BetterGIProWpf.Services.Safety;

public enum EmergencyKind { BanWarning, NetworkError, GameCrash, AntiCheatPopup, LongHang, AbnormalFrame, ProcessExit, InputLost, LoadStuck }
public record EmergencyEvent(EmergencyKind Kind, string Detail, DateTime At);

public class EmergencyStop
{
    public enum AutoAction { StopOnly, Restart, SafePosition }
    private readonly List<EmergencyEvent> _events = new();
    public bool IsStopped { get; private set; }
    public AutoAction Action { get; set; } = AutoAction.StopOnly;
    public event Action<EmergencyEvent>? Alerted;

    public bool Raise(EmergencyKind kind, string detail)
    {
        _events.Add(new EmergencyEvent(kind, detail, DateTime.Now));
        IsStopped = true; Alerted?.Invoke(_events[^1]); return true;
    }

    public bool CheckNetwork(string t)
    {
        if (t.Contains("网络连接") || t.Contains("掉线")) return Raise(EmergencyKind.NetworkError, t);
        return false;
    }

    public bool CheckRiskText(string t)
    {
        if (t.Contains("封禁") || t.Contains("违规") || t.Contains("mihoyo shield")) return Raise(EmergencyKind.BanWarning, t);
        return false;
    }

    public void Resume() { IsStopped = false; }
    public string SaveState(string progress, double videoTs, string? shot) => JsonSerializer.Serialize(new { progress, videoTs, shot });
    public IReadOnlyList<EmergencyEvent> Events => _events;
}
