using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BetterGIProWpf.Services.BetterGI;

public static class BetterGIScriptGenerator
{
    public sealed record TaskItem(string Type, string Goal, string? Location, string? Action);

    public static string Generate(string outputRoot, string scriptName, IReadOnlyList<TaskItem> tasks, string? videoSource = null)
    {
        var dir = Path.Combine(outputRoot, scriptName);
        Directory.CreateDirectory(dir);
        var manifest = new JsonObject
        {
            ["manifest_version"] = 1,
            ["name"] = scriptName,
            ["version"] = "1.0.0",
            ["description"] = $"由 BetterGI Pro WPF 视频解析生成（来源: {videoSource ?? "未知"}）",
            ["main"] = "main.js",
            ["http_allowed_urls"] = new JsonArray("https://*", "http://localhost:*"),
        };
        File.WriteAllText(Path.Combine(dir, "manifest.json"), manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var steps = new JsonArray();
        foreach (var t in tasks)
        {
            steps.Add(new JsonObject { ["type"] = t.Type, ["goal"] = t.Goal, ["location"] = t.Location ?? "", ["action"] = t.Action ?? "" });
        }
        var mainJs = $@"// BetterGI 脚本：{scriptName}
const TASKS = {steps.ToJsonString()};
let cancelled = false;
const log = (msg) => {{ try {{ BetterGI.Log(msg); }} catch (e) {{ console.log(msg); }} }};
try {{ if (typeof BetterGI !== 'undefined' && BetterGI.OnCancel) BetterGI.OnCancel(() => {{ cancelled = true; }}); }} catch (e) {{ }}
async function runTask(task, index) {{
  if (cancelled) return;
  log(`[${{index + 1}}/${{TASKS.length}}] ${{task.goal}}`);
  try {{
    await BetterGI.Sleep(500);
    switch (task.type) {{
      case 'teleport': await BetterGI.KeyPress('M'); break;
      case 'dialogue': await BetterGI.KeyPress('F'); break;
      case 'combat': await BetterGI.KeyPress('E'); break;
      default: await BetterGI.KeyPress('W'); break;
    }}
  }} catch (err) {{ log('任务失败: ' + err); }}
}}
(async () => {{ for (let i = 0; i < TASKS.length; i++) {{ if (cancelled) break; await runTask(TASKS[i], i); }} }})();
";
        File.WriteAllText(Path.Combine(dir, "main.js"), mainJs);
        return dir;
    }
}
