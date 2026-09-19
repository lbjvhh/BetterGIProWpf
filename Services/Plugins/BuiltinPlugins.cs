namespace BetterGIProWpf.Services.Plugins;
public static class BuiltinPlugins {
    public sealed class Core : IGamePlugin {
        public Core(string n,string d,bool dis=false){Name=n;Desc=d;CanDisable=dis;}
        public string Name {get;} public string Version=>"1.0.0"; public string Author=>"BetterGI Pro"; public string Desc; public string Description=>Desc; public string MinBetterGiVersion=>"0.0.0"; public PluginKind Kind=>PluginKind.Builtin; public PluginStatus Status {get;private set;}=PluginStatus.Loaded; public bool CanDisable {get;}
        public string? Load(){Status=PluginStatus.Enabled;return null;} public string? Unload(){Status=PluginStatus.Disabled;return null;} public string StatusText()=>"builtin";
    }
    public static IReadOnlyList<IGamePlugin> All()=>new List<IGamePlugin>{
        new Core("core.localai","LocalAI offline OCR/TTS/ASR/KB/intent/QA/translate"),
        new Core("core.nitrogen","NitroGen VLA 21x16 action block + event gate + confidence"),
        new Core("core.plugins","Plugin manager: builtin + external dll/manifest"),
        new Core("mod.ai-companion","AI companion (Module 1)",true),
        new Core("mod.coach","Real-time coach (Module 2)",true),
        new Core("mod.highlight","Highlight recorder (Module 3)",true),
        new Core("mod.knowledge","KB semantic search (Module 4)",true),
        new Core("mod.cross-game","Cross-game adapter (Module 5)",true),
        new Core("mod.rule-extract","Rule extractor (Module 6)",true),
        new Core("mod.community","Community script version mgmt (Module 7)",true),
        new Core("mod.desktop-clone","Desktop mirror (Module 8)",true),
        new Core("mod.state-qa","Multimodal QA (Module 9)",true),
        new Core("mod.voice-dubbing","Auto dubbing (Module 10)",true),
        new Core("mod.dashboard","Dashboard metrics (Module 11)",true),
        new Core("mod.regression","Regression suite (Module 12)",true),
        new Core("mod.resource","Resource advisor (Module 13)",true),
        new Core("mod.translate","Multilingual translate (Module 14)",true),
        new Core("mod.item-manager","Item manager (Module 15)",true),
        new Core("mod.emergency","Emergency stop (Module 16)",true),
        new Core("mod.audio-scene","Audio scene classifier (Module 17)",true),
        new Core("mod.replay","Replay recorder (Module 18)",true),
        new Core("mod.script-repo","Script repo direct (Module 19)",true),
        new Core("mod.local-scripts","Local script loader (Module 20)",true),
        new Core("mod.runtime-adapt","Runtime adaptation (Module 21)",true),
        new Core("mod.diag-repair","Diag repair (Module 22)",true),
        new Core("mod.stuck-escape","Stuck escape (Module 23)",true),
        new Core("mod.network-adapt","Network adapt (Module 24)",true),
        new Core("mod.ui-adapt","UI layout adapt (Module 25)",true),
        new Core("mod.humanize","Humanize input (Module 26)",true),
        new Core("svc.capture-failover","Capture failover WGC/BitBlt/DXGI",true),
        new Core("svc.version-matrix","Version compatibility matrix",true),
        new Core("svc.security-check","Security software check",true),
        new Core("svc.adaptive-controller","Central adaptive controller",true),
    };
}
