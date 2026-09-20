using System;
using System.IO;
using System.Text;

namespace BetterGIProWpf.Services;

public static class AppLogger
{
    private static readonly string _logDir;
    private static readonly string _logFile;
    private static readonly object _lock = new();
    private static readonly System.Collections.Generic.Queue<string> _ring = new();
    public const int RingCapacity = 500;

    static AppLogger()
    {
        _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterGIProWpf", "logs");
        Directory.CreateDirectory(_logDir);
        _logFile = Path.Combine(_logDir, $"app_{DateTime.Now:yyyyMMdd}.log");
    }

    public static event Action<string>? OnLog;

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg) => Write("ERROR", msg);
    public static void Error(string msg, Exception ex) => Write("ERROR", $"{msg}: {ex.Message}\n{ex.StackTrace}");

    private static void Write(string level, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}";
        lock (_lock)
        {
            try { File.AppendAllText(_logFile, line + Environment.NewLine, Encoding.UTF8); } catch { }
            _ring.Enqueue(line);
            while (_ring.Count > RingCapacity) _ring.Dequeue();
        }
        try { OnLog?.Invoke(line); } catch { }
    }

    public static string[] Recent(int n = 200)
    {
        lock (_lock)
        {
            var arr = _ring.ToArray();
            return arr.Length <= n ? arr : arr.Skip(arr.Length - n).ToArray();
        }
    }

    public static string LogFilePath => _logFile;
}
