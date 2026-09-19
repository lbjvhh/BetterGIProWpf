using System.IO;
using System.Text.Json;

namespace BetterGIProWpf.Services;

public class ScriptGenerator
{
    private readonly string _scriptRoot;

    public ScriptGenerator(string? scriptRoot = null)
    {
        _scriptRoot = scriptRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BetterGIProWpf", "ScriptGroup");
        Directory.CreateDirectory(_scriptRoot);
    }

    public string Generate(string scriptName, List<OperationStep> steps, string sourceVideo = "")
    {
        var dir = Path.Combine(_scriptRoot, SafeName(scriptName));
        Directory.CreateDirectory(dir);
        var manifest = new
        {
            manifest_version = 1,
            name = scriptName,
            version = "1.0.0",
            main = "main.js",
            description = "由视频自动解析生成",
            video_source = sourceVideo,
            generated_at = DateTime.UtcNow.ToString("o"),
            http_allowed_urls = new[] { "https://*", "http://*" },
        };
        File.WriteAllText(Path.Combine(dir, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// 自动生成的操作脚本");
        sb.AppendLine("module.exports = async function (ctx) {");
        sb.AppendLine("  try {");
        foreach (var s in steps)
        {
            sb.AppendLine($"    // {s.Desc ?? s.Type}");
            sb.AppendLine(ToJs(s));
            sb.AppendLine("    if (ctx && ctx.isCancelled && ctx.isCancelled()) return;");
        }
        sb.AppendLine("  } catch (e) { console.log('脚本异常: ' + e); }");
        sb.AppendLine("};");
        File.WriteAllText(Path.Combine(dir, "main.js"), sb.ToString());
        return dir;
    }

    private static string ToJs(OperationStep s) => s.Type switch
    {
        "key" => $"    await ctx.keyPress('{s.Key?.ToLower()}');",
        "click" => $"    await ctx.click({s.X}, {s.Y});",
        "move" => $"    await ctx.move({s.X}, {s.Y});",
        _ => $"    await ctx.wait({s.Duration});",
    };

    private static string SafeName(string s) =>
        string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '_' ? c : '_'));
}
