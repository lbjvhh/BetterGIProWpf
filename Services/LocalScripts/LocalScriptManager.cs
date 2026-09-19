using System.IO; using System.Text.Json; namespace BetterGIProWpf.Services.LocalScripts;
public enum ScriptKind { JsScript, AutoPathing, AutoFight, GeniusInvokation, KeyMouseScript, ScriptGroup }
public record ScriptEntry(ScriptKind Kind, string Name, string Path, string Version, string Author);
public record ScriptStatus(string Name, bool Running, string Step, bool Cancelled);
public class LocalScriptManager {
    public string UserRoot { get; }
    private readonly Dictionary<ScriptKind, string> _dirs; private readonly Dictionary<string, ScriptStatus> _st = new(); private readonly object _lock = new();
    public LocalScriptManager(string root) { UserRoot = root; _dirs = new() { [ScriptKind.JsScript]=Path.Combine(root,"JsScript"), [ScriptKind.AutoPathing]=Path.Combine(root,"AutoPathing"), [ScriptKind.AutoFight]=Path.Combine(root,"AutoFight"), [ScriptKind.KeyMouseScript]=Path.Combine(root,"KeyMouseScript"), [ScriptKind.ScriptGroup]=Path.Combine(root,"ScriptGroup") }; }
    public List<ScriptEntry> Scan() { var r = new List<ScriptEntry>(); foreach (var (k,d) in _dirs) { if (!Directory.Exists(d)) continue; foreach (var f in Directory.EnumerateFiles(d,"*.*",SearchOption.TopDirectoryOnly)) { var ext = Path.GetExtension(f).ToLowerInvariant(); if (ext is ".json" or ".js") r.Add(new ScriptEntry(k, Path.GetFileNameWithoutExtension(f), f, "-", "-")); } } return r; }
    public bool Execute(string path, string? arg=null, Action<string>? log=null) { var n = Path.GetFileNameWithoutExtension(path); if (!File.Exists(path)) { log?.Invoke($"[{n}] 不存在"); return false; } Set(n,true,"starting",false); try { log?.Invoke($"[{n}] 执行"); System.Threading.Thread.Sleep(120); Set(n,false,"done",false); return true; } catch (Exception ex) { Set(n,false,"failed",false); log?.Invoke($"[{n}] {ex.Message}"); return false; } }
    public void Cancel(string n) { lock (_lock) if (_st.TryGetValue(n, out var s)) _st[n] = s with { Cancelled=true, Running=false }; }
    public ScriptStatus? Status(string n) { lock (_lock) return _st.GetValueOrDefault(n); }
    private void Set(string n, bool r, string s, bool c) { lock (_lock) _st[n] = new ScriptStatus(n,r,s,c); }
}
