using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BetterGIProWpf.Services;

public class WindowCapture
{
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public static IntPtr FindWindow(params string[] titles) => GameLauncher.FindWindowByTitle(titles);

    public static Bitmap? Capture(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return null;
        if (!GetWindowRect(hWnd, out var rect)) return null;
        int w = rect.Right - rect.Left, h = rect.Bottom - rect.Top;
        if (w <= 0 || h <= 0) return null;
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            var hdc = g.GetHdc();
            bool ok = PrintWindow(hWnd, hdc, 2);
            g.ReleaseHdc(hdc);
            if (!ok) return null;
        }
        return (Bitmap)bmp.Clone();
    }

    public static string? CaptureToFile(IntPtr hWnd, string outPath)
    {
        using var bmp = Capture(hWnd);
        if (bmp == null) return null;
        bmp.Save(outPath, ImageFormat.Jpeg);
        return outPath;
    }
}
