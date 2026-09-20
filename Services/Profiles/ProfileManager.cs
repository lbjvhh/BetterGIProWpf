using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BetterGIProWpf.Services.Profiles;

public class GameProfile
{
    public string Name { get; set; } = "default";
    public string Game { get; set; } = "genshin";
    public string RootDir { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public static class ProfileManager
{
    private static readonly string _baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterGIProWpf", "profiles");
    private static readonly string _indexPath = Path.Combine(_baseDir, "index.json");
    public static GameProfile Current { get; private set; } = new();
    private static List<GameProfile> _profiles = new();

    static ProfileManager() { Load(); }

    private static void Load()
    {
        try
        {
            if (File.Exists(_indexPath))
            {
                var data = JsonSerializer.Deserialize<ProfileIndex>(File.ReadAllText(_indexPath));
                if (data != null) { _profiles = data.Profiles ?? new(); Current = _profiles.Find(p => p.Name == (data.Current ?? "default")) ?? new GameProfile { Name = "default", RootDir = _baseDir };
                }
            }
            else { Current = new GameProfile { Name = "default", RootDir = Path.Combine(_baseDir, "default") }; _profiles.Add(Current); Save(); }
        }
        catch { Current = new GameProfile { Name = "default", RootDir = _baseDir }; }
    }

    private static void Save()
    {
        try { Directory.CreateDirectory(_baseDir); File.WriteAllText(_indexPath, JsonSerializer.Serialize(new ProfileIndex { Profiles = _profiles, Current = Current.Name }, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }

    public static List<GameProfile> ListProfiles() => _profiles;

    public static void Switch(string name)
    {
        var p = _profiles.Find(x => x.Name == name);
        if (p == null) p = CreateProfile(name);
        Current = p; Directory.CreateDirectory(p.RootDir); Save();
        AppLogger.Info($"[Profile] switched to {p.Name}");
    }

    public static GameProfile CreateProfile(string name, string game = "genshin")
    {
        var dir = Path.Combine(_baseDir, name); Directory.CreateDirectory(dir);
        var p = new GameProfile { Name = name, Game = game, RootDir = dir }; _profiles.Add(p); Save(); return p;
    }

    public static void DeleteProfile(string name) { if (name == "default") return; _profiles.RemoveAll(p => p.Name == name); Save(); }

    public static string UserDir => Path.Combine(Current.RootDir, "User");
    public static string LogDir => Path.Combine(Current.RootDir, "logs");

    private class ProfileIndex { public List<GameProfile>? Profiles { get; set; } public string? Current { get; set; } }
}
