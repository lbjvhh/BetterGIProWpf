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
        _res.AddSpots(new[] {
            new ResourceAdvisor.ResourceSpot(ResourceAdvisor.ResourceKind.Crystal, "水晶块", 120, 80, 6),
            new ResourceAdvisor.ResourceSpot(ResourceAdvisor.ResourceKind.Crystal, "水晶块", 320, 240, 5),
            new ResourceAdvisor.ResourceSpot(ResourceAdvisor.ResourceKind.Ore, "白铁矿", 500, 300, 3),
        });
        AddDemoEntries();
    }

    private void AddDemoEntries()
    {
        _kb.Add(new KnowledgeEntry { VideoSource = "https://bilibili.com/video/BV123", TimestampSec = 45, TaskDescription = "传送到风起地七天神像", Location = "风起地", TaskType = "传送", VlmSummary = "从神像出发向北探索" });
        _kb.Add(new KnowledgeEntry { VideoSource = "https://bilibili.com/video/BV123", TimestampSec = 210, TaskDescription = "击败北风狼王，弱点是雷元素", Location = "奔狼领", TaskType = "战斗", BossName = "北风狼王", VlmSummary = "推荐雷电将军主C" });
        _kb.Add(new KnowledgeEntry { VideoSource = "https://youtube.com/watch?v=abc", TimestampSec = 90, TaskDescription = "采集水晶块路线", Location = "层岩巨渊", TaskType = "采集", VlmSummary = "每5分钟约30个水晶块" });
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
}
