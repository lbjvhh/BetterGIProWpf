using System.IO;
using System.Text.Json;

namespace BetterGIProWpf.Services.Games;

public class GameAdapterManager
{
    public record AdapterInfo(string Id, string DisplayName, string Version, string[] Aliases,
        string WindowClassHint, string InputMapJson);

    private readonly Dictionary<string, AdapterInfo> _adapters = new();
    private readonly FileSystemWatcher? _watcher;
    private readonly object _lock = new();

    public event Action<string>? Log;

    public GameAdapterManager(string? adapterRoot = null)
    {
        _adapters["genshin"] = new AdapterInfo("genshin", "原神", "1.0", new[] { "原神", "Genshin Impact", "YuanShen" }, "UnityWndClass", "{}");
        _adapters["wuthering"] = new AdapterInfo("wuthering", "鸣潮", "1.0", new[] { "鸣潮", "Wuthering Waves" }, "UnrealWindow", "{}");
        _adapters["zzz"] = new AdapterInfo("zzz", "绝区零", "1.0", new[] { "绝区零", "Zenless Zone Zero" }, "UnityWndClass", "{}");
        if (!string.IsNullOrEmpty(adapterRoot) && Directory.Exists(adapterRoot))
        {
            _watcher = new FileSystemWatcher(adapterRoot, "*.json") { IncludeSubdirectories = true, EnableRaisingEvents = true };
            _watcher.Changed += (_, e) => Reload(e.FullPath);
            _watcher.Created += (_, e) => Reload(e.FullPath);
        }
    }

    public void Reload(string jsonPath)
    {
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var r = doc.RootElement;
            var id = r.GetProperty("id").GetString()!;
            var info = new AdapterInfo(id, r.GetProperty("name").GetString() ?? id,
                r.GetProperty("version").GetString() ?? "1.0",
                r.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()!).ToArray(),
                r.GetProperty("windowClass").GetString() ?? "",
                r.GetProperty("inputMap").GetRawText());
            lock (_lock) _adapters[id] = info;
            Log?.Invoke($"[适配器] 热加载 {id}");
        }
        catch (Exception ex) { Log?.Invoke($"[适配器] 加载失败: {ex.Message}"); }
    }

    public AdapterInfo? Resolve(string gameName)
    {
        lock (_lock)
            return _adapters.Values.FirstOrDefault(a =>
                a.DisplayName.Equals(gameName, StringComparison.OrdinalIgnoreCase) ||
                a.Aliases.Any(al => al.Equals(gameName, StringComparison.OrdinalIgnoreCase)));
    }

    public string AdaptToVersionChange(AdapterInfo adapter, string gameVersion)
    {
        if (string.IsNullOrEmpty(gameVersion)) { Log?.Invoke($"[适配器] 无法确认版本，降级保守"); return "conservative"; }
        Log?.Invoke($"[适配器] {adapter.DisplayName} v{adapter.Version} 适配 {gameVersion}");
        return "normal";
    }

    public IReadOnlyList<AdapterInfo> List() { lock (_lock) return _adapters.Values.ToArray(); }
}
