using System.Text; namespace BetterGIProWpf.Services.Recognition;
public static class BattleScriptGenerator {
    public sealed record Cmd(string Op, double Dur, string? Param=null){public override string ToString()=>Param==null?$"{Op};{Dur:0.###}":$"{Op};{Dur:0.###};{Param}";}
    public static string MapKey(string k)=>k.ToUpperInvariant() switch{"A"or"LMB"=>"attack","Q"=>"burst","E"=>"skill","SPACE"=>"jump","SHIFT"=>"sprint","F"=>"interact",_=>"walk"};
    public static List<Cmd> FromTimeline(IEnumerable<KeyOverlay.KeyEvent> ev, double min=0.15){
        var cmds=new List<Cmd>();var down=new Dictionary<string,double>();
        foreach(var e in ev.OrderBy(e=>e.TimeSec)){if(e.Pressed)down[e.Key]=e.TimeSec;else if(down.TryGetValue(e.Key,out var s)){double d=e.TimeSec-s;down.Remove(e.Key);if(d<min)continue;cmds.Add(new Cmd(MapKey(e.Key),Math.Max(0.2,d)));}}
        return cmds;
    }
    public static string ToTxt(IEnumerable<Cmd> cmds){var sb=new StringBuilder();sb.AppendLine("# BetterGI AutoFight");foreach(var c in cmds)sb.AppendLine(c.ToString());return sb.ToString();}
    public static string Save(string root,string file,IEnumerable<Cmd> cmds){var dir=Path.Combine(root,"AutoFight");Directory.CreateDirectory(dir);var p=Path.Combine(dir,file.EndsWith(".txt",StringComparison.OrdinalIgnoreCase)?file:file+".txt");File.WriteAllText(p,ToTxt(cmds),Encoding.UTF8);return p;}
}
