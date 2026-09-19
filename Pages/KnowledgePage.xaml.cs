using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Knowledge;
using BetterGIProWpf.Services.LocalAI;

namespace BetterGIProWpf.Pages;

public partial class KnowledgePage : Page
{
    private readonly KnowledgeBase _kb = AppState.Knowledge;
    private readonly ResourceAdvisor _res = new();
    private readonly TranslationService _trans = new();

    public KnowledgePage()
    {
        InitializeComponent();
        LocalAiEngine.BindTranslation(_trans);
        _res.AddSpots(new[]
        {
            new ResourceAdvisor.ResourceSpot(ResourceAdvisor.ResourceKind.Crystal, "水晶块", 120, 80, 6),
            new ResourceAdvisor.ResourceSpot(ResourceAdvisor.ResourceKind.Crystal, "水晶块", 320, 240, 5),
            new ResourceAdvisor.ResourceSpot(ResourceAdvisor.ResourceKind.Ore, "白铁矿", 500, 300, 3),
        });
        AddDemoEntries();
    }

    private void AddDemoEntries()
    {
        _kb.Add(new KnowledgeEntry
        {
            VideoSource = "https://bilibili.com/video/BV123", TimestampSec = 45,
            TaskDescription = "传送到风起地七天神像", Location = "风起地", TaskType = "传送",
            VlmSummary = "从神像出发向北探索"
        });
        _kb.Add(new KnowledgeEntry
        {
            VideoSource = "https://bilibili.com/video/BV123", TimestampSec = 210,
            TaskDescription = "击败北风狼王，弱点是雷元素", Location = "奔狼领", TaskType = "战斗", BossName = "北风狼王",
            VlmSummary = "推荐雷电将军主C"
        });
        _kb.Add(new KnowledgeEntry
        {
            VideoSource = "https://youtube.com/watch?v=abc", TimestampSec = 90,
            TaskDescription = "采集水晶块路线", Location = "层岩巨渊", TaskType = "采集",
            VlmSummary = "每5分钟约30个水晶块"
        });
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        var q = SearchBox.Text.Trim();
        var filters = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(LocBox.Text.Trim())) filters["location"] = LocBox.Text.Trim();
        var hits = _kb.Search(q.Length > 0 ? q : "探索", top: 5, filters);
        KnowledgeLog.AppendText($"== 检索「{q}」==\n");
        if (hits.Count == 0) { KnowledgeLog.AppendText("  无结果\n"); return; }
        foreach (var h in hits.Take(5))
            KnowledgeLog.AppendText($"  [{h.Score:0.00}] {h.Entry.TaskDescription} @ {h.Entry.VideoSource}?t={(int)h.Entry.TimestampSec}（跳转链接）\n");
    }

    private void ToPath_Click(object sender, RoutedEventArgs e)
    {
        var json = KnowledgeBase.ToPathingJson(_kb.Filter());
        KnowledgeLog.AppendText("== 地图追踪 JSON（可导入 BetterGI AutoPathing）==\n" + json + "\n");
    }

    private void AddDemo_Click(object sender, RoutedEventArgs e) { AddDemoEntries(); KnowledgeLog.AppendText($"已添加示例条目，知识库现有 {_kb.Count} 条\n"); }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "knowledge_export.json");
        System.IO.File.WriteAllText(path, _kb.ExportJson());
        KnowledgeLog.AppendText($"已导出 {_kb.Count} 条到 {path}\n");
    }

    private void Resource_Click(object sender, RoutedEventArgs e)
    {
        int.TryParse(TargetBox.Text, out var target);
        var advice = _res.Advise(new[] { new ResourceAdvisor.Inventory("水晶块", 120) }, target > 0 ? target : -1);
        KnowledgeLog.AppendText("== 资源管理建议 ==\n");
        foreach (var a in advice) KnowledgeLog.AppendText("  " + a + "\n");
        var route = _res.PlanRoute(ResourceAdvisor.ResourceKind.Crystal, 2);
        KnowledgeLog.AppendText($"  推荐路线：{string.Join(" → ", route.Select(r => $"({r.X:0},{r.Y:0})"))}\n");
    }

    private async void Translate_Click(object sender, RoutedEventArgs e)
    {
        var t = await _trans.TranslateAsync(TransBox.Text, "en-US", "zh-CN");
        KnowledgeLog.AppendText($"== 翻译 ==\n  {TransBox.Text} → {t}（官方译名已套用）\n");
    }

    /// <summary>P2-2：从最近 OCR 文本自动识别资源关键词并加入资源点列表（去重计数）。</summary>
    private void HarvestFromOcr_Click(object sender, RoutedEventArgs e)
    {
        var ocr = AppState.LastOcrText ?? "";
        if (string.IsNullOrWhiteSpace(ocr))
        {
            KnowledgeLog.AppendText("[资源] 无 OCR 数据，请先启动持续识别。\n");
            return;
        }
        var hits = new List<string>();
        if (ocr.Contains("水晶") || ocr.Contains("矿")) hits.Add("水晶块");
        if (ocr.Contains("宝箱")) hits.Add("宝箱");
        if (ocr.Contains("花") || ocr.Contains("甜甜花")) hits.Add("甜甜花");
        if (ocr.Contains("Boss") || ocr.Contains("boss")) hits.Add("Boss材料");
        if (ocr.Contains("圣遗物") || ocr.Contains("遗物")) hits.Add("圣遗物");

        int added = 0;
        foreach (var name in hits)
        {
            var existing = _res.PlanRoute(ResourceAdvisor.ResourceKind.Crystal, 1000)
                .Concat(_res.PlanRoute(ResourceAdvisor.ResourceKind.Chest, 1000))
                .Concat(_res.PlanRoute(ResourceAdvisor.ResourceKind.Flower, 1000))
                .Concat(_res.PlanRoute(ResourceAdvisor.ResourceKind.BossMaterial, 1000))
                .FirstOrDefault(s => s.Name == name);
            if (existing != null)
            {
                KnowledgeLog.AppendText($"[资源] 「{name}」已存在 {existing.YieldPerMin:0} 点/分，跳过重复添加\n");
                continue;
            }
            var kind = name switch
            {
                "宝箱" => ResourceAdvisor.ResourceKind.Chest,
                "甜甜花" => ResourceAdvisor.ResourceKind.Flower,
                "Boss材料" => ResourceAdvisor.ResourceKind.BossMaterial,
                "圣遗物" => ResourceAdvisor.ResourceKind.BossMaterial,
                _ => ResourceAdvisor.ResourceKind.Crystal
            };
            _res.AddSpot(new ResourceAdvisor.ResourceSpot(kind, name,
                Random.Shared.Next(0, 1000), Random.Shared.Next(0, 1000), 4));
            added++;
        }
        KnowledgeLog.AppendText($"[资源] 从 OCR「{ocr}」识别到 {hits.Count} 类关键词，新增 {added} 个资源点（当前共 {_res.SpotCount} 个）\n");
        if (added > 0)
        {
            var advice = _res.Advise(new[] { new ResourceAdvisor.Inventory("水晶块", 120) }, 1000);
            foreach (var a in advice) KnowledgeLog.AppendText("  " + a + "\n");
        }
    }

    /// <summary>P2-2：导出资源点 CSV 到 User 目录。</summary>
    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "User");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, "resource_advisor.csv");
        System.IO.File.WriteAllText(path, _res.ExportCsv());
        KnowledgeLog.AppendText($"[资源] 已导出 {_res.SpotCount} 个资源点到 {path}\n");
        foreach (var line in _res.ExportCsv().Split('\n').Take(4))
            if (!string.IsNullOrWhiteSpace(line)) KnowledgeLog.AppendText("  " + line + "\n");
    }

    /// <summary>P1-9：多模态游戏状态问答（基于最近 OCR + 知识库）。</summary>
    private void Qa_Click(object sender, RoutedEventArgs e)
    {
        var q = QaBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(q)) return;
        var ocr = AppState.LastOcrText ?? "";
        var age = (DateTime.Now - AppState.LastRecognitionAt).TotalSeconds;
        QaOcrHint.Text = string.IsNullOrEmpty(ocr) ? "无 OCR" : $"OCR ({age:0}s): {ocr}";

        KnowledgeLog.AppendText($"[问] {q}\n");
        var answer = AnswerQuestion(q, ocr);
        KnowledgeLog.AppendText($"[答] {answer}\n\n");
        QaBox.Clear();
    }

    private string AnswerQuestion(string q, string ocr)
    {
        if (q.Contains("哪") || q.Contains("位置") || q.Contains("在哪") || q.Contains("地方"))
        {
            if (!string.IsNullOrWhiteSpace(ocr))
                return $"当前画面 OCR 文本：{ocr}。建议根据其中出现的地名判断位置。";
            return "暂无 OCR 数据。请先到攻略浏览器启动持续识别(2FPS)。";
        }
        if (q.Contains("什么") || q.Contains("这是") || q.Contains("这个"))
        {
            if (!string.IsNullOrWhiteSpace(ocr))
                return $"画面识别到：{ocr}。";
            return "暂无 OCR 数据，无法判断。请先启动识别。";
        }
        if (q.Contains("血") || q.Contains("体力") || q.Contains("耐力") || q.Contains("状态"))
        {
            if (ocr.Contains("血")) return "画面检测到血条相关文字，但未做精确数值识别。建议切到角色界面查看。";
            return "当前未在 OCR 中识别到血条/体力数值。体力管理建议：低于 25% 时传送到七天神像回复。";
        }
        if (q.Contains("怎么") || q.Contains("路线") || q.Contains("过去") || q.Contains("走"))
        {
            var hits = _kb.Search(q, top: 3);
            if (hits.Count > 0)
                return $"知识库中找到 {hits.Count} 条相关路线：" + string.Join("；",
                    hits.Take(3).Select(h => h.Entry.TaskDescription));
            return "知识库暂无相关路线。建议先在攻略浏览器识别一段路线视频。";
        }
        if (q.Contains("弱") || q.Contains("打不过") || q.Contains("boss", StringComparison.OrdinalIgnoreCase))
        {
            var hits = _kb.Search("弱点", top: 3);
            if (hits.Count > 0)
                return string.Join("；", hits.Take(3).Select(h => h.Entry.TaskDescription));
            return "知识库暂无 Boss 弱点数据。建议识别一段对应 Boss 的攻略视频。";
        }
        var kbhits = _kb.Search(q, top: 2);
        if (kbhits.Count > 0)
            return "知识库检索：" + string.Join("；", kbhits.Take(2).Select(h => h.Entry.TaskDescription));
        return $"我听懂了「{q}」，但目前没有足够的画面信息回答。请先启动持续识别。";
    }
}
