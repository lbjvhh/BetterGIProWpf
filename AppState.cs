using BetterGIProWpf.Services;
using BetterGIProWpf.Services.Knowledge;
using BetterGIProWpf.Services.Companion;

namespace BetterGIProWpf;

/// <summary>跨页面共享的运行时状态（当前操作序列与调度器）。</summary>
public static class AppState
{
    public static List<OperationStep> Steps { get; set; } = new();
    public static CronScheduler Scheduler { get; } = new();

    /// <summary>全局攻略知识库单例（攻略浏览器识别结果自动入库 → 知识库页检索）。</summary>
    public static KnowledgeBase Knowledge { get; } = new();

    public static string LastOcrText { get; set; } = "";
    public static string LastVideoOcrText { get; set; } = "";
    public static DateTime LastRecognitionAt { get; set; } = DateTime.MinValue;

    public static Services.Humanize.HumanizeInput Humanize { get; } = new();

    /// <summary>全局 AI 客户端（OpenAI 兼容）。Settings 页保存后会重建。</summary>
    public static AiService Ai { get; private set; } = new(AppConfig.Ai);

    /// <summary>模块9：多模态游戏状态问答单例。UseExternal=true 时自动挂载 OpenAI 后端。</summary>
    public static GameQaService Qa { get; } = new();

    public static void ReloadAi()
    {
        Ai = new AiService(AppConfig.Ai);
        if (AppConfig.Ai.UseExternal && !string.IsNullOrWhiteSpace(AppConfig.Ai.ApiKey))
            Qa.Backend = new OpenAiVisionBackend(Ai);
        else
            Qa.Backend = null;
    }

    static AppState() { ReloadAi(); }
}
