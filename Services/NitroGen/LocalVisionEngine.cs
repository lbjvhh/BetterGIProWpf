using System;

namespace BetterGIProWpf.Services.NitroGen;

public sealed class LocalVisionEngine
{
    private float[]? _lastThumb;
    private float _stickLx, _stickLy, _stickRx, _stickRy;
    private float _attackHold;
    private float _movePhase;

    public const int Tw = 32, Th = 18;

    public sealed record VisionEvents(bool Moving, bool Attacking, bool Dashing, bool Jump, bool Interact, float Warm, float Motion, float Saliency);
    public sealed record InferResult(ActionBlock Actions, float Confidence, string Mode, VisionEvents? Events);

    public InferResult Infer(RgbFrame[] frames)
    {
        if (frames == null || frames.Length < 2) return Empty("insufficient");
        var last = frames[^1];
        var thumb = ToGrayThumb(last);
        var mv = _lastThumb is null ? (X: 0f, Y: 0f, Mag: 0f) : MotionVector(_lastThumb, thumb);
        _lastThumb = thumb;
        float warm = WarmRatio(last);
        float sal = CenterSaliency(last);
        float moveX = Math.Clamp(mv.X * 2.2f, -1f, 1f);
        float moveY = Math.Clamp(mv.Y * 2.2f, -1f, 1f);
        bool moving = mv.Mag > 0.05f;
        _movePhase += moving ? mv.Mag * 0.5f : -0.3f;
        _movePhase = Math.Clamp(_movePhase, 0f, 1f);
        bool attacking = warm > 0.09f && sal > 0.05f && mv.Mag > 0.06f;
        _attackHold = attacking ? 1f : Math.Max(0f, _attackHold - 0.35f);
        bool dashing = mv.Mag > 0.45f;
        bool jump = moving && mv.Mag > 0.3f && (_movePhase > 0.92f || (warm > 0.16f && _movePhase > 0.6f));
        bool interact = !moving && sal > 0.10f && warm < 0.06f;
        float rx = attacking ? mv.X * 0.4f : 0, ry = attacking ? mv.Y * 0.4f : 0;
        _stickLx = SmoothStick(_stickLx, moveX, 0.35f);
        _stickLy = SmoothStick(_stickLy, moveY, 0.35f);
        _stickRx = SmoothStick(_stickRx, rx, 0.3f);
        _stickRy = SmoothStick(_stickRy, ry, 0.3f);
        var actions = new ActionBlock();
        for (int s = 0; s < NitroGenConst.BlockSteps; s++)
        {
            int off = s * NitroGenConst.StepDim;
            actions.Data[off] = (_stickLx + 1f) / 2f;
            actions.Data[off + 1] = (_stickLy + 1f) / 2f;
            actions.Data[off + 2] = (_stickRx + 1f) / 2f;
            actions.Data[off + 3] = (_stickRy + 1f) / 2f;
            if (_attackHold > 0.5f) actions.Data[off + 4 + NitroGenConst.Btn.A] = 1f;
            if (dashing) actions.Data[off + 4 + NitroGenConst.Btn.RT] = 1f;
            if (jump && s % 4 == 0) actions.Data[off + 4 + NitroGenConst.Btn.B] = 1f;
            if (interact && s == 0) actions.Data[off + 4 + NitroGenConst.Btn.X] = 1f;
        }
        float confidence = Math.Clamp(0.35f + mv.Mag * 0.3f + (attacking ? 0.2f : 0f) + (interact ? 0.2f : 0f), 0.05f, 0.95f);
        return new InferResult(actions, confidence, "local-vision",
            new VisionEvents(moving, attacking, dashing, jump, interact, warm, mv.Mag, sal));
    }

    public InferResult Empty(string reason) => new(new ActionBlock(), 0f, reason, null);

    public void Reset()
    {
        _lastThumb = null;
        _stickLx = _stickLy = _stickRx = _stickRy = 0f;
        _attackHold = 0f; _movePhase = 0f;
    }

    public static float[] ToGrayThumb(RgbFrame f)
    {
        var outA = new float[Tw * Th];
        for (int y = 0; y < Th; y++)
        {
            int sy = (int)Math.Floor((y / (double)Th) * f.Height);
            for (int x = 0; x < Tw; x++)
            {
                int sx = (int)Math.Floor((x / (double)Tw) * f.Width);
                int i = (sy * f.Width + sx) * 4;
                outA[y * Tw + x] = 0.299f * f.Data[i] + 0.587f * f.Data[i + 1] + 0.114f * f.Data[i + 2];
            }
        }
        return outA;
    }

    public static (float X, float Y, float Mag) MotionVector(float[] a, float[] b)
    {
        double dx = 0, dy = 0, n = 0;
        for (int y = 1; y < Th - 1; y++)
            for (int x = 1; x < Tw - 1; x++)
            {
                int i = y * Tw + x;
                float d = b[i] - a[i];
                float mx = b[i + 1] - b[i - 1];
                float my = b[i + Tw] - b[i - Tw];
                if (Math.Abs(d) > 4f)
                {
                    double g = Math.Sqrt(mx * mx + my * my) + 1e-6;
                    dx += (mx / g) * Math.Abs(d);
                    dy += (my / g) * Math.Abs(d);
                    n += Math.Abs(d);
                }
            }
        if (n < 1e-3) return (0, 0, 0);
        return ((float)(dx / n), (float)(dy / n), (float)Math.Min(1, n / (Tw * Th * 12.0)));
    }

    public static float WarmRatio(RgbFrame f)
    {
        int warm = 0, total = 0;
        int step = Math.Max(1, (f.Width * f.Height) / (160 * 90));
        for (int i = 0; i < f.Data.Length; i += 4 * step)
        {
            int r = f.Data[i], g = f.Data[i + 1], b = f.Data[i + 2];
            total++;
            if (r > 120 && r > g * 1.35 && r > b * 1.35) warm++;
        }
        return total == 0 ? 0f : (float)warm / total;
    }

    public static float CenterSaliency(RgbFrame f)
    {
        int cx0 = (int)Math.Floor(f.Width * 0.3), cy0 = (int)Math.Floor(f.Height * 0.25);
        int cx1 = (int)Math.Floor(f.Width * 0.7), cy1 = (int)Math.Floor(f.Height * 0.75);
        double center = 0, edge = 0; int cn = 0, en = 0;
        for (int y = cy0; y < cy1; y += 4)
            for (int x = cx0; x < cx1; x += 4)
            {
                int i = (y * f.Width + x) * 4;
                center += (f.Data[i] + f.Data[i + 1] + f.Data[i + 2]) / 3.0;
                cn++;
            }
        for (int y = 0; y < f.Height; y += 4)
            for (int x = 0; x < f.Width; x += 4)
            {
                if (x >= cx0 && x < cx1 && y >= cy0 && y < cy1) continue;
                int i = (y * f.Width + x) * 4;
                edge += (f.Data[i] + f.Data[i + 1] + f.Data[i + 2]) / 3.0;
                en++;
            }
        if (cn == 0 || en == 0) return 0f;
        double diff = (edge / en) - (center / cn);
        return (float)Math.Max(0, Math.Min(1, Math.Abs(diff) / 64.0));
    }

    private static float SmoothStick(float cur, float target, float t)
    {
        float ease = t * t * (3f - 2f * t);
        return cur + (target - cur) * ease;
    }
}
