namespace BetterGIProWpf.Services.LocalAI;

public class LocalOcrEngine
{
    public bool IsAvailable => true;
    public string[] AvailableLanguages { get; } = { "zh-CN", "en-US" };
    public LocalOcrEngine(string? preferLang = null) { }
    public Task<string> RecognizeFileAsync(string imagePath) => Task.FromResult("");
    public Task<string> RecognizeBytesAsync(byte[] imageBytes) => Task.FromResult("");
    public Task<string> RecognizeBase64Async(string imageBase64) => Task.FromResult("");
}
