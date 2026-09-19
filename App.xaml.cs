using System.Diagnostics; using System.IO; using System.Runtime.InteropServices; using System.Windows; using System.Windows.Threading; using BetterGIProWpf.Services.LocalAI; namespace BetterGIProWpf;
public partial class App : Application {
    private const string MUT="BetterGIProWpf_Single_8F3A"; private Mutex? _mtx;
    protected override void OnStartup(StartupEventArgs e){base.OnStartup(e);_mtx=new Mutex(true,MUT,out bool ok);if(!ok){Activate();Shutdown();return;}
        DispatcherUnhandledException+=(_,a)=>{try{File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"crash.log"),$"[{DateTime.Now:HH:mm:ss}] ui {a.Exception}\n");}catch{}MessageBox.Show("UI异常:"+a.Exception.Message);a.Handled=true;};
        AppDomain.CurrentDomain.UnhandledException+=(_,a)=>{try{File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"crash.log"),$"[{DateTime.Now:HH:mm:ss}] app {a.ExceptionObject}\n");}catch{}};
        LocalAiEngine.Init();
    }
    static void Activate(){IntPtr h=IntPtr.Zero;foreach(var p in Process.GetProcessesByName("BetterGIProWpf")){if(p.MainWindowHandle!=IntPtr.Zero){h=p.MainWindowHandle;break;}}if(h!=IntPtr.Zero){ShowWindow(h,9);SetForegroundWindow(h);}}
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int n);
}
