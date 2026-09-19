namespace BetterGIProWpf.Services;

public class OperationStep
{
    public string Type { get; set; } = "wait";
    public string? Key { get; set; }
    public int X { get; set; } = 960;
    public int Y { get; set; } = 540;
    public int Duration { get; set; } = 500;
    public string? Desc { get; set; }
}

public class StepExecutor
{
    private CancellationTokenSource? _cts;
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    public async Task RunAsync(IList<OperationStep> steps, Action<int, OperationStep> onStep, Action onDone)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        for (int i = 0; i < steps.Count; i++)
        {
            if (token.IsCancellationRequested) break;
            var s = steps[i];
            onStep(i, s);
            try { await ExecuteOneAsync(s, token); }
            catch (TaskCanceledException) { break; }
            catch { }
        }
        onDone();
    }

    public void Stop() => _cts?.Cancel();

    private static async Task ExecuteOneAsync(OperationStep s, CancellationToken token)
    {
        switch (s.Type)
        {
            case "key":
                if (!string.IsNullOrEmpty(s.Key))
                {
                    InputSimulator.KeyDown(s.Key!);
                    await Task.Delay(s.Duration, token);
                    InputSimulator.KeyUp(s.Key!);
                }
                break;
            case "click":
                InputSimulator.Click(s.X, s.Y, "left");
                await Task.Delay(Math.Max(200, s.Duration), token);
                break;
            case "move":
                InputSimulator.Move(s.X, s.Y);
                await Task.Delay(Math.Max(100, s.Duration), token);
                break;
            default:
                await Task.Delay(s.Duration, token);
                break;
        }
    }
}
