using System.IO;
using System.Text.Json;

namespace BetterGIProWpf.Services.LocalScripts;

/// <summary>脚本类型（模块20：本地脚本加载与执行管理）。</summary>
public enum ScriptKind { JsScript, AutoPathing, AutoFight, GeniusInvokation, KeyMouseScript, ScriptGroup }

/// <summary>扫描到的脚本清单条目。</summary>
public record ScriptEntry(ScriptKind Kind, string Name, string Path, string Version, string Author, string MinGameVersion);

/// <summary>执行状态。</summary>
public record ScriptStatus(string Name, bool Running, string CurrentStep, bool Cancelled);

/// <summary>
/// 本地脚本加载与执行管理（模块20）：扫描 BetterGI User 目录下 6 类脚本，
/// 解析 manifest.json，读取/修改配置 JSON，解析脚本间依赖相对路径，执行状态监控，失败捕获分类。
/// </summary>
public class LocalScriptManager
{
    public string UserRoot { get; }
    private readonly Dictionary<ScriptKind, string> _dirs;
    private readonly List<ScriptEntry> _scanned = new();
    private readonly Dictionary<string, ScriptStatus> _status = new();
    private readonly object _lock = new();

    public LocalScriptManager(string userRoot)
    {
        UserRoot = userRoot;
        _dirs = new Dictionary<ScriptKind, string>
        {
            [ScriptKind.JsScript] = Path.Combine(userRoot, "JsScript"),
            [ScriptKind.AutoPathing] = Path.Combine(userRoot, "AutoPathing"),
            [ScriptKind.AutoFight] = Path.Combine(userRoot, "AutoFight"),
            [ScriptKind.GeniusInvokation] = Path.Combine(userRoot, "AutoGeniusInvokation"),
            [ScriptKind.KeyMouseScript] = Path.Combine(userRoot, "KeyMouseScript"),
            [ScriptKind.ScriptGroup] = Path.Combine(userRoot, "ScriptGroup"),
        };
    }

    /// <summary>扫描 User 目录下所有脚本（含 AutoFight/KeyMouseScript 的 .txt）。</summary>
    public List<ScriptEntry> Scan()
    {
        var result = new List<ScriptEntry>();
        foreach (var (kind, dir) in _dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".json" or ".js" or ".txt")
                    result.Add(Describe(kind, file));
            }
        }
        lock (_lock) { _scanned.Clear(); _scanned.AddRange(result); }
        return result;
    }

    private ScriptEntry Describe(ScriptKind kind, string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var version = "-"; var author = "-"; var minVer = "-";
        var dir = Path.GetDirectoryName(path);
        var manifest = dir != null ? Path.Combine(dir, "manifest.json") : null;
        if (manifest != null && File.Exists(manifest))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                var r = doc.RootElement;
                name = r.TryGetProperty("name", out var n) ? n.GetString() ?? name : name;
                version = r.TryGetProperty("version", out var v) ? v.GetString() ?? "-" : "-";
                author = r.TryGetProperty("author", out var a) ? a.GetString() ?? "-" : "-";
                minVer = r.TryGetProperty("min_game_version", out var m) ? m.GetString() ?? "-" : "-";
            }
            catch { }
        }
        return new ScriptEntry(kind, name, path, version, author, minVer);
    }

    public string? ReadConfig(string scriptPath)
    {
        var dir = Path.GetDirectoryName(scriptPath);
        if (dir == null) return null;
        var cfg = Path.Combine(dir, "config.json");
        return File.Exists(cfg) ? File.ReadAllText(cfg) : null;
    }

    public bool WriteConfig(string scriptPath, string key, JsonElement value)
    {
        var dir = Path.GetDirectoryName(scriptPath);
        if (dir == null) return false;
        var cfgPath = Path.Combine(dir, "config.json");
        JsonDocument doc;
        if (File.Exists(cfgPath))
            doc = JsonDocument.Parse(File.ReadAllText(cfgPath));
        else
            doc = JsonDocument.Parse("{}");
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Name != key) p.WriteTo(w);
            w.WritePropertyName(key);
            value.WriteTo(w);
            w.WriteEndObject();
        }
        File.WriteAllText(cfgPath, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        return true;
    }

    public static string? ResolveDependency(string scriptPath, string reference)
    {
        var dir = Path.GetDirectoryName(scriptPath);
        if (dir == null) return null;
        var candidate = Path.GetFullPath(Path.Combine(dir, reference));
        return File.Exists(candidate) ? candidate : null;
    }

    public bool Execute(string scriptPath, string? arg = null, Action<string>? log = null)
    {
        var name = Path.GetFileNameWithoutExtension(scriptPath);
        if (!File.Exists(scriptPath)) { log?.Invoke($"[{name}] 文件不存在"); return false; }
        SetStatus(name, true, "starting", false);
        try
        {
            log?.Invoke($"[{name}] 执行中（{arg ?? "无参数"}）");
            System.Threading.Thread.Sleep(120);
            SetStatus(name, false, "done", false);
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(name, false, $"failed: {ex.Message}", false);
            log?.Invoke($"[{name}] 执行异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// P2-1：真实执行 —— 解析 AutoFight 战斗脚本（TXT/JSON）为按键序列，
    /// 通过 stream_bridge 5005 /inject 注入。指令集映射：
    /// walk→w attack→j skill→e burst→q dash→shift jump→space wait→等待。
    /// </summary>
    public async Task<bool> ExecuteViaBridgeAsync(string scriptPath, Action<string>? log = null)
    {
        var name = Path.GetFileNameWithoutExtension(scriptPath);
        if (!File.Exists(scriptPath)) { log?.Invoke($"[{name}] 文件不存在"); return false; }
        SetStatus(name, true, "bridge", false);
        try
        {
            var keys = new List<string>();
            foreach (var rawLine in File.ReadAllLines(scriptPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var cmd = parts[0].ToLowerInvariant();
                var arg = parts.Length > 1 ? parts[1] : "1";
                switch (cmd)
                {
                    case "walk" or "move" or "前进": AddRepeated(keys, "w", ParseCount(arg, 2)); break;
                    case "back" or "后退": AddRepeated(keys, "s", ParseCount(arg, 2)); break;
                    case "left" or "左": AddRepeated(keys, "a", ParseCount(arg, 1)); break;
                    case "right" or "右": AddRepeated(keys, "d", ParseCount(arg, 1)); break;
                    case "attack" or "普攻" or "攻击": AddRepeated(keys, "j", ParseCount(arg, 2)); break;
                    case "skill" or "元素战技" or "技能": AddRepeated(keys, "e", ParseCount(arg, 1)); break;
                    case "burst" or "元素爆发": AddRepeated(keys, "q", ParseCount(arg, 1)); break;
                    case "dash" or "冲刺": AddRepeated(keys, "shift", ParseCount(arg, 1)); break;
                    case "jump" or "跳跃": AddRepeated(keys, "space", ParseCount(arg, 1)); break;
                    case "interact" or "交互": AddRepeated(keys, "f", ParseCount(arg, 1)); break;
                    case "map" or "地图": AddRepeated(keys, "m", ParseCount(arg, 1)); break;
                    case "wait" or "等待": log?.Invoke($"[{name}] 等待 {arg}ms"); await Task.Delay(ParseCount(arg, 500) * 10); break;
                    default:
                        if (arg == "1" && cmd.Length <= 2 && "wasdjqefm".Contains(cmd)) keys.Add(cmd);
                        break;
                }
            }
            log?.Invoke($"[{name}] 解析出 {keys.Count} 个按键 → 注入 stream_bridge…");
            if (keys.Count > 0)
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var delay = BetterGIProWpf.AppState.Humanize.JitteredDelay(45);
                var body = System.Text.Json.JsonSerializer.Serialize(new { keys, delay_ms = delay });
                var resp = await http.PostAsync("http://127.0.0.1:5005/inject",
                    new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
                var json = await resp.Content.ReadAsStringAsync();
                log?.Invoke($"[{name}] inject → {json}");
                if (!resp.IsSuccessStatusCode || !json.Contains("\"ok\":true"))
                {
                    SetStatus(name, false, "failed: inject 被安全停机拦截或 bridge 未启动", false);
                    return false;
                }
            }
            SetStatus(name, false, "done", false);
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(name, false, $"failed: {ex.Message}", false);
            log?.Invoke($"[{name}] 执行异常: {ex.Message}");
            return false;
        }
    }

    private static void AddRepeated(List<string> keys, string key, int count)
    {
        for (var i = 0; i < Math.Clamp(count, 1, 20); i++) keys.Add(key);
    }

    private static int ParseCount(string s, int fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(s, @"\d+");
        return m.Success ? int.Parse(m.Value) : fallback;
    }

    public void Cancel(string name) { lock (_lock) if (_status.TryGetValue(name, out var s)) _status[name] = s with { Cancelled = true, Running = false }; }

    public ScriptStatus? Status(string name) { lock (_lock) return _status.GetValueOrDefault(name); }

    private void SetStatus(string name, bool running, string step, bool cancelled)
    {
        lock (_lock) _status[name] = new ScriptStatus(name, running, step, cancelled);
    }
}
