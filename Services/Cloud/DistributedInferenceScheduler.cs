using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace BetterGIProWpf.Services.Cloud;

public class WorkerNode
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public int Priority { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime LastHeartbeat { get; set; } = DateTime.MinValue;
}

public class DistributedInferenceScheduler
{
    private readonly List<WorkerNode> _workers = new();
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly object _lock = new();

    public void RegisterWorker(string id, string url, int priority = 0)
    {
        lock (_lock) { var e = _workers.Find(w => w.Id == id); if (e != null) { e.Url = url; e.Priority = priority; } else _workers.Add(new WorkerNode { Id = id, Url = url, Priority = priority }); }
    }

    public WorkerNode? SelectWorker()
    {
        lock (_lock) { return _workers.Where(w => w.IsAvailable).OrderByDescending(w => w.Priority).ThenBy(w => w.LastHeartbeat).FirstOrDefault(); }
    }

    public async Task<string?> DispatchAsync(string workerUrl, string endpoint, string payload)
    {
        try { var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json"); var resp = await _http.PostAsync(workerUrl + endpoint, content); if (!resp.IsSuccessStatusCode) return null; return await resp.Content.ReadAsStringAsync(); }
        catch (Exception ex) { AppLogger.Error($"[Distributed] dispatch to {workerUrl} failed", ex); return null; }
    }

    public List<WorkerNode> GetWorkers() { lock (_lock) return new List<WorkerNode>(_workers); }
}
