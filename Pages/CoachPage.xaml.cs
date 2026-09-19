using System;
using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Coach;
using BetterGIProWpf.Services.LocalAI;

namespace BetterGIProWpf.Pages;

public partial class CoachPage : Page
{
    private readonly StrategyCoach _coach = new();

    public CoachPage()
    {
        InitializeComponent();
        _coach.SuggestionIssued += s => Dispatcher.Invoke(() =>
            CoachLog.AppendText($"[建议] {s.Title}：{s.Advice}\n"));
        LocalAiEngine.BindCoach(_coach);
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        _coach.Frequency = (FreqCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        {
            "Off" => CoachFrequency.Off,
            "Low" => CoachFrequency.Low,
            "High" => CoachFrequency.High,
            _ => CoachFrequency.Normal
        };
        _coach.TriggerThreshold = ThreshSlider.Value;
        ThreshLabel.Text = ThreshSlider.Value.ToString("0.00");
        _coach.VoiceEnabled = VoiceChk.IsChecked == true;

        var video = new StrategyCoach.VideoContext
        {
            Histogram = MakeHist(new byte[] { 180, 60, 40, 200, 70, 50, 160, 80, 60 }),
            KeyTexts = new[] { "风起地", "传送点" },
            SceneName = "风起地·七天神像",
            Actions = new List<string> { "元素战技赶路" },
            Weaknesses = new Dictionary<string, string> { ["北风狼王"] = "雷元素" }
        };
        var gameHist = MakeHist(new byte[] { 60, 120, 130, 55, 115, 128, 70, 110, 120 });
        var suggestions = _coach.Analyze(video, gameHist, new[] { "北风狼王" }, staminaRatio: 0.15,
            currentCharacter: "雷电将军", bossInFight: "北风狼王", skillReady: new[] { true, false });

        CoachLog.AppendText($"[对比] 帧差距 color={1 - CosineForLog(video.Histogram, gameHist):0.00} → 生成 {suggestions.Count} 条建议\n");
        if (suggestions.Count == 0) CoachLog.AppendText("[对比] 差距低于阈值，无建议（可调低阈值）\n");
    }

    /// <summary>P1-2：基于 ContinuousRecognizer 最新 OCR 文本实时生成建议。</summary>
    private void LiveAnalyze_Click(object sender, RoutedEventArgs e)
    {
        var ocr = AppState.LastOcrText ?? "";
        var age = (DateTime.Now - AppState.LastRecognitionAt).TotalSeconds;
        if (string.IsNullOrWhiteSpace(ocr))
        {
            LiveOcrStatus.Text = "暂无 OCR 数据，请先在攻略浏览器启动持续识别";
            CoachLog.AppendText("[实时] 无 OCR 数据。请先到「攻略浏览器」点「持续识别(2FPS)」。\n");
            return;
        }
        LiveOcrStatus.Text = $"最近 OCR ({age:0}s 前): {ocr}";
        CoachLog.AppendText($"[实时] 当前 OCR: {ocr}\n");

        var suggestions = new List<CoachSuggestion>();
        if (ocr.Contains("元素") || ocr.Contains("火") || ocr.Contains("水") || ocr.Contains("雷") || ocr.Contains("冰"))
            suggestions.Add(new CoachSuggestion(SuggestionKind.ElementReaction, "元素反应",
                "画面出现元素相关文字，建议优先触发对应元素附着以打反应", 0.7));
        if (ocr.Contains("体力") || ocr.Contains("耐力") || ocr.Contains("体力不足"))
            suggestions.Add(new CoachSuggestion(SuggestionKind.Stamina, "体力管理",
                "检测到体力相关提示，建议停下等待体力回复或传送到七天神像", 0.8));
        if (ocr.Contains("传送") || ocr.Contains("锚点") || ocr.Contains("七天神像"))
            suggestions.Add(new CoachSuggestion(SuggestionKind.Route, "路线优化",
                "检测到传送点，建议先传送再跑图，节省时间", 0.75));
        if (ocr.Contains("技能") || ocr.Contains("元素战技") || ocr.Contains("元素爆发"))
            suggestions.Add(new CoachSuggestion(SuggestionKind.SkillTiming, "技能时机",
                "检测到技能提示，建议在敌人硬直或背身时释放", 0.65));
        if (ocr.Contains("Boss") || ocr.Contains("boss") || ocr.Contains("狼王") || ocr.Contains("公子") || ocr.Contains("女士"))
            suggestions.Add(new CoachSuggestion(SuggestionKind.EnemyWeakness, "敌人弱点",
                "检测到 Boss，建议切换到对应克制元素角色", 0.85));

        if (suggestions.Count == 0)
        {
            CoachLog.AppendText("[实时] 未识别到可触发建议的关键词。\n");
            return;
        }
        foreach (var s in suggestions)
        {
            CoachLog.AppendText($"[建议] {s.Title}: {s.Advice} (相关度 {s.Relevance:0.00})\n");
            if (VoiceChk.IsChecked == true && _coach.TtsSpeak != null) _ = _coach.TtsSpeak(s.Advice);
        }
    }

    private static double[] MakeHist(byte[] v)
    {
        var h = new double[16];
        for (var i = 0; i < v.Length && i < 16; i++) h[i] = v[i] / 255d;
        var total = h.Sum();
        for (var i = 0; i < h.Length; i++) h[i] /= total;
        return h;
    }

    private static double CosineForLog(double[] a, double[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
