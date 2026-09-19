using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>本地 OCR 引擎：Windows.Media.Ocr 系统引擎，完全离线。</summary>
public class LocalOcrEngine
{
    private readonly OcrEngine? _engine;
    public bool IsAvailable => _engine != null;
    public string[] AvailableLanguages { get; }

    public LocalOcrEngine(string? preferLang = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(preferLang))
            {
                try { _engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language(preferLang)); }
                catch { _engine = null; }
            }
            _engine ??= OcrEngine.TryCreateFromUserProfileLanguages();
            AvailableLanguages = OcrEngine.AvailableRecognizerLanguages?.Select(l => l.LanguageTag ?? "").Where(x => x.Length > 0).ToArray() ?? Array.Empty<string>();
        }
        catch { _engine = null; AvailableLanguages = Array.Empty<string>(); }
    }

    public async Task<string> RecognizeFileAsync(string imagePath)
    {
        if (_engine == null || !File.Exists(imagePath)) return "";
        try { using var fs = File.OpenRead(imagePath); using var ras = fs.AsRandomAccessStream(); return await DecodeAndRecognize(ras); }
        catch { return ""; }
    }

    public async Task<string> RecognizeBytesAsync(byte[] imageBytes)
    {
        if (_engine == null || imageBytes == null || imageBytes.Length == 0) return "";
        try { using var ms = new MemoryStream(imageBytes); using var ras = ms.AsRandomAccessStream(); return await DecodeAndRecognize(ras); }
        catch { return ""; }
    }

    public Task<string> RecognizeBase64Async(string imageBase64)
    {
        if (string.IsNullOrWhiteSpace(imageBase64)) return Task.FromResult("");
        try { return RecognizeBytesAsync(Convert.FromBase64String(imageBase64)); }
        catch { return Task.FromResult(""); }
    }

    private async Task<string> DecodeAndRecognize(IRandomAccessStream stream)
    {
        if (_engine == null) return "";
        try
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            var result = await _engine.RecognizeAsync(bitmap);
            return string.Join("\n", result.Lines.Select(l => l.Text));
        }
        catch { return ""; }
    }
}
