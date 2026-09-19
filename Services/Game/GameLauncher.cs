using System.Diagnostics; using Microsoft.Win32; namespace BetterGIProWpf.Services.Game;
public sealed class GameLauncher {
    public bool IsRunning(){foreach(var p in Process.GetProcessesByName("YuanShen"))return !p.HasExited;return false;}
    public string? FindPath(){foreach(var k in new[]{@"HKCU\Software\miHoYo\Genshin Impact",@"HKLM\SOFTWARE\miHoYo\Genshin Impact"}){try{var v=Registry.GetValue(k,"GameInstallPath",null) as string;if(!string.IsNullOrEmpty(v)){var e=Path.Combine(v,"YuanShen.exe");if(File.Exists(e))return e;}}catch{}}return null;}
    public bool Launch(){if(IsRunning())return true;var p=FindPath();if(string.IsNullOrEmpty(p))return false;try{Process.Start(new ProcessStartInfo{FileName=p,UseShellExecute=true});return true;}catch{return false;}}
}
