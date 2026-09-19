using System.Reflection; using System.Text.Json.Nodes; namespace BetterGIProWpf.Services.Plugins;
public sealed class PluginManager {
    private readonly List<IGamePlugin> _p = new();
    public PluginManager() { _p.AddRange(BuiltinPlugins.CreateAll()); }
    public IReadOnlyList<IGamePlugin> Plugins => _p;
    public IGamePlugin? Get(string n) => _p.FirstOrDefault(x => x.Name == n);
    public string Enable(string n) { var p=Get(n); if(p==null)return $"未找到 {n}"; var e=p.Load(); return e==null?$"已启用 {n}":$"启用失败:{e}"; }
    public string Disable(string n) { var p=Get(n); if(p==null)return $"未找到 {n}"; if(!p.CanDisable)return $"核心插件不可禁用"; p.Unload(); return $"已禁用 {n}"; }
    public int ScanExternal() { int added=0; foreach (var dir in new[]{Path.Combine(AppContext.BaseDirectory,"Plugins"),Path.Combine(AppContext.BaseDirectory,"User","Plugins")}) { if(!Directory.Exists(dir))continue; foreach(var dll in Directory.GetFiles(dir,"*.dll")){try{var asm=Assembly.LoadFrom(dll);foreach(var t in asm.GetTypes().Where(x=>!x.IsAbstract&&typeof(IGamePlugin).IsAssignableFrom(x))){if(Activator.CreateInstance(t) is IGamePlugin p&&_p.All(x=>x.Name!=p.Name)){_p.Add(p);added++;}}}catch{}} foreach(var sub in Directory.GetDirectories(dir)){var m=Path.Combine(sub,"manifest.json");if(!File.Exists(m))continue;try{var n=JsonNode.Parse(File.ReadAllText(m))?["name"]?.GetValue<string>();if(!string.IsNullOrEmpty(n)&&_p.All(x=>x.Name!=n)){_p.Add(new ManifestPlugin(n,sub));added++;}}catch{}} } return added; }
    private sealed class ManifestPlugin : IGamePlugin {
        private readonly string _dir; public ManifestPlugin(string n,string d){Name=n;_dir=d;}
        public string Name { get; } public string Version { get; private set; } = "1.0"; public string Author { get; private set; } = ""; public string Description { get; private set; } = ""; public string MinBetterGiVersion { get; private set; } = "0"; public PluginKind Kind { get; private set; } = PluginKind.Script; public PluginStatus Status { get; private set; } = PluginStatus.Enabled; public bool CanDisable => true;
        public string? Load() { try{var n=JsonNode.Parse(File.ReadAllText(Path.Combine(_dir,"manifest.json")));Version=n?["version"]?.GetValue<string>()??Version;Author=n?["author"]?.GetValue<string>()??Author;Description=n?["description"]?.GetValue<string>()??Description;Status=PluginStatus.Enabled;return null;}catch(Exception e){Status=PluginStatus.Error;return e.Message;} }
        public string? Unload(){Status=PluginStatus.Disabled;return null;} public string StatusText()=>_dir;
    }
}
