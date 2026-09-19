namespace BetterGIProWpf.Services.EndToEnd;

public interface IVirtualGamepad
{
    string Name { get; }
    void SetState(GamePadOutput state);
}

public class KeyboardVirtualGamepad : IVirtualGamepad
{
    public string Name => "键鼠映射手柄";
    private readonly object _lock = new();

    public void SetState(GamePadOutput state)
    {
        lock (_lock)
        {
            Set("W", state.LeftY < -0.4f); Set("S", state.LeftY > 0.4f);
            Set("A", state.LeftX < -0.4f); Set("D", state.LeftX > 0.4f);
            Set("SPACE", state.Buttons.Contains("A"));
            Set("SHIFT", state.Buttons.Contains("B"));
            Set("E", state.Buttons.Contains("X"));
            Set("Q", state.Buttons.Contains("Y"));
        }
    }

    private static void Set(string key, bool on)
    {
        if (on) InputSimulator.KeyDown(key); else InputSimulator.KeyUp(key);
    }
}
