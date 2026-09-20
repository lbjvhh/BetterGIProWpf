using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BetterGIProWpf.Services.Scripting;

public class NaturalLanguageScriptGenerator
{
    private static readonly Dictionary<string, string> KeywordMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["开盾"] = "skill_e", ["元素战技"] = "skill_e", ["E"] = "skill_e",
        ["元素爆发"] = "burst_q", ["Q"] = "burst_q", ["大招"] = "burst_q",
        ["普攻"] = "attack_normal", ["打"] = "attack_normal", ["攻击"] = "attack_normal",
        ["重击"] = "attack_charged", ["冲刺"] = "dash", ["跑"] = "run",
        ["跳"] = "jump", ["切换"] = "switch", ["换人"] = "switch",
        ["等待"] = "wait", ["等"] = "wait",
        ["捡"] = "pickup", ["拾取"] = "pickup",
        ["交互"] = "interact", ["对话"] = "talk", ["钓鱼"] = "fish",
    };

    public string Generate(string naturalLanguage)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage)) return "";
        var lines = new List<string> { $"# 自动生成: {naturalLanguage}", "" };
        var separators = new[] { '，', ',', '。', '；', ';', '\n', '\r' };
        var clauses = naturalLanguage.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        int step = 1;
        foreach (var clause in clauses)
        {
            var trimmed = clause.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var action = MatchAction(trimmed);
            if (action != null) { lines.Add($"# 步骤 {step}: {trimmed}"); lines.Add(action); lines.Add(""); step++; }
            else lines.Add($"# [未识别] {trimmed}");
        }
        lines.Add("# 生成完毕");
        return string.Join(Environment.NewLine, lines);
    }

    private string? MatchAction(string clause)
    {
        foreach (var kv in KeywordMap) if (clause.Contains(kv.Key)) return kv.Value;
        return null;
    }

    public string SaveToFile(string naturalLanguage, string path)
    {
        var script = Generate(naturalLanguage);
        File.WriteAllText(path, script, Encoding.UTF8);
        return path;
    }
}
