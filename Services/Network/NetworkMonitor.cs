namespace BetterGIProWpf.Services.Network;

public class NetworkMonitor
{
    public record Mirror(string Name, string Url, bool Healthy = true);
    private readonly List<Mirror> _mirrors = new();
    private readonly object _lock = new();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public int RetryCount { get; set; } = 3;
    public bool IsNetworkAbnormal { get; private set; }
    public bool IsDisconnected { get; private set; }
    public event Action<string>? Log;

    public NetworkMonitor()
    {
        _mirrors.Add(new Mirror("GitHub", "https://github.com"));
        _mirrors.Add(new Mirror("Gitee", "https://gitee.com"));
        _mirrors.Add(new Mirror("jsDelivr", "https://cdn.jsdelivr.net"));
    }

    public void AddMirror(string name, string url) { lock (_lock) _mirrors.Add(new Mirror(name, url)); }

    public bool CheckFrameHealth(double frameChangeRate, bool hasLoadingFeature, double stagnantSeconds)
    {
        if (frameChangeRate < 0.02 && !hasLoadingFeature && stagnantSeconds >= 5) { IsNetworkAbnormal = true; Log?.Invoke("画面无变化，疑似网络异常"); return true; }
        return false;
    }

    public bool CheckReconnectScreen(string ocrText)
    {
        if (ocrText.Contains("重新连接") || ocrText.Contains("网络连接失败") || ocrText.Contains("连接服务器")) { IsDisconnected = true; IsNetworkAbnormal = true; return true; }
        return false;
    }

    public string? FetchWithFallback(string relativePath, Func<string, string?> fetch)
    {
        lock (_lock)
        {
            foreach (var m in _mirrors.Where(m => m.Healthy))
            {
                var result = fetch(m.Url + relativePath);
                if (result != null) return result;
            }
            return null;
        }
    }

    public bool CheckPauseThreshold(DateTime abnormalSince, Action<string> notify)
    {
        if (IsNetworkAbnormal && (DateTime.Now - abnormalSince).TotalSeconds >= 60) { notify("网络异常持续超 60s，已暂停"); return true; }
        return false;
    }

    public int ResumeFromCheckpoint(int checkpoint, int total) => Math.Clamp(checkpoint, 0, Math.Max(0, total - 1));
}
