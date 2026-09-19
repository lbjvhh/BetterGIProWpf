using System.Collections.Concurrent;

namespace BetterGIProWpf.Services.Automation;

/// <summary>自动化模块统一接口：启动、停止、状态查询。对应方案 3.3 模块协议。</summary>
public interface IAutomationModule
{
    string Id { get; }
    string Name { get; }
    bool IsRunning { get; }
    string Status { get; }
    Task StartAsync();
    Task StopAsync();
}

/// <summary>模块管理器：注册、启停、心跳检测、崩溃自动重启、集中日志。</summary>
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

    public AutomationModuleManager()
    {
        // 心跳监控：每 2s 检查模块运行状态，异常则自动重启
        _ = Task.Run(MonitorLoopAsync);
    }

    public void Register(IAutomationModule module)
    {
        _modules[module.Id] = module;
        AppendLog($"[模块] 注册 {module.Name} ({module.Id})");
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
        try { await m.StopAsync(); AppendLog($"[模块] {m.Name} 已停止"); }
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
                    // 心跳：预期运行却停止 → 判定崩溃 → 自动重启（带次数上限）
                    if (m.Status == "running" && !m.IsRunning)
                    {
                        _restartCount.TryGetValue(id, out var n);
                        if (n < MaxRestartsPerModule)
                        {
                            _restartCount[id] = n + 1;
                            AppendLog($"[模块] {m.Name} 疑似崩溃，自动重启（第 {n + 1}/{MaxRestartsPerModule} 次）");
                            await m.StartAsync();
                            ModuleRestarted?.Invoke(id, m.Name);
                        }
                        else
                        {
                            AppendLog($"[模块] {m.Name} 重启次数达上限，停止自动恢复");
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
