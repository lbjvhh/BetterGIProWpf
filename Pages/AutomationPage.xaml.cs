using System.Windows;
using System.Windows.Controls;
using BetterGIProWpf.Services.Automation;
using BetterGIProWpf.Services.Integration;
using TaskScheduler = BetterGIProWpf.Services.Automation.TaskScheduler;

namespace BetterGIProWpf.Pages;

public class DemoModule : IAutomationModule
{
    private readonly string _id; private readonly string _name;
    private volatile bool _running;
    public DemoModule(string id, string name) { _id = id; _name = name; }
    public string Id => _id;
    public string Name => _name;
    public bool IsRunning => _running;
    public string Status => _running ? "running" : "stopped";
    public Task StartAsync() { _running = true; return Task.CompletedTask; }
    public Task StopAsync() { _running = false; return Task.CompletedTask; }
}

public partial class AutomationPage : Page
{
    private readonly TaskScheduler _scheduler = new();
    private readonly AutomationModuleManager _modules = new();
    private readonly NamedPipeServer _pipe = new();

    public AutomationPage()
    {
        InitializeComponent();
        _modules.Register(new DemoModule("video-parse", "视频解析模块"));
        _modules.Register(new DemoModule("task-run", "任务执行模块"));
        _modules.Register(new DemoModule("browser", "攻略浏览器模块"));
        _modules.LogAppended += line => Dispatcher.Invoke(() =>
        {
            ModuleLog.AppendText(line + "\r\n");
            ModuleLog.ScrollToEnd();
        });
        _scheduler.TaskTriggered += OnTaskTriggered;
        _pipe.OnCommand += async cmd => await HandleCommandAsync(cmd);
        _pipe.Start();
        RefreshTasks();
    }

    private async Task<string> HandleCommandAsync(string cmd)
    {
        return cmd switch
        {
            "status" => $"{{\"ok\":true,\"modules\":{_modules.Modules.Count},\"tasks\":{_scheduler.Snapshot().Count}}}",
            "modules:start" => await StartAllModulesAsync() + "",
            "modules:stop" => await StopAllModulesAsync() + "",
            _ => $"{{\"ok\":false,\"error\":\"unknown cmd: {cmd}\"}}",
        };
    }

    private void OnTaskTriggered(ScheduledTask task)
    {
        Dispatcher.Invoke(() => SchedMsg.Text = $"[{DateTime.Now:HH:mm:ss}] 触发 {task.Name} → {task.Action}");
        switch (task.Action)
        {
            case "system-sleep": _ = Task.Run(() => WindowsPower.Sleep()); break;
            case "system-wake":
                var err = WindowsPower.SetWakeTimer(DateTime.Now.AddMinutes(2));
                if (err != "") Dispatcher.Invoke(() => SchedMsg.Text = "唤醒设置失败: " + err);
                break;
        }
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var t = new ScheduledTask
            {
                Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "未命名" : NameBox.Text.Trim(),
                Cron = CronBox.Text.Trim(),
                Action = (ActionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "run-tasks",
            };
            _scheduler.Upsert(t);
            RefreshTasks();
            SchedMsg.Text = $"已添加（下次执行: {t.NextRun:yyyy-MM-dd HH:mm}）";
        }
        catch (Exception ex) { SchedMsg.Text = "添加失败: " + ex.Message; }
    }

    private void RefreshTasks_Click(object sender, RoutedEventArgs e) => RefreshTasks();

    private void RefreshTasks()
    {
        TaskList.Items.Clear();
        foreach (var t in _scheduler.Snapshot())
            TaskList.Items.Add($"[{(t.Enabled ? "启用" : "停用")}] {t.Name}  cron={t.Cron}  动作={t.Action}  下次={t.NextRun:MM-dd HH:mm}");
    }

    private void SleepBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("即将使电脑进入睡眠，确认？", "电源管理", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        WindowsPower.Sleep();
    }

    private void WakeBtn_Click(object sender, RoutedEventArgs e)
    {
        var wake = DateTime.Now.AddMinutes(5);
        var err = WindowsPower.SetWakeTimer(wake);
        SchedMsg.Text = err == "" ? $"已设置 {wake:HH:mm} 唤醒（RTC 计时器）" : "设置失败: " + err;
    }

    private void ClearWake_Click(object sender, RoutedEventArgs e)
    {
        var err = WindowsPower.ClearWakeTimer();
        SchedMsg.Text = err == "" ? "已清除唤醒计时器" : "清除失败: " + err;
    }

    private async Task<int> StartAllModulesAsync()
    {
        foreach (var m in _modules.Modules) { try { await _modules.StartAsync(m.Id); } catch { } }
        UpdateModuleStatus();
        return _modules.Modules.Count;
    }

    private async Task<int> StopAllModulesAsync()
    {
        foreach (var m in _modules.Modules) { try { await _modules.StopAsync(m.Id); } catch { } }
        UpdateModuleStatus();
        return _modules.Modules.Count;
    }

    private async void ModulesStart_Click(object sender, RoutedEventArgs e) => await StartAllModulesAsync();
    private async void ModulesStop_Click(object sender, RoutedEventArgs e) => await StopAllModulesAsync();

    private void UpdateModuleStatus() =>
        ModuleStatus.Text = $"运行中: {_modules.Modules.Count(m => m.IsRunning)}/{_modules.Modules.Count}";
}
