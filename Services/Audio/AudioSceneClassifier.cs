namespace BetterGIProWpf.Services.Audio;

/// <summary>场景音乐类型（模块17 要求 ≥6 种）。</summary>
public enum SceneMusicType { Combat, Exploration, Town, Dialogue, Menu, Loading }

/// <summary>
/// 游戏音乐/音效识别与场景匹配（模块17）：CPU 推理（纯特征计算，不占 GPU），
/// 基于短时能量/过零率/频谱质心的启发式分类；与视觉识别融合输出综合状态。
/// </summary>
public class AudioSceneClassifier
{
    /// <summary>一帧音频特征（16kHz 单声道 50ms 窗口）。</summary>
    public record AudioFrame(double Rms, double ZeroCrossingRate, double SpectralCentroid);

    public double RecognitionLatencyMs { get; private set; }
    public event Action<SceneMusicType>? SceneDetected;

    /// <summary>从 PCM16 采样计算特征。</summary>
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
        // 简单频谱质心：帧内相邻差分绝对值作为高频代理
        for (var i = 1; i < n; i++)
        {
            var d = Math.Abs(BitConverter.ToInt16(pcm16, i * 2) / 32768d - BitConverter.ToInt16(pcm16, (i - 1) * 2) / 32768d);
            magSum += d;
        }
        return new AudioFrame(rms, zcr / n, n < 2 ? 0 : magSum / (n - 1));
    }

    /// <summary>分类：战斗（高能量高过零）、加载（极低能量）、菜单（中低能量低质心）、
    /// 对话（中能量）、城镇/探索（环境音乐中能量）。</summary>
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

    /// <summary>视觉+音频融合判定（误判率目标 &lt;5%）。</summary>
    public static string Fuse(SceneMusicType audio, string visualState)
    {
        if (audio == SceneMusicType.Loading) return "加载中";
        if (audio == SceneMusicType.Combat && visualState.Contains("战斗", StringComparison.OrdinalIgnoreCase)) return "战斗中";
        if (audio == SceneMusicType.Combat && visualState.Contains("探索", StringComparison.OrdinalIgnoreCase)) return "疑似战斗（音频确认）";
        return visualState.Length > 0 ? visualState : audio.ToString();
    }
}
