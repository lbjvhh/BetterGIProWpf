using System.Runtime.InteropServices;

namespace BetterGIProWpf.Services;

public static class InputSimulator
{
    [Flags]
    private enum InputType { Mouse = 0, Keyboard = 1 }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll")] private static extern uint SendInput(uint n, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void Move(int x, int y) => SetCursorPos(x, y);

    public static void Click(int x, int y, string button = "left")
    {
        SetCursorPos(x, y);
        var down = button == "right" ? 0x0008u : MOUSEEVENTF_LEFTDOWN;
        var up = button == "right" ? 0x0010u : MOUSEEVENTF_LEFTUP;
        var inputs = new[] {
            new INPUT { type = (uint)InputType.Mouse, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = down } } },
            new INPUT { type = (uint)InputType.Mouse, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = up } } },
        };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    public static void KeyPress(string key)
    {
        var vk = KeyToVk(key);
        if (vk == 0) return;
        var inputs = new[] {
            new INPUT { type = (uint)InputType.Keyboard, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk } } },
            new INPUT { type = (uint)InputType.Keyboard, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } },
        };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    public static void KeyDown(string key)
    {
        var vk = KeyToVk(key);
        if (vk == 0) return;
        var inputs = new[] { new INPUT { type = (uint)InputType.Keyboard, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk } } } } };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    public static void KeyUp(string key)
    {
        var vk = KeyToVk(key);
        if (vk == 0) return;
        var inputs = new[] { new INPUT { type = (uint)InputType.Keyboard, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } } } };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    private static ushort KeyToVk(string key) => key.ToUpperInvariant() switch
    {
        "W" => 0x57, "A" => 0x41, "S" => 0x53, "D" => 0x44,
        "E" => 0x45, "F" => 0x46, "Q" => 0x51, "R" => 0x52,
        "SPACE" => 0x20, "ESC" => 0x1B, "TAB" => 0x09, "ENTER" => 0x0D,
        "SHIFT" => 0x10, "CTRL" => 0x11, _ => (ushort)0,
    };
}
