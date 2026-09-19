using BetterGIProWpf.Services;
using BetterGIProWpf.Services.Knowledge;
using BetterGIProWpf.Services.Companion;

namespace BetterGIProWpf;

public static class AppState
{
    public static List<OperationStep> Steps { get; set; } = new();
    public static CronScheduler Scheduler { get; } = new();
    public static KnowledgeBase Knowledge { get; } = new();
    public static string LastOcrText { get; set; } = "";
    public static string LastVideoOcrText { get; set; } = "";
    public static DateTime LastRecognitionAt { get; set; } = DateTime.MinValue;
    public static string LastStepsJson { get; set; } = "[]";
    public static System.Net.Http.HttpClient Http { get; } = new() { Timeout = TimeSpan.FromSeconds(30) };
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
