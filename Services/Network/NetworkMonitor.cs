namespace BetterGIProWpf.Services.Network;
public class NetworkMonitor {
    public record Mirror(string Name, string Url, bool Healthy=true);
    private readonly List<Mirror> _m = new(); public bool IsAbnormal { get; private set; } public event Action<string>? Log;
    public NetworkMonitor() { _m.Add(new("GitHub","https://github.com")); _m.Add(new("Gitee","https://gitee.com")); _m.Add(new("jsDelivr","https://cdn.jsdelivr.net")); }
    public bool CheckReconnect(string t) { if (t.Contains("重新连接")||t.Contains("网络连接失败")||t.Contains("连接服务器")) { IsAbnormal=true; Log?.Invoke("检测到断线"); return true; } return false; }
    public string? FetchWithFallback(string rel, Func<string,string?> fetch) { lock (_lock) foreach (var m in _m) { var r = fetch(m.Url+rel); if (r!=null) return r; Log?.Invoke($"{m.Name} 失败"); } return null; }
    public int ResumeFromCheckpoint(int cp, int total) => Math.Clamp(cp,0,Math.Max(0,total-1));
}
