using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;
using Windows.Storage.Streams;
using BetterGIProWpf.Services.Companion;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>
/// 本地语音识别引擎（模块 1/9）：基于 Windows.Media.SpeechRecognition 系统引擎，
/// 使用本地词表约束离线识别（无需网络）。
/// </summary>
public class LocalAsrEngine : IAsrProvider
{
    private readonly SpeechRecognizer? _recognizer;
    private bool _compiled;

    public bool IsAvailable => _recognizer != null;
    public string? Language { get; }

    public LocalAsrEngine(string? langTag = "zh-CN", IEnumerable<string>? vocabulary = null)
    {
        try
        {
            var language = new Windows.Globalization.Language(langTag ?? "zh-CN");
            _recognizer = new SpeechRecognizer(language);
            Language = langTag;
            var words = (vocabulary ?? DefaultVocabulary()).Distinct().Take(2000).ToList();
            _recognizer.Constraints.Add(new SpeechRecognitionListConstraint(words));
            _compiled = false;
        }
        catch { _recognizer = null; Language = null; }
    }

    public static IEnumerable<string> DefaultVocabulary()
    {
        var list = new List<string>
        {
            "跟随","跟着我","跟紧","打","击杀","消灭","清怪","战斗","打怪","打败","收拾","干掉",
            "采集","捡","收集","拾取","拿","传送","前往","去","到","锚点","神像","探索","找","搜","看看",
            "帮我","请","现在","马上","那个","这个","附近","里面","出来","前面","后面","左边","右边",
        };
        foreach (var e in LocalGameKnowledge.Entities) { list.Add(e.Name); if (list.Count >= 1800) break; }
        return list;
    }

    public async Task<string> RecognizeAsync(byte[] pcm16k, int sampleRate)
    {
        if (_recognizer == null) return "";
        try
        {
            if (!_compiled) { await _recognizer.CompileConstraintsAsync(); _compiled = true; }
            var result = await _recognizer.RecognizeAsync();
            return result?.Text?.Trim() ?? "";
        }
        catch { return ""; }
    }

    public static byte[] BuildWav(byte[] pcm, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int dataLen = pcm.Length; int byteRate = sampleRate * 2;
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + dataLen);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); bw.Write(16);
        bw.Write((short)1); bw.Write((short)1); bw.Write(sampleRate); bw.Write(byteRate);
        bw.Write((short)2); bw.Write((short)16);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data")); bw.Write(dataLen); bw.Write(pcm);
        bw.Flush(); return ms.ToArray();
    }
}
