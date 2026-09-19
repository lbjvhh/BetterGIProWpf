using System.Text.Json;

namespace BetterGIProWpf.Services.Companion;

public class GameQaService
{
    public interface IVisualQaBackend
    {
        Task<string> AskAsync(string question, string imageBase64, string context);
    }

    private readonly List<(DateTime T, string Role, string Text)> _history = new();
    private readonly object _lock = new();

    public IVisualQaBackend? Backend { get; set; }
    public int ContextMinutes { get; set; } = 5;
    public event Action<string>? Log;

    public async Task<string> AskAsync(string question, string imageBase64)
    {
        if (Backend == null) { Log?.Invoke("未配置视觉问答后端"); return "未配置视觉问答后端"; }
        var context = BuildContext();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var answer = await Backend.AskAsync(question, imageBase64, context);
        sw.Stop();
        lock (_lock)
        {
            _history.Add((DateTime.Now, "user", question));
            _history.Add((DateTime.Now, "assistant", answer));
            Trim();
        }
        Log?.Invoke($"[问答] {question} → {answer}（{sw.ElapsedMilliseconds}ms）");
        return answer;
    }

    private string BuildContext()
    {
        lock (_lock)
        {
            var cutoff = DateTime.Now.AddMinutes(-ContextMinutes);
            return string.Join("\n", _history.Where(h => h.T >= cutoff).Select(h => $"{h.Role}: {h.Text}"));
        }
    }

    private void Trim()
    {
        var cutoff = DateTime.Now.AddMinutes(-ContextMinutes);
        _history.RemoveAll(h => h.T < cutoff);
        if (_history.Count > 100) _history.RemoveRange(0, _history.Count - 100);
    }

    public IReadOnlyList<(DateTime T, string Role, string Text)> History { get { lock (_lock) return _history.ToArray(); } }
    public int TurnCount { get { lock (_lock) return _history.Count / 2; } }
}
