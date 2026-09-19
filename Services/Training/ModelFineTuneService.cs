using System.Diagnostics;

namespace BetterGIProWpf.Services.Training;

public sealed class ModelFineTuneService
{
    private const string DatasetDir = @"C:\better\recorded_runs";
    private const string WorkDir = @"C:\better\NitroGen-Full";

    public sealed record Status(int RunCount, long FrameCount, bool CudaAvailable, string GpuName, bool FinalReady, string Recommendation);

    public Status GetStatus()
    {
        int runs = 0; long frames = 0;
        if (Directory.Exists(DatasetDir)) foreach (var d in Directory.GetDirectories(DatasetDir)) { var a = Path.Combine(d, "actions.jsonl"); if (File.Exists(a)) { runs++; frames += File.ReadLines(a).Count(); } }
        bool finalReady = File.Exists(Path.Combine(WorkDir, "final_model.pt"));
        string rec = finalReady ? "微调完成，推荐 final_model.pt (1.0x)" : (runs < 34 ? $"数据 {runs}/34 run" : "可开训");
        return new Status(runs, frames, false, "无 CUDA", finalReady, rec);
    }

    public bool StartTraining() { try { Process.Start(new ProcessStartInfo { FileName = Path.Combine(WorkDir, "run_finetune.bat"), UseShellExecute = true }); return true; } catch { return false; } }
}
