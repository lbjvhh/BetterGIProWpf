using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services;

namespace BetterGIProWpf.Pages;

public partial class AdvancedPage : Page
{
    public AdvancedPage() => InitializeComponent();

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
        sched.AddOrUpdate(new CronJob
        {
            Name = "每日凌晨解析",
            Cron = "0 3 * * *",
            Action = "parse-video",
        });
        sched.Start();
        OpMsg.Text = "已注册每日 03:00 定时任务（动作: parse-video）。Cron 语法: 分 时 日 月 周";
    }
}
