namespace BetterGIProWpf.Services.Games;
public class GameAdapterManager {
    public record AdapterInfo(string Id, string DisplayName, string Version, string[] Aliases, string WindowClassHint, string InputMapJson);
    private readonly Dictionary<string, AdapterInfo> _ad = new();
    public event Action<string>? Log;
    public GameAdapterManager() {
        _ad["genshin"] = new AdapterInfo("genshin","原神","1.0",new[]{"原神","Genshin Impact"},"UnityWndClass","{}");
        _ad["wuthering"] = new AdapterInfo("wuthering","鸣潮","1.0",new[]{"鸣潮","Wuthering Waves"},"UnrealWindow","{}");
        _ad["zzz"] = new AdapterInfo("zzz","绝区零","1.0",new[]{"绝区零","Zenless Zone Zero"},"UnityWndClass","{}");
    }
    public AdapterInfo? Resolve(string name) => _ad.Values.FirstOrDefault(a => a.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase) || a.Aliases.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)));
    public IReadOnlyList<AdapterInfo> List() => _ad.Values.ToArray();
}
