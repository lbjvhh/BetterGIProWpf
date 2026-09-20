using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using BetterGIProWpf.Services.Integration;

namespace BetterGIProWpf.Services.Remote;

public class RemoteControlServer
{
    private readonly int _port;
    private HttpListener? _listener;
    private Thread? _thread;
    private volatile bool _running;

    public event Action? OnStartTask;
    public event Action? OnStopTask;
    public event Action? OnEmergencyStop;
    public Func<string>? StatusProvider;
    public Func<Bitmap?>? ScreenshotProvider;

    public RemoteControlServer(int port = 5090) { _port = port; }

    public void Start()
    {
        if (_running) return;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{_port}/");
        try { _listener.Start(); }
        catch { _listener = new HttpListener(); _listener.Prefixes.Add($"http://localhost:{_port}/"); _listener.Start(); }
        _running = true;
        _thread = new Thread(Loop) { IsBackground = true, Name = "RemoteControl" };
        _thread.Start();
        AppLogger.Info($"[Remote] started on http://*:{_port}/");
    }

    public void Stop() { _running = false; try { _listener?.Stop(); } catch { } try { _listener?.Close(); } catch { } }

    private void Loop() { while (_running) { try { var ctx = _listener!.GetContext(); Handle(ctx); } catch { if (_running) Thread.Sleep(100); } } }

    private void Handle(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            switch (path)
            {
                case "/": WriteHtml(ctx, MobilePage()); break;
                case "/status": WriteJson(ctx, StatusProvider?.Invoke() ?? "{}"); break;
                case "/screenshot":
                    var bmp = ScreenshotProvider?.Invoke();
                    if (bmp != null) { using var ms = new MemoryStream(); bmp.Save(ms, ImageFormat.Jpeg); ctx.Response.ContentType = "image/jpeg"; ctx.Response.OutputStream.Write(ms.ToArray(), 0, (int)ms.Length); ctx.Response.OutputStream.Close(); }
                    else { ctx.Response.StatusCode = 404; ctx.Response.Close(); }
                    break;
                case "/action":
                    var action = ctx.Request.QueryString["a"] ?? "";
                    switch (action)
                    {
                        case "start": OnStartTask?.Invoke(); WriteJson(ctx, "{\"ok\":true,\"a\":\"start\"}"); break;
                        case "stop": OnStopTask?.Invoke(); WriteJson(ctx, "{\"ok\":true,\"a\":\"stop\"}"); break;
                        case "emergency": OnEmergencyStop?.Invoke(); WriteJson(ctx, "{\"ok\":true,\"a\":\"emergency\"}"); break;
                        default: WriteJson(ctx, "{\"ok\":false,\"e\":\"unknown action\"}"); break;
                    }
                    break;
                default: ctx.Response.StatusCode = 404; ctx.Response.Close(); break;
            }
        }
        catch (Exception ex) { AppLogger.Error("[Remote] handle error", ex); }
    }

    private static void WriteJson(HttpListenerContext ctx, string json)
    {
        var buf = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = buf.Length;
        ctx.Response.OutputStream.Write(buf, 0, buf.Length);
        ctx.Response.OutputStream.Close();
    }

    private static void WriteHtml(HttpListenerContext ctx, string html)
    {
        var buf = Encoding.UTF8.GetBytes(html);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = buf.Length;
        ctx.Response.OutputStream.Write(buf, 0, buf.Length);
        ctx.Response.OutputStream.Close();
    }

    private static string MobilePage()
    {
        return @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<title>BetterGIPro 远程</title>
<style>
body{font-family:-apple-system,sans-serif;background:#1a1a2e;color:#eee;padding:16px;max-width:600px;margin:auto}
h1{font-size:18px;color:#00d4ff}
.btn{display:block;width:100%;padding:14px;margin:10px 0;border:none;border-radius:10px;font-size:16px;cursor:pointer}
.start{background:#2ecc71;color:#fff}
.stop{background:#e74c3c;color:#fff}
.emerg{background:#c0392b;color:#fff;font-weight:bold}
.status{background:#16213e;padding:12px;border-radius:8px;margin:10px 0;font-size:13px;white-space:pre-wrap}
img{width:100%;border-radius:8px;margin:10px 0}
</style></head>
<body>
<h1>BetterGIPro 远程控制</h1>
<div class='status' id='s'>加载状态...</div>
<img src='/screenshot' id='sc' onerror="this.style.display='none'" />
<button class='btn start' onclick="act('start')">开始任务</button>
<button class='btn stop' onclick="act('stop')">停止任务</button>
<button class='btn emerg' onclick="act('emergency')">紧急停机</button>
<script>
function act(a){fetch('/action?a='+a).then(r=>r.json()).then(()=>refresh())}
function refresh(){fetch('/status').then(r=>r.text()).then(t=>{document.getElementById('s').textContent=t})}
setInterval(refresh,3000);refresh();
</script>
</body></html>";
    }
}
