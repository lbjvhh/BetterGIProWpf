using System.Collections.Concurrent;

namespace BetterGIProWpf.Services.Automation;

public interface IAutomationModule
{
    string Id { get; }
    string Name { get; }
    bool IsRunning { get; }
    string Status { get; }
    Task StartAsync();
    Task StopAsync();
}

public class AutomationModuleManager : IDisposable
{
    private readonly Dictionary<string, IAutomationModule> _modules = new();
    private readonly ConcurrentQueue<string> _log = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, int> _restartCount = new();
    public const int MaxRestartsPerModule = 5;

    public event Action<string>? LogAppended;
    public event Action<string, string>? ModuleRestarted;

    public IReadOnlyCollection<IAutomationModule> Modules => _modules.Values.ToList();

    public AutomationModuleManager() { _ = Task.Run(MonitorLoopAsync); }

    public void Register(IAutomationModule module)
    {
        _modules[module.Id] = module;
        AppendLog($"[模块] 注册 {module.Name}");
    }

    public async Task StartAsync(string id)
    {
        if (!_modules.TryGetValue(id, out var m)) throw new KeyNotFoundException(id);
        AppendLog($"[模块] 启动 {m.Name}");
        try { await m.StartAsync(); AppendLog($"[模块] {m.Name} 已启动"); }
        catch (Exception ex) { AppendLog($"[模块] {m.Name} 启动失败: {ex.Message}"); throw; }
    }

    public async Task StopAsync(string id)
    {
        if (!_modules.TryGetValue(id, out var m)) throw new KeyNotFoundException(id);
        AppendLog($"[模块] 停止 {m.Name}");
        try { await m.StopAsync(); }
        catch (Exception ex) { AppendLog($"[模块] {m.Name} 停止异常: {ex.Message}"); }
    }

    private async Task MonitorLoopAsync()
    {
        var token = _cts.Token;
        while (!token.IsCancellationRequested)
        {
            foreach (var (id, m) in _modules.ToList())
            {
                try
                {
                    if (m.Status == "running" && !m.IsRunning)
                    {
                        _restartCount.TryGetValue(id, out var n);
                        if (n < MaxRestartsPerModule)
                        {
                            _restartCount[id] = n + 1;
                            AppendLog($"[模块] {m.Name} 崩溃，自动重启 {n + 1}/{MaxRestartsPerModule}");
                            await m.StartAsync();
                            ModuleRestarted?.Invoke(id, m.Name);
                        }
                    }
                }
                catch (Exception ex) { AppendLog($"[模块] {m.Name} 监控异常: {ex.Message}"); }
            }
            await Task.Delay(2000, token);
        }
    }

    private void AppendLog(string line)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        _log.Enqueue($"{ts}  {line}");
        while (_log.Count > 500) _log.TryDequeue(out _);
        LogAppended?.Invoke($"{ts}  {line}");
    }

    public IReadOnlyList<string> GetLogs(int take = 100) => _log.Reverse().Take(take).ToList();

    public void Dispose() => _cts.Cancel();
}
