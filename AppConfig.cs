using System.IO; using System.Text.Json; namespace BetterGIProWpf;
public static class AppConfig {
    private static readonly string P=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"BetterGIProWpf","config.json");
    public static AiConfig Ai {get;private set;}=new();
    static AppConfig(){try{if(File.Exists(P)){var d=JsonSerializer.Deserialize<AppData>(File.ReadAllText(P));if(d?.Ai!=null)Ai=d.Ai;}}catch{}}
    public static void Save(AiConfig ai){Ai=ai;try{Directory.CreateDirectory(Path.GetDirectoryName(P)!);File.WriteAllText(P,JsonSerializer.Serialize(new AppData{Ai=ai},new JsonSerializerOptions{WriteIndented=true}));}catch{}}
    class AppData{public AiConfig? Ai{get;set;}}
}
