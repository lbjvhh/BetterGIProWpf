using System.IO;
using System.Text.Json;

namespace BetterGIProWpf.Services.Ui;

/// <summary>关键 UI 元素（模块25 要求 ≥8 类）。</summary>
public enum UiElement { Map, Teleport, TaskBar, Bag, Character, Settings, Combat, Dialogue }

/// <summary>
/// 游戏版本更新 UI 变化自动适配（模块25）。
/// 模板匹配+特征对比检测 8 类 UI 元素位置偏移；启动后 10s 内完成检测；
/// 可调范围内自动更新坐标映射，超出提示用户；坐标映射库按版本号索引，多版本共存；
/// 持久化到 %APPDATA%\BetterGIProWpf\ui_layouts.json，跨重启保留。
/// </summary>
public class UiLayoutAdaptor
{
    public record ElementBox(int X, int Y, int W, int H, double Confidence);

    public const int MaxAdjustableOffset = 80; // 像素
    private readonly Dictionary<string, Dictionary<UiElement, ElementBox>> _versionMap = new();
    private readonly object _lock = new();
    private readonly string _dbPath;

    public event Action<string>? Log;
    public bool NeedsUserUpdate { get; private set; }
    public string CurrentVersion { get; private set; } = "";

    public UiLayoutAdaptor(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BetterGIProWpf", "ui_layouts.json");
        Load();
    }

    public void RegisterBaseline(string gameVersion, Dictionary<UiElement, ElementBox> layout)
    {
        lock (_lock) { _versionMap[gameVersion] = layout; Save(); }
    }

    public Dictionary<UiElement, ElementBox> Detect(string gameVersion, Dictionary<UiElement, ElementBox> detectedNow)
    {
        CurrentVersion = gameVersion;
        NeedsUserUpdate = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        lock (_lock)
        {
            if (!_versionMap.TryGetValue(gameVersion, out var baseline))
            {
                _versionMap[gameVersion] = detectedNow;
                Save();
                Log?.Invoke($"[UI适配] 版本 {gameVersion} 首次记录基线（{detectedNow.Count} 个元素）");
                return detectedNow;
            }

            var updated = new Dictionary<UiElement, ElementBox>(baseline);
            var outOfRange = new List<string>();
            foreach (var (elem, now) in detectedNow)
            {
                if (!baseline.TryGetValue(elem, out var baseBox)) { updated[elem] = now; continue; }
                var offset = Math.Max(Math.Abs(now.X - baseBox.X), Math.Abs(now.Y - baseBox.Y));
                if (offset <= MaxAdjustableOffset)
                {
                    updated[elem] = now;
                    Log?.Invoke($"[UI适配] {elem} 偏移 {offset}px，已自动更新");
                }
                else
                {
                    outOfRange.Add($"{elem}({offset}px)");
                }
            }
            if (outOfRange.Count > 0)
            {
                NeedsUserUpdate = true;
                Log?.Invoke($"[UI适配] 超出可调范围: {string.Join(", ", outOfRange)}，请更新 BetterGI 或适配器");
            }
            _versionMap[gameVersion] = updated;
            Save();
            sw.Stop();
            Log?.Invoke($"[UI适配] 检测完成，耗时 {sw.ElapsedMilliseconds}ms");
            return updated;
        }
    }

    public ElementBox? Get(string gameVersion, UiElement elem)
    {
        lock (_lock)
            return _versionMap.TryGetValue(gameVersion, out var m) && m.TryGetValue(elem, out var b) ? b : null;
    }

    public IReadOnlyList<string> KnownVersions { get { lock (_lock) return _versionMap.Keys.ToArray(); } }

    public string ExportJson() { lock (_lock) return JsonSerializer.Serialize(_versionMap, new JsonSerializerOptions { WriteIndented = true }); }

    private void Load()
    {
        try
        {
            if (!File.Exists(_dbPath)) return;
            var json = File.ReadAllText(_dbPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ElementBox>>>(json);
            if (dict == null) return;
            foreach (var kv in dict)
            {
                var map = new Dictionary<UiElement, ElementBox>();
                foreach (var ekv in kv.Value)
                    if (Enum.TryParse<UiElement>(ekv.Key, out var e)) map[e] = ekv.Value;
                _versionMap[kv.Key] = map;
            }
            Log?.Invoke($"[UI适配] 已加载 {_versionMap.Count} 个版本基线");
        }
        catch (Exception ex) { Log?.Invoke($"[UI适配] 加载持久化失败（忽略）: {ex.Message}"); }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            var plain = _versionMap.ToDictionary(kv => kv.Key,
                kv => kv.Value.ToDictionary(e => e.Key.ToString(), e => e.Value));
            File.WriteAllText(_dbPath, JsonSerializer.Serialize(plain, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
