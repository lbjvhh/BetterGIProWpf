using System.Drawing;

namespace BetterGIProWpf.Services;

public class PadState
{
    public float LX { get; set; }
    public float LY { get; set; }
    public float RX { get; set; }
    public float RY { get; set; }
    public bool A { get; set; }
    public bool B { get; set; }
    public bool X { get; set; }
    public bool Y { get; set; }
    public bool LB { get; set; }
    public bool RB { get; set; }
    public float Confidence { get; set; } = 1.0f;
}

public interface IGameAdapter
{
    string GameId { get; }
    IntPtr FindGameWindow();
    Task<Bitmap> CaptureGameFrameAsync();
    void ApplyPad(PadState pad);
    string DetectGameVersion();
}

public interface IEndToEndPolicy
{
    Task<PadState> InferAsync(Bitmap frame, CancellationToken ct);
    bool IsLoaded { get; }
    Task LoadAsync(string modelPath);
}
