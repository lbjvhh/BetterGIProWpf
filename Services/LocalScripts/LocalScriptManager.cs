using System.IO;
using System.Text.Json;

namespace BetterGIProWpf.Services.LocalScripts;

public enum ScriptKind { JsScript, AutoPathing, AutoFight, GeniusInvokation, KeyMouseScript, ScriptGroup }
public record ScriptEntry(ScriptKind Kind, string Name, string Path, string Version, string Author, string MinGameVersion);
public record ScriptStatus(string Name, bool Running, string CurrentStep, bool Cancelled);

public class LocalScriptManager
{
    public string UserRoot { get; }
    private readonly Dictionary<ScriptKind, string> _dirs;
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

    public List<ScriptEntry> Scan()
    {
        var result = new List<ScriptEntry>();
        foreach (var (kind, dir) in _dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".json" or ".js") result.Add(Describe(kind, file));
            }
        }
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
        try { System.Threading.Thread.Sleep(120); SetStatus(name, false, "done", false); return true; }
        catch (Exception ex) { SetStatus(name, false, $"failed: {ex.Message}", false); return false; }
    }

    public void Cancel(string name) { lock (_lock) if (_status.TryGetValue(name, out var s)) _status[name] = s with { Cancelled = true, Running = false }; }
    public ScriptStatus? Status(string name) { lock (_lock) return _status.GetValueOrDefault(name); }
    private void SetStatus(string name, bool running, string step, bool cancelled) { lock (_lock) _status[name] = new ScriptStatus(name, running, step, cancelled); }
}
