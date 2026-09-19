namespace BetterGIProWpf.Services.EndToEnd;

/// <summary>虚拟手柄设备抽象。</summary>
public interface IVirtualGamepad
{
    string Name { get; }
    void SetState(GamePadOutput state);
}

/// <summary>把虚拟手柄输出映射为键鼠输入：左摇杆→WASD，按键→对应键。</summary>
public class KeyboardVirtualGamepad : IVirtualGamepad
{
    public string Name => "键鼠映射手柄";
    public void SetState(GamePadOutput state)
    {
        var up = state.LeftY < -0.4f; var down = state.LeftY > 0.4f;
        var left = state.LeftX < -0.4f; var right = state.LeftX > 0.4f;
        Set("W", up); Set("S", down); Set("A", left); Set("D", right);
        Set("SPACE", state.Buttons.Contains("A"));
        Set("SHIFT", state.Buttons.Contains("B"));
        Set("E", state.Buttons.Contains("X"));
        Set("Q", state.Buttons.Contains("Y"));
    }
    private static void Set(string key, bool on) { if (on) InputSimulator.KeyDown(key); else InputSimulator.KeyUp(key); }
}
