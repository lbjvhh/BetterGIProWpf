using BetterGIProWpf.Services;
using BetterGIProWpf.Services.Knowledge;

namespace BetterGIProWpf;

/// <summary>跨页面共享的运行时状态（当前操作序列与调度器）。</summary>
public static class AppState
{
    public static List<OperationStep> Steps { get; set; } = new();
    public static CronScheduler Scheduler { get; } = new();

    /// <summary>全局攻略知识库单例（攻略浏览器识别结果自动入库 → 知识库页检索）。</summary>
    public static KnowledgeBase Knowledge { get; } = new();

    /// <summary>P1-2/9：最近一次 OCR 识别到的游戏画面文本（ContinuousRecognizer 写入）。</summary>
    public static string LastOcrText { get; set; } = "";

    /// <summary>P1-2：最近一次视频参考帧的 OCR 文本（用于视频 vs 游戏对比）。</summary>
    public static string LastVideoOcrText { get; set; } = "";

    /// <summary>P1-11：最近一次识别时间戳。</summary>
    public static DateTime LastRecognitionAt { get; set; } = DateTime.MinValue;
}
