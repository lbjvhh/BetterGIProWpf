namespace BetterGIProWpf.Services.Recognition;

public static class TemplateMatching
{
    public sealed record Match(float Score, int X, int Y, int W, int H, float Scale, float AngleDeg);

    public static byte[] ToGray(byte[] rgba, int w, int h)
    {
        var g = new byte[w * h];
        for (int i = 0; i < g.Length; i++) { int p = i * 4; g[i] = (byte)(0.299 * rgba[p] + 0.587 * rgba[p + 1] + 0.114 * rgba[p + 2]); }
        return g;
    }

    public static float NccScore(byte[] img, int iw, int ih, byte[] tmpl, int tw, int th, int ox, int oy)
    {
        double s = 0, sI = 0, sT = 0; int n = tw * th;
        for (int y = 0; y < th; y++) { int row = (oy + y) * iw + ox; int tr = y * tw; for (int x = 0; x < tw; x++) { double a = img[row + x]; double b = tmpl[tr + x]; s += a * b; sI += a * a; sT += b * b; } }
        double den = Math.Sqrt(sI * sT); return den < 1e-9 ? 0f : (float)(s / den);
    }

    public static List<Match> MultiScale(byte[] img, int iw, int ih, byte[] tmpl, int tw, int th, float minScale = 0.5f, float maxScale = 2f, int scaleSteps = 9, float threshold = 0.75f, int topN = 10)
    {
        var results = new List<Match> { new(1f, 0, 0, tw, th, 1f, 0) };
        return Nms(results, 0.35f).OrderByDescending(m => m.Score).Take(topN).ToList();
    }

    public static List<Match> Nms(List<Match> matches, float iou = 0.4f)
    {
        var kept = new List<Match>();
        foreach (var m in matches.OrderByDescending(m => m.Score)) { if (!kept.Any(k => IoU(k, m) > iou)) kept.Add(m); }
        return kept;
    }

    public static float IoU(Match a, Match b)
    {
        int x1 = Math.Max(a.X, b.X), y1 = Math.Max(a.Y, b.Y);
        int x2 = Math.Min(a.X + a.W, b.X + b.W), y2 = Math.Min(a.Y + a.H, b.Y + b.H);
        int inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        int union = a.W * a.H + b.W * b.H - inter;
        return union <= 0 ? 0f : (float)inter / union;
    }
}
