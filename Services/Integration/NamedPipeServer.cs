using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace BetterGIProWpf.Services.Integration;
public class NamedPipeServer : IDisposable
{
    public const string PipeName = "bettergipro-wpf";
    private readonly CancellationTokenSource _cts = new();
    public event Func<string, Task<string>>? OnCommand;
    public void Start() => _ = Task.Run(AcceptLoopAsync);
    private async Task AcceptLoopAsync() { while (!_cts.IsCancellationRequested) { try { var s = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous); await s.WaitForConnectionAsync(_cts.Token); _ = Task.Run(() => HandleClientAsync(s)); } catch { await Task.Delay(500); } } }
    private async Task HandleClientAsync(NamedPipeServerStream s)
    {
        using var gate = new SemaphoreSlim(1, 1);
        try { using var r = new StreamReader(s); using var w = new StreamWriter(s) { AutoFlush = true }; while (!_cts.IsCancellationRequested && s.IsConnected) { var line = await r.ReadLineAsync(); if (line == null) break; var resp = "{\"ok\":true}"; try { await gate.WaitAsync(_cts.Token); try { var node = JsonNode.Parse(line); var cmd = node?["cmd"]?.GetValue<string>() ?? ""; if (OnCommand != null) resp = await OnCommand(cmd); } finally { gate.Release(); } } catch (Exception ex) { resp = "{\"ok\":false,\"error\":" + JsonSerializer.Serialize(ex.Message) + "}"; } await w.WriteLineAsync(resp); } }
        catch { } finally { s.Dispose(); }
    }
    public void Dispose() => _cts.Cancel();
}
public class NamedPipeClient
{
    public static async Task<string> SendAsync(string command, int timeoutMs = 5000, int maxRetries = 3)
    {
        var last = "";
        for (int attempt = 0; attempt <= maxRetries; attempt++) try { using var cts = new CancellationTokenSource(timeoutMs); using var c = new NamedPipeClientStream(".", NamedPipeServer.PipeName, PipeDirection.InOut); await c.ConnectAsync(cts.Token); using var r = new StreamReader(c); using var w = new StreamWriter(c) { AutoFlush = true }; await w.WriteLineAsync(command); return await r.ReadLineAsync() ?? ""; }
        catch (TimeoutException) { last = "timeout"; break; }
        catch (IOException) { last = "pipe-broken"; if (attempt < maxRetries) await Task.Delay(300*(attempt+1)); }
        return last;
    }
}
