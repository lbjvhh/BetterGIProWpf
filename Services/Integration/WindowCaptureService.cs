using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BetterGIProWpf.Services.Integration;

public static class WindowCaptureService
{
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint nFlags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdcDst, int x, int y, int w, int h, IntPtr hdcSrc, int sx, int sy, uint rop);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    private const uint SRCCOPY = 0x00CC0020;
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    public static Bitmap? CaptureWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        if (!GetWindowRect(hwnd, out var r)) return null;
        var w = r.Right - r.Left; var h = r.Bottom - r.Top;
        if (w <= 0 || h <= 0) return null;
        var src = GetDC(IntPtr.Zero);
        var mem = CreateCompatibleDC(src);
        var bmp = CreateCompatibleBitmap(src, w, h);
        var old = SelectObject(mem, bmp);
        try
        {
            BitBlt(mem, 0, 0, w, h, src, r.Left, r.Top, SRCCOPY);
            if (!PrintWindow(hwnd, mem, PW_RENDERFULLCONTENT)) { }
            return Image.FromHbitmap(bmp);
        }
        finally { SelectObject(mem, old); DeleteObject(bmp); DeleteDC(mem); ReleaseDC(IntPtr.Zero, src); }
    }

    public static byte[]? CaptureRgb(IntPtr hwnd, int targetW, int targetH)
    {
        using var bmp = CaptureWindow(hwnd);
        if (bmp == null) return null;
        using var resized = new Bitmap(bmp, targetW, targetH);
        var data = resized.LockBits(new Rectangle(0, 0, targetW, targetH), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var bytes = new byte[targetW * targetH * 3];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return bytes;
        }
        finally { resized.UnlockBits(data); }
    }
}
