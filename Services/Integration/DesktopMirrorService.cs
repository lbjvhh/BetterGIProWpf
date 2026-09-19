namespace BetterGIProWpf.Services.Integration;

public class DesktopMirrorService
{
    public enum MirrorState { Off, Switching, Background, Foreground }
    public MirrorState State { get; private set; } = MirrorState.Off;
    public bool TaskPaused { get; private set; }
    public event Action<string>? Log;
    public event Action<string>? Notify;

    public void EnterBackground()
    {
        if (State is MirrorState.Background) return;
        State = MirrorState.Switching;
        Log?.Invoke("切换到后台模式…");
        System.Threading.Thread.Sleep(200);
        State = MirrorState.Background;
        TaskPaused = false;
        Log?.Invoke("后台模式已启用");
    }

    public void EnterForeground()
    {
        if (State is MirrorState.Foreground or MirrorState.Off) return;
        State = MirrorState.Switching;
        System.Threading.Thread.Sleep(150);
        State = MirrorState.Foreground;
        Log?.Invoke("前台模式已恢复，任务未中断");
    }

    public void NotifyAbnormal(string message) { Notify?.Invoke(message); Log?.Invoke($"[通知] {message}"); }
    public void PauseTask() { TaskPaused = true; }
    public void ResumeTask() { TaskPaused = false; }
}
