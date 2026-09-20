using System;
using System.IO;
using System.Text.Json;
using BetterGIProWpf.Services;

namespace BetterGIProWpf;

public class ServicePorts
{
    public int NitroGenBridge { get; set; } = 5003;
    public int VisionServer { get; set; } = 5004;
    public int StreamBridge { get; set; } = 5005;
    public int NitroGenZmq { get; set; } = 5555;
    public string Host { get; set; } = "127.0.0.1";
}

public static class AppConfig
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BetterGIProWpf", "config.json");

    public static AiConfig Ai { get; private set; } = new();
    public static ServicePorts Ports { get; private set; } = new();

    static AppConfig()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<AppConfigData>(json);
                if (loaded?.Ai != null) Ai = loaded.Ai;
                if (loaded?.Ports != null) Ports = loaded.Ports;
            }
        }
        catch { }
    }

    public static void Save(AiConfig ai) { Ai = ai; Save(); }
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(new AppConfigData { Ai = Ai, Ports = Ports }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static string NitroGenUrl => $"http://{Ports.Host}:{Ports.NitroGenBridge}";
    public static string VisionUrl => $"http://{Ports.Host}:{Ports.VisionServer}";
    public static string StreamUrl => $"http://{Ports.Host}:{Ports.StreamBridge}";

    private class AppConfigData { public AiConfig? Ai { get; set; } public ServicePorts? Ports { get; set; } }
}
