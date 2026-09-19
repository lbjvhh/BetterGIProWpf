using BetterGIProWpf.Services.Companion;

namespace BetterGIProWpf.Services.LocalAI;

public class LocalVisualQaEngine : GameQaService.IVisualQaBackend
{
    private readonly LocalOcrEngine _ocr;
    public LocalVisualQaEngine(LocalOcrEngine ocr, Func<string, string>? fallback = null) => _ocr = ocr;

    public async Task<string> AskAsync(string question, string imageBase64, string context)
    {
        await Task.CompletedTask;
        var entity = LocalGameKnowledge.FindEntities(question).FirstOrDefault();
        return LocalIntentEngine.Answer(question, "", entity);
    }
}
