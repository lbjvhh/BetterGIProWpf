using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BetterGIProWpf.Services.LocalAI;

namespace BetterGIProWpf;

public partial class App : Application
{
    private const string MutexName = "BetterGIProWpf_SingleInstance_8F3A2C7B";
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now:HH:mm:ss}] ui {args.Exception}\n"); }
            catch { }
            MessageBox.Show("发生未处理的界面异常：\n" + args.Exception.Message, "BetterGI Pro",
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
