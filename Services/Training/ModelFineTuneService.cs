using System.Diagnostics; using System.IO; using System.Linq; namespace BetterGIProWpf.Services.Training;
public sealed class ModelFineTuneService {
    private const string DS = @"C:\better\recorded_runs"; private const string WD = @"C:\better\NitroGen-Full";
    public sealed record Status(int Runs, long Frames, bool Cuda, string Gpu, bool BaseReady, bool FinalReady, string Rec);
    public Status GetStatus() {
        int runs=0; long frames=0;
        if (Directory.Exists(DS)) foreach (var d in Directory.GetDirectories(DS)) { var a=Path.Combine(d,"actions.jsonl"); if (File.Exists(a)) { runs++; frames+=File.ReadLines(a).Count(); } }
        bool cuda=false; string gpu="CPU";
        try { var psi = new ProcessStartInfo("python","-c \"import torch;print(torch.cuda.is_available());print(torch.cuda.get_device_name(0) if torch.cuda.is_available() else '')\"") { RedirectStandardOutput=true, UseShellExecute=false, CreateNoWindow=true }; using var p=Process.Start(psi); var o=p.StandardOutput.ReadToEnd().Trim().Split('\n'); if (o.Length>=1 && bool.TryParse(o[0].Trim(), out cuda)) gpu = cuda&&o.Length>=2?o[1].Trim():"CPU"; } catch { }
        bool br = File.Exists(@"C:\better\BetterGIProWpf-App\Models\ng.pt");
        bool fr = File.Exists(Path.Combine(WD,"final_model.pt"));
        string rec = fr ? "微调完成，推荐 final_model.pt (1.0x, Macro F1 44.61%)" : !cuda ? "需 CUDA GPU (RTX 4070 Ti Super+)" : runs<34 ? $"数据 {runs}/34 run" : "就绪，运行 run_finetune.bat";
        return new Status(runs, frames, cuda, gpu, br, fr, rec);
    }
    public bool StartTraining() { try { Process.Start(new ProcessStartInfo { FileName=Path.Combine(WD,"run_finetune.bat"), UseShellExecute=true }); return true; } catch { return false; } }
}
