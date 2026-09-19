using System;
using System.Linq;
using System.Text;
using BetterGIProWpf.Services.Coach;
using BetterGIProWpf.Services.Companion;
using BetterGIProWpf.Services.Knowledge;
using BetterGIProWpf.Services.Media;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>
/// 本地 AI 引擎聚合管理器：初始化全部本地引擎（OCR/TTS/ASR/翻译/状态检测/视觉问答/
/// 教练/视频分析），并绑定到各业务服务。全离线，零外部模型，零 API Key。
/// </summary>
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
        Tts = new LocalTtsEngine();
        Tts.SelectVoice("zh-CN");
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
        var sb = new StringBuilder();
        sb.AppendLine($"OCR 文字识别：{(Ocr.IsAvailable ? "就绪 ✓（语言：" + string.Join("、", Ocr.AvailableLanguages.Take(4)) + "）" : "不可用（系统未安装 OCR 语言包）")}");
        sb.AppendLine($"TTS 语音合成：{(Tts.IsAvailable ? "就绪 ✓（语音：" + string.Join("、", Tts.Voices.Take(3)) + "）" : "不可用")}");
        sb.AppendLine($"ASR 语音识别：{(Asr.IsAvailable ? "就绪 ✓（本地词表 " + LocalAsrEngine.DefaultVocabulary().Count() + " 词）" : "不可用（系统无语音识别语言包，可用文字指令）")}");
        sb.AppendLine($"本地翻译：就绪 ✓（中/英/日/韩/俄 词典）");
        sb.AppendLine($"本地知识库：就绪 ✓（实体 " + LocalGameKnowledge.Entities.Count + " 条）");
        sb.AppendLine($"状态检测：就绪 ✓（战斗/探索/对话/地图/菜单/加载/背包）");
        sb.AppendLine($"视觉问答：就绪 ✓（OCR + 状态 + 知识库模板）");
        sb.AppendLine($"视频攻略识别：就绪 ✓（帧差异 + OCR + 规则）");
        sb.AppendLine($"外部模型：未启用（当前全部为本地推理）");
        return sb.ToString();
    }
}
