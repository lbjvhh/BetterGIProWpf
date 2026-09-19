using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using BetterGIProWpf.Services.Companion;
using BetterGIProWpf.Services.Media;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>本地 TTS 引擎：Windows.Media.SpeechSynthesis，完全离线。</summary>
public class LocalTtsEngine : ITtsProvider, NarrationGenerator.ITtsBackend
{
    private readonly SpeechSynthesizer _synth = new();
    public bool IsAvailable { get; private set; } = true;
    public string[] Voices { get; }

    public LocalTtsEngine()
    {
        try
        {
            Voices = SpeechSynthesizer.AllVoices?.Select(v => v.DisplayName ?? v.Language ?? "").Where(x => x.Length > 0).Distinct().ToArray() ?? Array.Empty<string>();
            IsAvailable = Voices.Length > 0;
        }
        catch { IsAvailable = false; Voices = Array.Empty<string>(); }
    }

    public void SelectVoice(string? voiceName = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(voiceName))
            {
                var v = SpeechSynthesizer.AllVoices?.FirstOrDefault(x => (x.DisplayName?.Contains(voiceName, StringComparison.OrdinalIgnoreCase) ?? false) || (x.Language?.Contains(voiceName, StringComparison.OrdinalIgnoreCase) ?? false));
                if (v != null) { _synth.Voice = v; return; }
            }
            var zh = SpeechSynthesizer.AllVoices?.FirstOrDefault(v => (v.Language ?? "").StartsWith("zh", StringComparison.OrdinalIgnoreCase));
            if (zh != null) _synth.Voice = zh;
        }
        catch { }
    }

    public async Task<byte[]> SynthesizeAsync(string text, string voice, string style)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<byte>();
        try
        {
            SelectVoice(voice);
            using var stream = await _synth.SynthesizeTextToStreamAsync(text);
            using var ms = new MemoryStream();
            stream.AsStreamForRead().CopyTo(ms);
            return ms.ToArray();
        }
        catch { return Array.Empty<byte>(); }
    }

    public async Task SpeakAsync(string text, string voice = "default")
    {
        var wav = await SynthesizeAsync(text, voice, "speak");
        if (wav.Length == 0) return;
        try { using var ms = new MemoryStream(wav); using var player = new System.Media.SoundPlayer(ms); player.PlaySync(); }
        catch { }
    }

    public async Task<string> SaveToFileAsync(string text, string path, string? voice = null)
    {
        var wav = await SynthesizeAsync(text, voice ?? "zh-CN", "narrate");
        if (wav.Length == 0) return "";
        await File.WriteAllBytesAsync(path, wav);
        return path;
    }

    public byte[] Stylize(byte[] wav, double speed = 1.0, double pitchShift = 0.0) => wav;
}
