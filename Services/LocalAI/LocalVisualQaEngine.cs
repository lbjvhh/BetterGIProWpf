using System;
using System.Linq;
using System.Threading.Tasks;
using BetterGIProWpf.Services.Companion;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>本地视觉问答引擎：OCR + 状态检测 + 知识库 + 模板回答，完全离线。</summary>
public class LocalVisualQaEngine : GameQaService.IVisualQaBackend
{
    private readonly LocalOcrEngine _ocr;
    public LocalVisualQaEngine(LocalOcrEngine ocr) => _ocr = ocr;

    public async Task<string> AskAsync(string question, string imageBase64, string context)
    {
        try
        {
            var imageBytes = Convert.FromBase64String(imageBase64 ?? "");
            var status = LocalStatusDetector.Detect(imageBytes);
            var ocrText = await _ocr.RecognizeBase64Async(imageBase64 ?? "");
            var entity = LocalGameKnowledge.FindEntities(question).FirstOrDefault();
            var answer = LocalIntentEngine.Answer(question, ocrText, entity);
            if (status.Scene != LocalStatusDetector.GameScene.Unknown && status.Confidence > 0.5)
                answer = $"[当前场景：{status.SceneName}] {answer}";
            return answer;
        }
        catch { return "本地识别失败（无法解码截图），请重试。"; }
    }
}
