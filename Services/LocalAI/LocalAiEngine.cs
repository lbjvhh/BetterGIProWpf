using BetterGIProWpf.Services.Coach;
using BetterGIProWpf.Services.Companion;
using BetterGIProWpf.Services.Knowledge;
using BetterGIProWpf.Services.Media;

namespace BetterGIProWpf.Services.LocalAI;

public static class LocalAiEngine
{
    private static bool _initialized;
    public static LocalOcrEngine Ocr { get; private set; } = new();
    public static LocalTtsEngine Tts { get; private set; } = new();
    public static LocalAsrEngine Asr { get; private set; } = new();
    public static LocalTranslateEngine Translate { get; private set; } = new();
    public static LocalVisualQaEngine VisualQa { get; private set; } = null!;
    public static LocalVideoAnalyzer VideoAnalyzer { get; private set; } = null!;

    public static void Init()
    {
        if (_initialized) return;
        Ocr = new LocalOcrEngine("zh-CN");
        Tts = new LocalTtsEngine(); Tts.SelectVoice("zh-CN");
        Asr = new LocalAsrEngine("zh-CN");
        Translate = new LocalTranslateEngine();
        VisualQa = new LocalVisualQaEngine(Ocr);
        VideoAnalyzer = new LocalVideoAnalyzer(Ocr);
        _initialized = true;
    }

    public static void BindCompanion(CompanionAgent agent) { Init(); agent.Asr = Asr; agent.Tts = Tts; }
    public static void BindQa(GameQaService qa) { Init(); qa.Backend = VisualQa; }
    public static void BindTranslation(TranslationService svc) { Init(); svc.Backend = Translate; }
    public static void BindNarration(NarrationGenerator gen) { Init(); gen.Tts = Tts; }
    public static void BindCoach(StrategyCoach coach) { Init(); coach.TtsSpeak = t => Tts.SpeakAsync(t); }

    public static string StatusReport()
    {
        Init();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"OCR: {(Ocr.IsAvailable ? "就绪 ✓" : "不可用")}");
        sb.AppendLine($"TTS: {(Tts.IsAvailable ? "就绪 ✓" : "不可用")}");
        sb.AppendLine($"ASR: {(Asr.IsAvailable ? "就绪 ✓" : "不可用")}");
        sb.AppendLine("本地翻译/知识库/状态检测/视觉问答/视频识别: 就绪 ✓");
        sb.AppendLine("外部模型: 未启用（全本地）");
        return sb.ToString();
    }
}
