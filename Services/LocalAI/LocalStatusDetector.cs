using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace BetterGIProWpf.Services.LocalAI;

/// <summary>本地游戏状态检测：像素特征 + OCR 关键词判断场景。</summary>
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
        var rect = new Rectangle(0, 0, w, h);
        var data = thumb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var stride = data.Stride;
        var bytes = new byte[stride * h];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        thumb.UnlockBits(data);
        double sumL = 0, sumS = 0, sumSq = 0, edges = 0; var n = 0;
        for (var y = 1; y < h - 1; y++) for (var x = 1; x < w - 1; x++)
        {
            int idx(int dx, int dy) => (y + dy) * stride + (x + dx) * 3;
            var b = bytes[idx(0,0)]; var g = bytes[idx(0,0)+1]; var r = bytes[idx(0,0)+2];
            var l = 0.299*r+0.587*g+0.114*b;
            var mx = Math.Max(r, Math.Max(g, b)); var mn = Math.Min(r, Math.Min(g, b));
            var s = mx == 0 ? 0 : (mx - mn) / (double)mx;
            sumL += l; sumS += s; sumSq += l*l;
            var lx = Math.Abs(0.299*(bytes[idx(-1,0)+2]-bytes[idx(1,0)+2]) + 0.587*(bytes[idx(-1,0)+1]-bytes[idx(1,0)+1]) + 0.114*(bytes[idx(-1,0)]-bytes[idx(1,0)]));
            var ly = Math.Abs(0.299*(bytes[idx(0,-1)+2]-bytes[idx(0,1)+2]) + 0.587*(bytes[idx(0,-1)+1]-bytes[idx(0,1)+1]) + 0.114*(bytes[idx(0,-1)]-bytes[idx(0,1)]));
            if (lx > 24 || ly > 24) edges++;
            n++;
        }
        var bright = sumL / n; var sat = sumS / n;
        var variance = Math.Sqrt(Math.Max(0, sumSq/n - bright*bright));
        var edgeDensity = edges / n;
        GameScene scene;
        if (bright < 18) scene = GameScene.Loading;
        else if (sat > 0.42 && variance > 52 && edgeDensity > 0.16) scene = GameScene.Combat;
        else if (sat > 0.38 && variance > 40 && edgeDensity > 0.12) scene = GameScene.Explore;
        else if (sat < 0.14 && variance < 22 && edgeDensity < 0.05) scene = GameScene.Map;
        else if (edgeDensity > 0.2 && sat > 0.2) scene = GameScene.Menu;
        else if (edgeDensity > 0.14 && variance > 30) scene = GameScene.Dialogue;
        else scene = GameScene.Explore;
        var name = scene switch { GameScene.Combat=>"战斗", GameScene.Explore=>"探索", GameScene.Dialogue=>"对话", GameScene.Map=>"地图", GameScene.Menu=>"菜单", GameScene.Loading=>"加载", GameScene.Backpack=>"背包", _=>"未知" };
        return new StatusResult(scene, name, Math.Min(0.98, 0.45+variance/200+edgeDensity), bright, sat, edgeDensity, variance);
    }

    public static GameScene RefineWithOcr(StatusResult baseResult, string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText)) return baseResult.Scene;
        if (ocrText.Contains("对话") || ocrText.Contains("确定") && ocrText.Contains("取消")) return GameScene.Dialogue;
        if (ocrText.Contains("地图") && ocrText.Contains("传送")) return GameScene.Map;
        if (ocrText.Contains("设置") || ocrText.Contains("返回") && ocrText.Contains("退出")) return GameScene.Menu;
        if (ocrText.Contains("背包") || ocrText.Contains("圣遗物")) return GameScene.Backpack;
        return baseResult.Scene;
    }
}
