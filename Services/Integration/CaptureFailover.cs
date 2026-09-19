namespace BetterGIProWpf.Services.Integration;
public enum CaptureMode { Wgc, BitBlt, Dxgi }
public class CaptureFailover
{
    private readonly List<CaptureMode> _order = new() { CaptureMode.Wgc, CaptureMode.BitBlt, CaptureMode.Dxgi };
    public int MaxConsecutiveFails { get; set; } = 5;
    public double FrozenFrameThreshold { get; set; } = 0.5;
    public CaptureMode Current { get; private set; } = CaptureMode.Wgc;
    private int _consecutiveFails; private byte[]? _lastFrame;
    public IReadOnlyList<string> SwitchHistory { get; private set; } = Array.Empty<string>();
    public bool RegisterResult(bool ok, byte[]? frameRgb)
    {
        var isFrozen = false;
        if (ok && frameRgb != null && _lastFrame != null && frameRgb.Length == _lastFrame.Length) isFrozen = IsNearlySame(_lastFrame, frameRgb);
        if (frameRgb != null) _lastFrame = frameRgb;
        if (!ok || isFrozen) { _consecutiveFails++; if (_consecutiveFails >= MaxConsecutiveFails) { SwitchNext(); _consecutiveFails = 0; return true; } return false; }
        _consecutiveFails = 0; return false;
    }
    private void SwitchNext() { var idx = _order.IndexOf(Current); var next = _order[(idx + 1) % _order.Count]; if (next == Current) return; Current = next; SwitchHistory = SwitchHistory.Append($"{DateTime.Now:HH:mm:ss} → {next}").ToArray(); }
    public void Force(CaptureMode mode) { if (_order.Contains(mode) && mode != Current) { Current = mode; _consecutiveFails = 0; SwitchHistory = SwitchHistory.Append($"手动 → {mode}").ToArray(); } }
    public static double MeanDiff(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return double.MaxValue; long s = 0; for (int i = 0; i < a.Length; i += 3) s += Math.Abs(a[i] - b[i]); return s / (double)(a.Length/3) / 255.0; }
    private bool IsNearlySame(byte[] a, byte[] b) => MeanDiff(a, b) < FrozenFrameThreshold;
    public bool CurrentAvailable() => Current != CaptureMode.Dxgi;
    public System.Drawing.Bitmap? Capture(IntPtr hwnd) => Current == CaptureMode.Dxgi ? null : WindowCaptureService.CaptureWindow(hwnd);
}
