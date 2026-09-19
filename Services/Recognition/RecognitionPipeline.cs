using System.Text; using BetterGIProWpf.Services.NitroGen; namespace BetterGIProWpf.Services.Recognition;
public sealed class RecognitionPipeline {
    public sealed record Result(string Ch,List<KeyOverlay.KeyEvent> Ev,List<BattleScriptGenerator.Cmd> Cmds,string Txt,string Rep);
    public static Result ChannelA(RgbFrame[] fr,double fps,Dictionary<string,byte[]> tmpl,int tw,int th,string root,string name,Dictionary<string,(int,int)>? reg=null){
        var ex=new KeyOverlay.Extractor();var ev=ex.Extract(fr,fps,tmpl,tw,th);var cmds=BattleScriptGenerator.FromTimeline(ev);var txt=BattleScriptGenerator.ToTxt(cmds);var path=BattleScriptGenerator.Save(root,name,cmds);var sb=new StringBuilder();sb.AppendLine($"Channel A: {ev.Count} events -> {cmds.Count} cmds");sb.AppendLine($"Saved: {path}");return new Result("A-key-overlay",ev,cmds,txt,sb.ToString());
    }
    public static Result ChannelB(RgbFrame[] fr,Func<RgbFrame[],(string Act,double Dur,double Conf)>? vlm=null){
        if(vlm!=null&&fr.Length>=2){var(a,d,c)=vlm(fr);var cmds=new List<BattleScriptGenerator.Cmd>{new(a,Math.Max(0.3,d))};var txt=BattleScriptGenerator.ToTxt(cmds);return new Result("B-vlm",new(),cmds,txt,$"VLM: {a} dur={d:0.###} conf={c:0.00}");}
        if(fr.Length>=2){var eng=new LocalVisionEngine();var r=eng.Infer(new[]{fr[0],fr[^1]});var cmds=new List<BattleScriptGenerator.Cmd>();if(r.Actions.IsButton(0,NitroGenConst.Btn.A))cmds.Add(new("attack",0.8));if(Math.Abs(r.Actions.LeftY(0)-0.5f)>0.05f)cmds.Add(new("walk",1.0));var txt=BattleScriptGenerator.ToTxt(cmds);return new Result("B-local",new(),cmds,txt,$"Local NitroGen mode={r.Mode} conf={r.Confidence:0.00}");}
        return new Result("none",new(),new(),"","frames<2");
    }
}
