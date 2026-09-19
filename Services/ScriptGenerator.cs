using System.IO; using System.Text.Json; namespace BetterGIProWpf.Services;
public class ScriptGenerator {
    private readonly string _root;
    public ScriptGenerator(string? root=null){_root=root??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"BetterGIProWpf","ScriptGroup");Directory.CreateDirectory(_root);}
    public string Gen(string name,List<OperationStep> steps,string src=""){var dir=Path.Combine(_root,name);Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"manifest.json"),JsonSerializer.Serialize(new{manifest_version=1,name,version="1.0.0",main="main.js",description="auto",video_source=src,http_allowed_urls=new[]{"https://*"}}));var sb=new System.Text.StringBuilder();sb.AppendLine("module.exports=async function(ctx){try{");foreach(var s in steps){sb.AppendLine($"await ctx.wait({s.Duration});if(ctx&&ctx.isCancelled&&ctx.isCancelled())return;");}sb.AppendLine("}catch(e){console.log(e);}}");File.WriteAllText(Path.Combine(dir,"main.js"),sb.ToString());return dir;}
}
