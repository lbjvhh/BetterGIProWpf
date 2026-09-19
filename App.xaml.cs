using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BetterGIProWpf.Services.LocalAI;

namespace BetterGIProWpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string MutexName = "BetterGIProWpf_SingleInstance_8F3A2C7B";
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例保护：已有实例时激活其主窗口并退出。
        _singleInstance = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }

        // 全局异常兜底：任何未处理异常（含 WebView2 初始化失败）不再直接闪退
        DispatcherUnhandledException += (_, args) =>
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"[{DateTime.Now:HH:mm:ss}] ui {args.Exception}\n"); }
            catch { }
            MessageBox.Show("发生未处理的界面异常：\n" + args.Exception.Message + "\n\n已尝试继续运行。", "BetterGI Pro",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"[{DateTime.Now:HH:mm:ss}] app {args.ExceptionObject}\n"); }
            catch { }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"[{DateTime.Now:HH:mm:ss}] task {args.Exception}\n"); }
            catch { }
            args.SetObserved();
        };

        LocalAiEngine.Init();

        // P1-4：知识库持久化（启动时从 User/knowledge.json 加载）
        try
        {
            var kp = Path.Combine(AppContext.BaseDirectory, "User", "knowledge.json");
            if (File.Exists(kp))
            {
                var json = File.ReadAllText(kp, System.Text.Encoding.UTF8);
                AppState.Knowledge.ImportJson(json);
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now:HH:mm:ss}] knowledge load {ex.Message}\n");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // P1-4：退出时保存知识库到 User/knowledge.json
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "User");
            Directory.CreateDirectory(dir);
            var kp = Path.Combine(dir, "knowledge.json");
            File.WriteAllText(kp, AppState.Knowledge.ExportJson(), System.Text.Encoding.UTF8);
        }
        catch { }
        base.OnExit(e);
    }

    private static void ActivateExistingInstance()
    {
        IntPtr h = IntPtr.Zero;
        var current = Process.GetCurrentProcess().Id;
        for (int i = 0; i < 20 && h == IntPtr.Zero; i++)
        {
            foreach (var p in Process.GetProcessesByName("BetterGIProWpf"))
            {
                if (p.Id == current) continue;
                h = p.MainWindowHandle;
                if (h != IntPtr.Zero) break;
            }
            if (h == IntPtr.Zero) Thread.Sleep(100);
        }
        if (h != IntPtr.Zero)
        {
            ShowWindow(h, 9);
            SetForegroundWindow(h);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
