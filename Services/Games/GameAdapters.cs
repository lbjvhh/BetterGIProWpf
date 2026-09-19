using System.Drawing;
using BetterGIProWpf.Services.Integration;

namespace BetterGIProWpf.Services.Games;

public interface IGameAdapter
{
    string GameId { get; }
    string GameName { get; }
    string[] WindowAliases { get; }
    IntPtr FindWindow();
    Bitmap? CaptureFrame();
    byte[]? CaptureRgb(int w, int h);
    void Click(int x, int y);
    void Key(string key);
    string DetectVersion();
}

public class GenshinAdapter : IGameAdapter
{
    public string GameId => "genshin";
    public string GameName => "原神";
    public string[] WindowAliases => new[] { "原神", "Genshin Impact", "YuanShen" };
    private IntPtr _hwnd;
    public IntPtr FindWindow() => _hwnd = GameLauncher.FindWindowByTitle(WindowAliases);
    public Bitmap? CaptureFrame() => WindowCaptureService.CaptureWindow(FindWindow());
    public byte[]? CaptureRgb(int w, int h) => WindowCaptureService.CaptureRgb(FindWindow(), w, h);
    public void Click(int x, int y) => InputSimulator.Click(x, y);
    public void Key(string key) => InputSimulator.KeyPress(key);
    public string DetectVersion() => _hwnd == IntPtr.Zero ? "" : "unknown-genshin";
}

public class WutheringAdapter : IGameAdapter
{
    public string GameId => "wuthering";
    public string GameName => "鸣潮";
    public string[] WindowAliases => new[] { "鸣潮", "Wuthering Waves" };
    private IntPtr _hwnd;
    public IntPtr FindWindow() => _hwnd = GameLauncher.FindWindowByTitle(WindowAliases);
    public Bitmap? CaptureFrame() => WindowCaptureService.CaptureWindow(FindWindow());
    public byte[]? CaptureRgb(int w, int h) => WindowCaptureService.CaptureRgb(FindWindow(), w, h);
    public void Click(int x, int y) => InputSimulator.Click(x, y);
    public void Key(string key) => InputSimulator.KeyPress(key);
    public string DetectVersion() => "";
}

public class ZenlessAdapter : IGameAdapter
{
    public string GameId => "zzz";
    public string GameName => "绝区零";
    public string[] WindowAliases => new[] { "绝区零", "Zenless Zone Zero" };
    private IntPtr _hwnd;
    public IntPtr FindWindow() => _hwnd = GameLauncher.FindWindowByTitle(WindowAliases);
    public Bitmap? CaptureFrame() => WindowCaptureService.CaptureWindow(FindWindow());
    public byte[]? CaptureRgb(int w, int h) => WindowCaptureService.CaptureRgb(FindWindow(), w, h);
    public void Click(int x, int y) => InputSimulator.Click(x, y);
    public void Key(string key) => InputSimulator.KeyPress(key);
    public string DetectVersion() => "";
}

public static class GameAdapterFactory
{
    public static IGameAdapter Create(string gameId) => gameId.ToLowerInvariant() switch
    {
        "genshin" => new GenshinAdapter(),
        "wuthering" => new WutheringAdapter(),
        "zzz" or "zenless" => new ZenlessAdapter(),
        _ => throw new NotSupportedException($"未知游戏: {gameId}"),
    };
}
