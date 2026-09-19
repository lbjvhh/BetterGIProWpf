using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace BetterGIProWpf.Services.Integration;
public static class WindowCaptureService
{
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr h);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr h, int w, int hh);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr h, IntPtr o);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, uint rop);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr h);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr o);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    private const uint SRCCOPY = 0x00CC0020; private const uint PW_RENDERFULLCONTENT = 0x00000002;
    public static Bitmap? CaptureWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return null;
        var w = r.Right - r.Left; var h = r.Bottom - r.Top; if (w <= 0 || h <= 0) return null;
        var src = GetDC(IntPtr.Zero); var mem = CreateCompatibleDC(src); var bmp = CreateCompatibleBitmap(src, w, h); var old = SelectObject(mem, bmp);
        try { BitBlt(mem, 0, 0, w, h, src, r.Left, r.Top, SRCCOPY); PrintWindow(hwnd, mem, PW_RENDERFULLCONTENT); return Image.FromHbitmap(bmp); }
        finally { SelectObject(mem, old); DeleteObject(bmp); DeleteDC(mem); ReleaseDC(IntPtr.Zero, src); }
    }
    public static byte[]? CaptureRgb(IntPtr hwnd, int tw, int th)
    {
        using var bmp = CaptureWindow(hwnd); if (bmp == null) return null;
        using var resized = new Bitmap(bmp, tw, th); var data = resized.LockBits(new Rectangle(0,0,tw,th), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try { var bytes = new byte[tw*th*3]; Marshal.Copy(data.Scan0, bytes, 0, bytes.Length); return bytes; }
        finally { resized.UnlockBits(data); }
    }
}
