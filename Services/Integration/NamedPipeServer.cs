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

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cts.Token);
                _ = Task.Run(() => HandleClientAsync(server));
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(500); }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream s)
    {
        using var actionGate = new SemaphoreSlim(1, 1);
        try
        {
            using var reader = new StreamReader(s);
            using var writer = new StreamWriter(s) { AutoFlush = true };
            while (!_cts.IsCancellationRequested && s.IsConnected)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;
                var resp = "{\"ok\":true}";
                try
                {
                    await actionGate.WaitAsync(_cts.Token);
                    try
                    {
                        var node = JsonNode.Parse(line);
                        var cmd = node?["cmd"]?.GetValue<string>() ?? "";
                        if (OnCommand != null) resp = await OnCommand(cmd);
                    }
                    finally { actionGate.Release(); }
                }
                catch (Exception ex) { resp = "{\"ok\":false,\"error\":" + JsonSerializer.Serialize(ex.Message) + "}"; }
                await writer.WriteLineAsync(resp);
            }
        }
        catch { }
        finally { s.Dispose(); }
    }

    public void Dispose() => _cts.Cancel();
}

public static class NamedPipeClient
{
    public static async Task<string> SendAsync(string command, int timeoutMs = 5000, int maxRetries = 3)
    {
        var last = "";
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                using var client = new NamedPipeClientStream(".", NamedPipeServer.PipeName, PipeDirection.InOut);
                await client.ConnectAsync(cts.Token);
                using var reader = new StreamReader(client);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                await writer.WriteLineAsync(command);
                last = await reader.ReadLineAsync() ?? "";
                return last;
            }
            catch (TimeoutException) { last = "{\"ok\":false,\"error\":\"timeout\"}"; break; }
            catch (IOException) { last = "{\"ok\":false,\"error\":\"pipe-broken\"}"; if (attempt < maxRetries) await Task.Delay(300 * (attempt + 1)); }
            catch (OperationCanceledException) { last = "{\"ok\":false,\"error\":\"canceled\"}"; break; }
        }
        return last;
    }
}
