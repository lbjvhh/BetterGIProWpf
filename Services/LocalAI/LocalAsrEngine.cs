using System.IO;
using BetterGIProWpf.Services.Companion;

namespace BetterGIProWpf.Services.LocalAI;

public class LocalAsrEngine : IAsrProvider
{
    public bool IsAvailable { get; private set; }
    public string? Language { get; }

    public LocalAsrEngine(string? langTag = "zh-CN", IEnumerable<string>? vocabulary = null)
    {
        Language = langTag;
        IsAvailable = false; // 无系统语音包时降级
    }

    public static IEnumerable<string> DefaultVocabulary() => new[] {
        "跟随","跟着我","打","击杀","清怪","战斗","打怪","采集","收集","拾取","传送","前往","去","锚点","神像","探索","找","搜"
    };

    public Task<string> RecognizeAsync(byte[] pcm16k, int sampleRate) => Task.FromResult("");

    public static byte[] BuildWav(byte[] pcm, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int dataLen = pcm.Length; int byteRate = sampleRate * 2;
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataLen);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(sampleRate);
        bw.Write(byteRate); bw.Write((short)2); bw.Write((short)16);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(dataLen); bw.Write(pcm); bw.Flush();
        return ms.ToArray();
    }
}
