namespace BetterGIProWpf.Services.NitroGen;
public sealed class PlannerGuard {
    public enum Phase { Navigate, Battle, Collect, Dialogue, Idle }
    public sealed record Step(string Action, double Dur, string? Param=null);
    public static List<Step> Bind(string intent, Phase ph) {
        var s = new List<Step>(); var n = intent.ToLowerInvariant();
        if (n.Contains("打")||n.Contains("杀")||n.Contains("boss")) { s.Add(new("battle",3.0)); if(n.Contains("e"))s.Add(new("skill",1.0)); if(n.Contains("q"))s.Add(new("burst",1.0)); }
        else if (n.Contains("采集")||n.Contains("矿")) { s.Add(new("walk",2.0)); s.Add(new("interact",0.8)); }
        else if (n.Contains("传送")||n.Contains("锚点")) { s.Add(new("openmap",0.6)); s.Add(new("teleport",2.0)); }
        else if (n.Contains("对话")||n.Contains("npc")) { s.Add(new("walk",1.0)); s.Add(new("interact",0.6)); }
        else s.Add(new("walk",1.5));
        if (ph is Phase.Dialogue or Phase.Idle) s = s.Where(x=>x.Action!="battle").ToList();
        return s;
    }
    public static bool CallVla(Phase p) => p is Phase.Battle;
    public static Phase Classify(bool warm,bool menu,bool dial) => dial?Phase.Dialogue:menu?Phase.Idle:warm?Phase.Battle:Phase.Navigate;
}
