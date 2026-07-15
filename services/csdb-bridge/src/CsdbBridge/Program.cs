using CsdbBridge.Configuration;
using CsdbBridge.Models;
using CsdbBridge.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BridgeOptions>(opts =>
{
    // Bind from env vars (compose / .env) with familiar names.
    opts.CsdbBaseUrl = Env("CSDB_BASE_URL", opts.CsdbBaseUrl);
    opts.CsdbUser = Env("CSDB_USER", opts.CsdbUser);
    opts.CsdbPassword = Env("CSDB_PASSWORD", opts.CsdbPassword);
    opts.CsdbUserAgent = Env("CSDB_USER_AGENT", opts.CsdbUserAgent);
    opts.CsdbRateLimitMs = EnvInt("CSDB_RATE_LIMIT_MS", opts.CsdbRateLimitMs);
    opts.LibraryRomsRoot = Env("LIBRARY_ROMS_ROOT", opts.LibraryRomsRoot);
    opts.HvscRoot = Env("HVSC_ROOT", opts.HvscRoot);
    opts.CsdbDataRoot = Env("CSDB_DATA_ROOT", opts.CsdbDataRoot);
    opts.MaxResultsDefault = EnvInt("MAX_RESULTS_DEFAULT", opts.MaxResultsDefault);
    opts.MaxResultsCap = EnvInt("MAX_RESULTS_CAP", opts.MaxResultsCap);
    opts.BridgeApiKey = Env("BRIDGE_API_KEY", Env("CSDB_BRIDGE_API_KEY", opts.BridgeApiKey));
    opts.RommUrl = Env("ROMM_URL", opts.RommUrl);
    opts.RommApiToken = Env("ROMM_API_TOKEN", opts.RommApiToken);
});

builder.Services.AddHttpClient("csdb", (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<BridgeOptions>>().Value;
    client.BaseAddress = new Uri(o.CsdbBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", o.CsdbUserAgent);
    client.Timeout = TimeSpan.FromSeconds(o.CsdbRequestTimeoutS);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = true,
    AllowAutoRedirect = true,
});

builder.Services.AddHttpClient<CsdbClient>((sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<BridgeOptions>>().Value;
    client.BaseAddress = new Uri(o.CsdbBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", o.CsdbUserAgent);
    client.Timeout = TimeSpan.FromSeconds(o.CsdbRequestTimeoutS);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = true,
    AllowAutoRedirect = true,
});

builder.Services.AddSingleton<RssIndexService>();
builder.Services.AddScoped<IngestService>();

builder.WebHost.UseUrls("http://0.0.0.0:8090");

var app = builder.Build();

// Ensure data dirs exist
{
    var o = app.Services.GetRequiredService<IOptions<BridgeOptions>>().Value;
    Directory.CreateDirectory(o.LibraryRomsRoot);
    Directory.CreateDirectory(o.CsdbDataRoot);
    Directory.CreateDirectory(o.ResolvedRssRawDir);
}

app.MapGet("/health", (IOptions<BridgeOptions> options) =>
{
    var o = options.Value;
    return Results.Ok(new
    {
        status = "ok",
        service = "csdb-bridge",
        runtime = "csharp",
        csdb_identity_configured = !string.IsNullOrWhiteSpace(o.CsdbUser) && !string.IsNullOrWhiteSpace(o.CsdbPassword),
        csdb_anonymous_supported = true,
        library_roms_root = o.LibraryRomsRoot,
        hvsc_root = o.HvscRoot,
        csdb_data_root = o.CsdbDataRoot,
    });
});

app.MapGet("/csdb/v1/auth-status", async (HttpRequest req, CsdbClient client, IOptions<BridgeOptions> options) =>
{
    if (!Authorize(req, options.Value))
        return Results.Unauthorized();
    return Results.Ok(await client.EnsureSessionAsync(req.HttpContext.RequestAborted));
});

// Full-catalog CSDb search (live site search) and/or local RSS index.
// source=live (default): full CSDb catalog via /search/ (capped + rate-limited)
// source=index: local RSS recent-window only
// source=both: merge live then index, de-duped by (kind,id)
app.MapGet("/csdb/v1/search", async (
    HttpRequest req,
    CsdbClient client,
    RssIndexService index,
    IOptions<BridgeOptions> options,
    string q,
    string[]? kinds,
    int? limit,
    string? source) =>
{
    if (!Authorize(req, options.Value))
        return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { detail = "q is required" });
    var o = options.Value;
    var kindList = kinds is { Length: > 0 } ? kinds.ToList() : new List<string> { "demo", "crack", "sid" };
    var lim = Math.Min(limit ?? o.MaxResultsDefault, o.MaxResultsCap);
    var src = (source ?? "live").Trim().ToLowerInvariant();
    if (src is not ("live" or "index" or "both"))
        return Results.BadRequest(new { detail = "source must be live, index, or both" });

    var results = new List<object>();
    var seen = new HashSet<(string, int)>();

    if (src is "live" or "both")
    {
        await client.EnsureSessionAsync(req.HttpContext.RequestAborted);
        var hits = await client.SearchAsync(q, kindList, lim, req.HttpContext.RequestAborted);
        foreach (var h in hits)
        {
            if (!seen.Add((h.Kind, h.CsdbId)))
                continue;
            results.Add(new
            {
                kind = h.Kind,
                csdb_id = h.CsdbId,
                title = h.Title,
                csdb_type = h.CsdbType,
                page_url = h.PageUrl,
                source = "live",
            });
        }
    }

    if (src is "index" or "both" && results.Count < lim)
    {
        var rows = index.Search(q, kindList, lim, 0);
        foreach (var row in rows)
        {
            var kind = row.GetValueOrDefault("kind")?.ToString() ?? "other";
            var idObj = row.GetValueOrDefault("entry_id");
            if (idObj is null || !int.TryParse(idObj.ToString(), out var id) || id <= 0)
                continue;
            if (!seen.Add((kind, id)))
                continue;
            results.Add(new
            {
                kind,
                csdb_id = id,
                title = row.GetValueOrDefault("title")?.ToString() ?? "",
                csdb_type = row.GetValueOrDefault("csdb_type")?.ToString() ?? "",
                page_url = row.GetValueOrDefault("link")?.ToString() ?? "",
                source = "index",
                download_url = row.GetValueOrDefault("download_url")?.ToString(),
            });
            if (results.Count >= lim)
                break;
        }
    }

    return Results.Ok(new
    {
        query = q,
        kinds = kindList,
        source = src,
        catalog = src == "index" ? "rss_recent_window" : "csdb_full_catalog",
        count = results.Count,
        auth_mode = client.IdentityConfigured ? "optional_credentials_configured" : "anonymous",
        results,
    });
});

app.MapPost("/csdb/v1/index/refresh", async (
    HttpRequest req,
    RssIndexService index,
    IOptions<BridgeOptions> options,
    IndexRefreshRequest? body) =>
{
    if (!Authorize(req, options.Value))
        return Results.Unauthorized();
    var results = await index.RefreshAsync(body?.FeedKeys, req.HttpContext.RequestAborted);
    return Results.Ok(new
    {
        feeds = results.Select(r => new
        {
            feed_key = r.FeedKey,
            url = r.Url,
            ok = r.Ok,
            item_count = r.ItemCount,
            error = r.Error,
        }),
        stats = index.GetStats(),
    });
});

app.MapGet("/csdb/v1/index/stats", (HttpRequest req, RssIndexService index, IOptions<BridgeOptions> options) =>
{
    if (!Authorize(req, options.Value))
        return Results.Unauthorized();
    var s = index.GetStats();
    return Results.Ok(new
    {
        feeds = s.Feeds,
        items = s.Items,
        by_kind = s.ByKind,
        last_fetched_at = s.LastFetchedAt,
    });
});

app.MapGet("/csdb/v1/index/feeds", (HttpRequest req, RssIndexService index, IOptions<BridgeOptions> options) =>
{
    if (!Authorize(req, options.Value))
        return Results.Unauthorized();
    return Results.Ok(index.ListFeeds());
});

app.MapGet("/csdb/v1/index/search", (
    HttpRequest req,
    RssIndexService index,
    IOptions<BridgeOptions> options,
    string? q,
    string[]? kinds,
    int? limit,
    int? offset) =>
{
    if (!Authorize(req, options.Value))
        return Results.Unauthorized();
    var rows = index.Search(q ?? "", kinds, limit ?? 25, offset ?? 0);
    return Results.Ok(new { query = q ?? "", count = rows.Count, results = rows });
});

app.MapPost("/csdb/v1/ingest", async (
    HttpRequest req,
    IngestRequest body,
    CsdbClient client,
    IngestService ingest,
    IOptions<BridgeOptions> options) =>
{
    if (!Authorize(req, options.Value))
        return Results.Unauthorized();
    if (body.Items is null || body.Items.Count is < 1 or > 20)
        return Results.BadRequest(new { detail = "items must contain 1..20 entries" });

    var o = options.Value;
    var baseUrl = o.CsdbBaseUrl.TrimEnd('/');
    var hits = body.Items.Select(i => new SearchHit(
        i.Kind,
        i.CsdbId,
        $"csdb-{i.CsdbId}",
        i.Kind,
        i.Kind.Equals("sid", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/sid/?id={i.CsdbId}"
            : $"{baseUrl}/release/?id={i.CsdbId}")).ToList();

    await client.EnsureSessionAsync(req.HttpContext.RequestAborted);
    var jobId = $"job-{Guid.NewGuid():N}"[..16];
    var result = await ingest.IngestAsync(hits, jobId, body.Force, req.HttpContext.RequestAborted);
    return Results.Ok(new { job_id = result.JobId, requested = body.Items.Count, ingest = result });
});

app.MapGet("/csdb/v1/releases/{csdbId:int}", async (
    HttpRequest req, int csdbId, CsdbClient client, IOptions<BridgeOptions> options) =>
{
    if (!Authorize(req, options.Value))
        return Results.Unauthorized();
    await client.EnsureSessionAsync(req.HttpContext.RequestAborted);
    var rel = await client.GetReleaseAsync(csdbId, 2, req.HttpContext.RequestAborted);
    return Results.Ok(new
    {
        csdb_id = rel.CsdbId,
        name = rel.Name,
        csdb_type = rel.CsdbType,
        screenshot = rel.Screenshot,
        download_links = rel.DownloadLinks.Select(d => new
        {
            url = d.Url,
            counter_url = d.CounterUrl,
            status = d.Status,
            downloads = d.Downloads,
        }),
    });
});

app.MapGet("/csdb/v1/sids/{csdbId:int}", async (
    HttpRequest req, int csdbId, CsdbClient client, IOptions<BridgeOptions> options) =>
{
    if (!Authorize(req, options.Value))
        return Results.Unauthorized();
    await client.EnsureSessionAsync(req.HttpContext.RequestAborted);
    var sid = await client.GetSidAsync(csdbId, 1, req.HttpContext.RequestAborted);
    return Results.Ok(new
    {
        csdb_id = sid.CsdbId,
        name = sid.Name,
        hvsc_path = sid.HvscPath,
        author = sid.Author,
    });
});

app.Run();

static bool Authorize(HttpRequest req, BridgeOptions options)
{
    var expected = (options.BridgeApiKey ?? "").Trim();
    if (expected.Length == 0)
        return true;
    if (!req.Headers.TryGetValue("X-Api-Key", out var key))
        return false;
    return string.Equals(key.ToString().Trim(), expected, StringComparison.Ordinal);
}

static string Env(string name, string fallback)
    => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

static int EnvInt(string name, int fallback)
    => int.TryParse(Environment.GetEnvironmentVariable(name), out var n) ? n : fallback;

public sealed record IndexRefreshRequest(string[]? FeedKeys);
public sealed record IngestItemDto(string Kind, int CsdbId);
public sealed record IngestRequest(List<IngestItemDto> Items, bool Force = false);

// Expose for WebApplicationFactory tests if needed later
public partial class Program;
