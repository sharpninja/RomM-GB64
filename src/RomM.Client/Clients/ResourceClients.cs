using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using RomM.Client.Json;
using RomM.Client.Models;

namespace RomM.Client;

public interface IRomMSystemClient
{
    Task<HeartbeatResponse> GetHeartbeatAsync(CancellationToken cancellationToken = default);
}

public interface IRomMPlatformsClient
{
    Task<IReadOnlyList<PlatformSchema>> ListAsync(CancellationToken cancellationToken = default);
    Task<PlatformSchema> GetAsync(int id, CancellationToken cancellationToken = default);
}

public interface IRomMRomsClient
{
    Task<RomPage> ListAsync(RomListQuery? query = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<SimpleRomSchema> EnumerateAsync(RomListQuery? query = null, CancellationToken cancellationToken = default);
    Task<DetailedRomSchema> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<Stream> DownloadContentAsync(int id, string fileName, CancellationToken cancellationToken = default);
}

public interface IRomMTasksClient
{
    Task<IReadOnlyList<TaskInfo>> ListAsync(CancellationToken cancellationToken = default);
    Task<TaskExecutionResponse> RunAsync(string taskName, CancellationToken cancellationToken = default);
    Task<TaskStatusResponse> GetStatusAsync(string taskId, CancellationToken cancellationToken = default);
    Task WaitAsync(string taskId, TimeSpan pollInterval, TimeSpan timeout, CancellationToken cancellationToken = default);
    Task ScanLibraryAsync(CancellationToken cancellationToken = default);
}

internal sealed class RomMSystemClient(IRomMTransport transport) : IRomMSystemClient
{
    public Task<HeartbeatResponse> GetHeartbeatAsync(CancellationToken cancellationToken = default) =>
        transport.GetJsonAsync<HeartbeatResponse>("api/heartbeat", cancellationToken);
}

internal sealed class RomMPlatformsClient(IRomMTransport transport) : IRomMPlatformsClient
{
    public async Task<IReadOnlyList<PlatformSchema>> ListAsync(CancellationToken cancellationToken = default)
    {
        var list = await transport.GetJsonAsync<List<PlatformSchema>>("api/platforms", cancellationToken)
            .ConfigureAwait(false);
        return list;
    }

    public Task<PlatformSchema> GetAsync(int id, CancellationToken cancellationToken = default) =>
        transport.GetJsonAsync<PlatformSchema>($"api/platforms/{id}", cancellationToken);
}

internal sealed class RomMRomsClient(IRomMTransport transport) : IRomMRomsClient
{
    public Task<RomPage> ListAsync(RomListQuery? query = null, CancellationToken cancellationToken = default)
    {
        query ??= new RomListQuery();
        var qs = query.ToQueryString();
        var url = string.IsNullOrEmpty(qs) ? "api/roms" : $"api/roms?{qs}";
        return transport.GetJsonAsync<RomPage>(url, cancellationToken);
    }

    public async IAsyncEnumerable<SimpleRomSchema> EnumerateAsync(
        RomListQuery? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        query ??= new RomListQuery();
        var limit = query.Limit is > 0 ? query.Limit.Value : 100;
        var offset = query.Offset is >= 0 ? query.Offset.Value : 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageQuery = new RomListQuery
            {
                SearchTerm = query.SearchTerm,
                PlatformIds = query.PlatformIds,
                Limit = limit,
                Offset = offset,
                OrderBy = query.OrderBy,
                OrderDir = query.OrderDir,
                Matched = query.Matched,
                Favorite = query.Favorite,
                Missing = query.Missing,
                WithFiles = query.WithFiles,
            };
            var page = await ListAsync(pageQuery, cancellationToken).ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                yield break;
            }

            foreach (var item in page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }

            offset += page.Items.Count;
            if (page.Items.Count < limit)
            {
                yield break;
            }

            if (page.Total > 0 && offset >= page.Total)
            {
                yield break;
            }
        }
    }

    public Task<DetailedRomSchema> GetAsync(int id, CancellationToken cancellationToken = default) =>
        transport.GetJsonAsync<DetailedRomSchema>($"api/roms/{id}", cancellationToken);

    public async Task<Stream> DownloadContentAsync(int id, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var encoded = Uri.EscapeDataString(fileName);
        var response = await transport.SendAsync(
                HttpMethod.Get,
                $"api/roms/{id}/content/{encoded}",
                completion: HttpCompletionOption.ResponseHeadersRead,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class RomMTasksClient(IRomMTransport transport) : IRomMTasksClient
{
    public async Task<IReadOnlyList<TaskInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        // API may return array or object wrapper; try array first then dictionary-like.
        try
        {
            return await transport.GetJsonAsync<List<TaskInfo>>("api/tasks", cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            var status = await transport.GetJsonAsync<Dictionary<string, TaskInfo>>("api/tasks/status", cancellationToken)
                .ConfigureAwait(false);
            return status.Select(kv =>
            {
                var t = kv.Value;
                t.Name ??= kv.Key;
                return t;
            }).ToList();
        }
    }

    public Task<TaskExecutionResponse> RunAsync(string taskName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);
        return transport.SendJsonAsync<TaskExecutionResponse>(
            HttpMethod.Post,
            $"api/tasks/run/{Uri.EscapeDataString(taskName)}",
            content: null,
            cancellationToken);
    }

    public async Task<TaskStatusResponse> GetStatusAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        try
        {
            return await transport.GetJsonAsync<TaskStatusResponse>($"api/tasks/{Uri.EscapeDataString(taskId)}", cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Some builds expose aggregate status only.
            var all = await transport.GetJsonAsync<Dictionary<string, TaskStatusResponse>>("api/tasks/status", cancellationToken)
                .ConfigureAwait(false);
            if (all.TryGetValue(taskId, out var s))
            {
                return s;
            }

            throw;
        }
    }

    public async Task WaitAsync(
        string taskId,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (pollInterval <= TimeSpan.Zero)
        {
            pollInterval = TimeSpan.FromSeconds(1);
        }

        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await GetStatusAsync(taskId, cancellationToken).ConfigureAwait(false);
            if (status.IsTerminal)
            {
                if (!status.IsSuccess)
                {
                    throw new RomMApiException(500, $"/api/tasks/{taskId}", $"Task ended with status '{status.Status ?? status.State}'.");
                }

                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for task '{taskId}'.");
            }

            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ScanLibraryAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await ListAsync(cancellationToken).ConfigureAwait(false);
        var scanName = tasks
            .Select(t => t.Name)
            .FirstOrDefault(n => n is not null && n.Contains("scan", StringComparison.OrdinalIgnoreCase))
            ?? "scan";

        var execution = await RunAsync(scanName, cancellationToken).ConfigureAwait(false);
        string taskId;
        try
        {
            taskId = execution.ResolveTaskId();
        }
        catch
        {
            // Fire-and-forget style APIs may not return an id; treat as completed.
            return;
        }

        await WaitAsync(taskId, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(30), cancellationToken)
            .ConfigureAwait(false);
    }
}
