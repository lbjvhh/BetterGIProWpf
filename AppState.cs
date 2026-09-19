using BetterGIProWpf.Services;
using BetterGIProWpf.Services.Knowledge;
using BetterGIProWpf.Services.Companion;

namespace BetterGIProWpf;

/// <summary>跨页面共享的运行时状态。</summary>
public static class AppState
{
    public static List<OperationStep> Steps { get; set; } = new();
    public static CronScheduler Scheduler { get; } = new();
    public static KnowledgeBase Knowledge { get; } = new();
    public static string LastOcrText { get; set; } = "";
    public static string LastVideoOcrText { get; set; } = "";
    public static DateTime LastRecognitionAt { get; set; } = DateTime.MinValue;

    /// <summary>P3: 最近一次持续识别的步骤 JSON。</summary>
    public static string LastStepsJson { get; set; } = "[]";

    public static Services.Humanize.HumanizeInput Humanize { get; } = new();
    public static AiService Ai { get; private set; } = new(AppConfig.Ai);
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
