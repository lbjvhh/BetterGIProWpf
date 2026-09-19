using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services;
using BetterGIProWpf.Services.Humanize;

namespace BetterGIProWpf.Pages;

public partial class AdvancedPage : Page
{
    public AdvancedPage() => InitializeComponent();

    private void AnalyzeBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var parts = IntervalsBox.Text.Split(',', '，', ' ', '\n', '\t');
            var intervals = new List<int>();
            foreach (var p in parts)
                if (int.TryParse(p.Trim(), out var v) && v > 0) intervals.Add(v);
            var r = HumanizeInput.AnalyzeSequence(intervals);
            HumanizeReport.Text =
                $"节拍标准差: {r.BeatStdMs:F1} ms\n" +
                $"<80ms 固定节拍占比: {r.FixedBeatRatio:P0}\n" +
                $"操作序列熵（归一化）: {r.ActionEntropy:F2}\n" +
                $"路径噪声幅度: {r.PathNoisePct:F1}%\n" +
                $"综合拟人度: {r.Overall:F0} / 100\n" +
                $"结论: {r.Conclusion}";
        }
        catch (Exception ex) { HumanizeReport.Text = "分析失败: " + ex.Message; }
    }

    private void GenScriptBtn_Click(object sender, RoutedEventArgs e)
    {
        if (AppState.Steps.Count == 0) { ScriptMsg.Text = "暂无步骤，请先在攻略浏览器识别"; return; }
        var gen = new ScriptGenerator();
        var dir = gen.Generate("auto_guide_" + DateTime.Now.ToString("HHmmss"), AppState.Steps);
        ScriptMsg.Text = "已生成: " + dir;
    }

    private void PurgeBtn_Click(object sender, RoutedEventArgs e)
    {
        var n = new FeatureCache().PurgeExpired();
        OpMsg.Text = $"清理过期缓存 {n} 条";
    }

    private void SchedBtn_Click(object sender, RoutedEventArgs e)
    {
        var sched = AppState.Scheduler;
        sched.AddOrUpdate(new CronJob { Name = "每日凌晨解析", Cron = "0 3 * * *", Action = "parse-video" });
        sched.Start();
        OpMsg.Text = "已注册每日 03:00 定时任务。Cron: 分 时 日 月 周";
    }
}
