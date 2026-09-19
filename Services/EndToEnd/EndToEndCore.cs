namespace BetterGIProWpf.Services.EndToEnd;

public class GamePadOutput
{
    public float LeftX { get; set; }
    public float LeftY { get; set; }
    public float RightX { get; set; }
    public float RightY { get; set; }
    public HashSet<string> Buttons { get; set; } = new();
    public float Confidence { get; set; } = 0.5f;

    public bool IsNeutral => Math.Abs(LeftX) < 0.1f && Math.Abs(LeftY) < 0.1f &&
                             Math.Abs(RightX) < 0.1f && Math.Abs(RightY) < 0.1f && Buttons.Count == 0;
}

public interface IEndToEndAgent
{
    string Name { get; }
    string Status { get; }
    Task<GamePadOutput> InferAsync(byte[] rgbFrame, int width, int height);
    void Load();
    void Unload();
}

public class MockEndToEndAgent : IEndToEndAgent
{
    public string Name => "模拟端到端代理";
    public string Status { get; private set; } = "未加载";

    public void Load() => Status = "已加载";
    public void Unload() => Status = "已卸载";

    public Task<GamePadOutput> InferAsync(byte[] rgbFrame, int width, int height)
    {
        double sum = 0;
        if (rgbFrame != null) for (int i = 0; i < rgbFrame.Length && i < width * height; i++) sum += rgbFrame[i];
        var out_ = new GamePadOutput { LeftY = -0.8f, Confidence = 0.25f };
        if (rgbFrame?.Length > 0 && sum / rgbFrame.Length > 200) out_.Buttons.Add("A");
        return Task.FromResult(out_);
    }
}
