namespace BetterGIProWpf.Services.Plugins;

public static class BuiltinPlugins
{
    public sealed class Core : IGamePlugin
    {
        public Core(string name, string desc, bool canDisable = false) { Name = name; Description = desc; CanDisable = canDisable; }
        public string Name { get; }
        public string Version => "1.0.0";
        public string Author => "BetterGI Pro";
        public string Description { get; }
        public string MinBetterGiVersion => "0.0.0";
        public PluginKind Kind => PluginKind.Builtin;
        public PluginStatus Status { get; private set; } = PluginStatus.Loaded;
        public bool CanDisable { get; }
        public string? Load() { Status = PluginStatus.Enabled; return null; }
        public string? Unload() { Status = PluginStatus.Disabled; return null; }
        public string StatusText() => "内置核心插件";
    }

    public static IReadOnlyList<IGamePlugin> CreateAll() => new List<IGamePlugin>
    {
        new Core("core.localai", "本地 AI 引擎"),
        new Core("core.nitrogen", "NitroGen 端到端引擎"),
        new Core("mod.ai-companion", "AI 游戏队友", true),
        new Core("mod.coach", "策略教练", true),
        new Core("mod.highlight", "高光剪辑", true),
        new Core("mod.knowledge", "知识库", true),
        new Core("mod.cross-game", "跨游戏适配", true),
        new Core("mod.desktop-clone", "桌面分身", true),
        new Core("mod.state-qa", "状态问答", true),
        new Core("mod.dashboard", "仪表盘", true),
        new Core("mod.regression", "回归测试", true),
        new Core("mod.resource", "资源管理", true),
        new Core("mod.translate", "多语言翻译", true),
        new Core("mod.item-manager", "装备识别", true),
        new Core("mod.emergency", "安全停机", true),
        new Core("mod.replay", "回放对比", true),
        new Core("mod.script-repo", "脚本仓库直连", true),
        new Core("mod.local-scripts", "本地脚本管理", true),
        new Core("mod.runtime-adapt", "运行时自适应", true),
        new Core("mod.stuck-escape", "卡死脱离", true),
        new Core("mod.network-adapt", "网络自适应", true),
        new Core("mod.ui-adapt", "UI 自动适配", true),
        new Core("mod.humanize", "输入拟人化", true),
        new Core("svc.capture-failover", "截图故障切换", true),
        new Core("svc.version-matrix", "版本兼容矩阵", true),
        new Core("svc.adaptive-controller", "中央自适应控制器", true),
    };
}
