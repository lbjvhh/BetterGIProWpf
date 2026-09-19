namespace BetterGIProWpf.Services.Adaptive;

/// <summary>
/// 模块21/22: 运行时自适应控制器 + 异常诊断知识库。
/// </summary>
public class ExceptionAdvisor
{
    public record FixStrategy(string Name, string Action, int Priority);
    public record ExceptionPattern(string Keyword, string Description, List<FixStrategy> Strategies);

    private readonly List<ExceptionPattern> _patterns = new()
    {
        new("不在地图界面", "传送前未打开地图", new()
        {
            new("按M键打开地图", "press m", 1),
            new("等待1秒重试", "wait 1000", 2),
        }),
        new("傻站", "战斗中角色不释放技能", new()
        {
            new("切换备用战斗策略", "switch combat strategy", 1),
            new("手动按E+Q", "press e, q", 2),
        }),
        new("卡住", "角色卡在障碍物", new()
        {
            new("跳跃脱离", "press space", 1),
            new("方向键组合", "press wasd", 2),
            new("冲刺", "press shift w", 3),
        }),
        new("超时", "脚本执行超时", new()
        {
            new("跳过当前步骤", "skip step", 1),
            new("等待2秒", "wait 2000", 2),
        }),
        new("死亡", "角色死亡", new()
        {
            new("回到最近锚点", "teleport to waypoint", 1),
            new("等待复活", "wait 5000", 2),
        }),
        new("网络", "网络波动/掉线", new()
        {
            new("等待重连", "wait 3000", 1),
            new("按ESC", "press esc", 2),
        }),
        new("加载", "加载画面卡住", new()
        {
            new("等待5秒", "wait 5000", 1),
            new("按ESC", "press esc", 2),
        }),
    };

    private readonly List<(string Error, string Strategy, string Result)> _history = new();
    public event Action<string>? Log;

    public List<FixStrategy> Diagnose(string errorText)
    {
        foreach (var p in _patterns)
        {
            if (errorText.Contains(p.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                Log?.Invoke($"[诊断] 匹配异常「{p.Keyword}」→ {p.Description}");
                return p.Strategies;
            }
        }
        Log?.Invoke($"[诊断] 未识别异常: {errorText}");
        return new List<FixStrategy>();
    }

    public void RecordResult(string error, string strategy, string result)
    {
        _history.Add((error, strategy, result));
        Log?.Invoke($"[记录] {error} → {strategy} → {result}");
    }

    public string ExportHistoryJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(_history);
    }
}
