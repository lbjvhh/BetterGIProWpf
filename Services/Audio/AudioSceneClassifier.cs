namespace BetterGIProWpf.Services.Audio;

public enum SceneMusicType { Combat, Exploration, Town, Dialogue, Menu, Loading }

public class AudioSceneClassifier
{
    public record AudioFrame(double Rms, double ZeroCrossingRate, double SpectralCentroid);

    public double RecognitionLatencyMs { get; private set; }
    public event Action<SceneMusicType>? SceneDetected;

    public static AudioFrame Extract(byte[] pcm16)
    {
        var n = pcm16.Length / 2;
        if (n == 0) return new AudioFrame(0, 0, 0);
        double sum = 0, sum2 = 0, zcr = 0, prev = 0, magSum = 0;
        for (var i = 0; i < n; i++)
        {
            var s = BitConverter.ToInt16(pcm16, i * 2) / 32768d;
            sum += Math.Abs(s); sum2 += s * s;
            if (i > 0 && ((s >= 0) != (prev >= 0))) zcr++;
            prev = s;
        }
        var rms = Math.Sqrt(sum2 / n);
        for (var i = 1; i < n; i++)
        {
            var d = Math.Abs(BitConverter.ToInt16(pcm16, i * 2) / 32768d - BitConverter.ToInt16(pcm16, (i - 1) * 2) / 32768d);
            magSum += d;
        }
        return new AudioFrame(rms, zcr / n, n < 2 ? 0 : magSum / (n - 1));
    }

    public SceneMusicType Classify(AudioFrame f)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        SceneMusicType type;
        if (f.Rms < 0.015) type = SceneMusicType.Loading;
        else if (f.Rms > 0.18 && f.ZeroCrossingRate > 0.12) type = SceneMusicType.Combat;
        else if (f.Rms < 0.05) type = SceneMusicType.Menu;
        else if (f.ZeroCrossingRate < 0.04) type = SceneMusicType.Town;
        else if (f.SpectralCentroid < 0.12) type = SceneMusicType.Dialogue;
        else type = SceneMusicType.Exploration;
        sw.Stop();
        RecognitionLatencyMs = sw.Elapsed.TotalMilliseconds;
        SceneDetected?.Invoke(type);
        return type;
    }

    public static string Fuse(SceneMusicType audio, string visualState)
    {
        if (audio == SceneMusicType.Loading) return "加载中";
        if (audio == SceneMusicType.Combat && visualState.Contains("战斗", StringComparison.OrdinalIgnoreCase)) return "战斗中";
        if (audio == SceneMusicType.Combat && visualState.Contains("探索", StringComparison.OrdinalIgnoreCase)) return "疑似战斗（音频确认）";
        return visualState.Length > 0 ? visualState : audio.ToString();
    }
}
