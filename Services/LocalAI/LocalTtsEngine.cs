using BetterGIProWpf.Services.Companion;
using BetterGIProWpf.Services.Media;

namespace BetterGIProWpf.Services.LocalAI;

public class LocalTtsEngine : ITtsProvider, NarrationGenerator.ITtsBackend
{
    public bool IsAvailable { get; private set; } = true;
    public string[] Voices { get; } = { "zh-CN" };

    public void SelectVoice(string? voiceName = null) { }

    public Task<byte[]> SynthesizeAsync(string text, string voice, string style) => Task.FromResult(Array.Empty<byte>());

    public async Task SpeakAsync(string text, string voice = "default") { await Task.CompletedTask; }

    public async Task<string> SaveToFileAsync(string text, string path, string? voice = null)
    {
        await File.WriteAllTextAsync(path, text);
        return path;
    }

    public byte[] Stylize(byte[] wav, double speed = 1.0, double pitchShift = 0.0) => wav;
}
