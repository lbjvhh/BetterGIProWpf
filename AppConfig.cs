using System.IO;
using System.Text.Json;
using BetterGIProWpf.Services;

namespace BetterGIProWpf;

public static class AppConfig
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BetterGIProWpf", "config.json");

    public static AiConfig Ai { get; private set; } = new();

    static AppConfig()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<AppConfigData>(json);
                if (loaded?.Ai != null) Ai = loaded.Ai;
            }
        }
        catch { }
    }

    public static void Save(AiConfig ai)
    {
        Ai = ai;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new AppConfigData { Ai = ai },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private class AppConfigData { public AiConfig? Ai { get; set; } }
}
