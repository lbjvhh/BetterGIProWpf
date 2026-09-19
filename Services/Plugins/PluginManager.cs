using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;

namespace BetterGIProWpf.Services.Plugins;

public sealed class PluginManager
{
    private readonly List<IGamePlugin> _plugins = new();

    public static IReadOnlyList<string> PluginDirs()
    {
        var dirs = new List<string>();
        try { dirs.Add(Path.Combine(AppContext.BaseDirectory, "Plugins")); } catch { }
        try { dirs.Add(Path.Combine(AppContext.BaseDirectory, "User", "Plugins")); } catch { }
        return dirs;
    }

    public PluginManager() { _plugins.AddRange(BuiltinPlugins.CreateAll()); }
    public IReadOnlyList<IGamePlugin> Plugins => _plugins;
    public IGamePlugin? Get(string name) => _plugins.FirstOrDefault(p => p.Name == name);
    public int EnabledCount => _plugins.Count(p => p.Status == PluginStatus.Enabled || p.Status == PluginStatus.Loaded);

    public string Enable(string name)
    {
        var p = Get(name); if (p is null) return $"未找到 {name}";
        var err = p.Load(); return err is not null ? $"启用失败: {err}" : $"已启用 {name}";
    }

    public string Disable(string name)
    {
        var p = Get(name); if (p is null) return $"未找到 {name}";
        if (!p.CanDisable) return $"{name} 不可禁用";
        p.Unload(); return $"已禁用 {name}";
    }

    public int ScanExternal()
    {
        int added = 0;
        foreach (var dir in PluginDirs())
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var dll in Directory.GetFiles(dir, "*.dll"))
            {
                try
                {
                    var asm = Assembly.LoadFrom(dll);
                    foreach (var type in asm.GetTypes().Where(t => !t.IsAbstract && typeof(IGamePlugin).IsAssignableFrom(t)))
                    {
                        if (Activator.CreateInstance(type) is IGamePlugin p && _plugins.All(x => x.Name != p.Name)) { _plugins.Add(p); added++; }
                    }
                }
                catch { }
            }
        }
        return added;
    }

    public string Summary() => $"启用 {EnabledCount} 个插件";
}
