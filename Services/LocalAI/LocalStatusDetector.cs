using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BetterGIProWpf.Services.LocalAI;

public static class LocalStatusDetector
{
    public enum GameScene { Combat, Explore, Dialogue, Map, Menu, Loading, Backpack, Unknown }
    public record StatusResult(GameScene Scene, string SceneName, double Confidence, double Brightness, double Saturation, double EdgeDensity, double Variance);

    public static StatusResult Detect(byte[] imageBytes)
    {
        try { using var ms = new MemoryStream(imageBytes); using var bmp = new Bitmap(ms); return Detect(bmp); }
        catch { return new StatusResult(GameScene.Unknown, "未知", 0, 0, 0, 0, 0); }
    }

    public static StatusResult Detect(Bitmap source)
    {
        const int w = 64, h = 36;
        using var thumb = new Bitmap(source, w, h);
        var data = thumb.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var bytes = new byte[data.Stride * h];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        thumb.UnlockBits(data);
        double sumL = 0, sumS = 0, sumSq = 0, edges = 0; var n = 0;
        for (var y = 1; y < h - 1; y++) for (var x = 1; x < w - 1; x++)
        {
            int idx(int dx, int dy) => (y + dy) * data.Stride + (x + dx) * 3;
            var b = bytes[idx(0, 0)]; var g = bytes[idx(0, 0) + 1]; var r = bytes[idx(0, 0) + 2];
            var l = 0.299 * r + 0.587 * g + 0.114 * b;
            var mx = Math.Max(r, Math.Max(g, b)); var mn = Math.Min(r, Math.Min(g, b));
            sumL += l; sumS += (mx == 0 ? 0 : (mx - mn) / (double)mx); sumSq += l * l;
            if (Math.Abs(r - bytes[idx(-1, 0) + 2]) > 24) edges++;
            n++;
        }
        var bright = sumL / n; var sat = sumS / n; var variance = Math.Sqrt(Math.Max(0, sumSq / n - bright * bright)); var edgeDensity = edges / n;
        GameScene scene;
        if (bright < 18) scene = GameScene.Loading;
        else if (sat > 0.42 && variance > 52 && edgeDensity > 0.16) scene = GameScene.Combat;
        else if (sat > 0.38 && variance > 40) scene = GameScene.Explore;
        else if (sat < 0.14 && variance < 22) scene = GameScene.Map;
        else scene = GameScene.Explore;
        var name = scene.ToString();
        return new StatusResult(scene, name, 0.7, bright, sat, edgeDensity, variance);
    }

    public static GameScene RefineWithOcr(StatusResult baseResult, string ocrText)
    {
        if (ocrText.Contains("地图") && ocrText.Contains("传送")) return GameScene.Map;
        if (ocrText.Contains("背包") || ocrText.Contains("圣遗物")) return GameScene.Backpack;
        return baseResult.Scene;
    }
}
