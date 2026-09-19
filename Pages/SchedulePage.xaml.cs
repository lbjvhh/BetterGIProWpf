using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services;

namespace BetterGIProWpf.Pages;

public partial class SchedulePage : Page
{
    public SchedulePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppState.Scheduler.Tick += OnTick;
            AppState.Scheduler.Start();
            Render();
        };
        Unloaded += (_, _) => AppState.Scheduler.Tick -= OnTick;
    }

    private void Render()
    {
        JobList.Items.Clear();
        foreach (var j in AppState.Scheduler.Jobs)
            JobList.Items.Add($"{(j.Enabled ? "●" : "○")} {j.Name}  |  {j.Cron}  |  {j.Action}  |  上次: {(j.LastRun?.ToString("HH:mm:ss") ?? "-")}");
    }

    private void OnTick(CronJob job)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = $"⏰ 到点执行: {job.Name} ({job.Action}) @ {DateTime.Now:HH:mm:ss}";
            Render();
        });
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var action = (JobAction.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "capture";
        AppState.Scheduler.AddOrUpdate(new CronJob { Name = JobName.Text.Trim(), Cron = JobCron.Text.Trim(), Action = action });
        Render();
        StatusText.Text = "已添加";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var jobs = AppState.Scheduler.Jobs.ToList();
        if (jobs.Count > 0) AppState.Scheduler.Remove(jobs[^1].Id);
        Render();
    }
}
