namespace BetterGIProWpf.Services.Plugins;
public enum PluginKind { Builtin, Assembly, Script, Config }
public enum PluginStatus { Loaded, Enabled, Disabled, Error }
public interface IGamePlugin {
    string Name { get; } string Version { get; } string Author { get; } string Description { get; } string MinBetterGiVersion { get; } PluginKind Kind { get; } PluginStatus Status { get; } bool CanDisable { get; }
    string? Load(); string? Unload(); string StatusText();
}
public sealed record PluginInfo(string Name, string Version, string Author, string Description, string MinVer, PluginKind Kind, string Src, bool CanDisable=true);
