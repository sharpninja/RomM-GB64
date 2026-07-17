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
    opts.RommTokenShareEnabled = EnvBool("ROMM_TOKEN_SHARE_ENABLED", opts.RommTokenShareEnabled);
    opts.RommTokenShareCidrs = Env("ROMM_TOKEN_SHARE_CIDRS", opts.RommTokenShareCidrs);
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

// Admin RomM client for user provisioning: base = RomM, bearer = the admin ROMM_API_TOKEN.
builder.Services.AddHttpClient("romm-admin", (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<BridgeOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(o.RommUrl) ? "http://romm:8080" : o.RommUrl;
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(o.RommApiToken))
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", o.RommApiToken);
});

// Login RomM client for the per-user password grant: base = RomM, no bearer (this IS the login).
builder.Services.AddHttpClient("romm-login", (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<BridgeOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(o.RommUrl) ? "http://romm:8080" : o.RommUrl;
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
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

// Per-Xbox-user RomM provisioning + connection sharing. A same-subnet client passes its Xbox user id;
// the bridge ensures a RomM user exists for it (creating it with the shared token as its password), then
// logs in AS that user to mint a per-user access token, and returns { url, token } - so the client gets a
// token scoped to its own user and NEVER holds the admin ROMM_API_TOKEN. No pairing code, no typed token.
// Gated ONLY by the subnet check (the bootstrapping client has no bridge key yet) plus
// ROMM_TOKEN_SHARE_ENABLED + a configured admin ROMM_API_TOKEN.
//
// The admin token stays server-side; still scope ROMM_TOKEN_SHARE_CIDRS tightly on shared networks (any
// same-subnet caller can provision + obtain a token for an arbitrary user id). The returned access token
// is short-lived (RomM /api/token expires); a client re-requests this endpoint when its token expires.
app.MapGet("/romm/v1/connection", async (
    HttpRequest req,
    IOptions<BridgeOptions> options,
    IHttpClientFactory httpFactory,
    string? user_id) =>
{
    var o = options.Value;
    if (!o.RommTokenShareEnabled)
        return Results.NotFound(new { detail = "romm token sharing disabled" });
    if (string.IsNullOrWhiteSpace(o.RommApiToken))
        return Results.NotFound(new { detail = "no ROMM_API_TOKEN configured" });

    var forwardedFor = req.Headers.TryGetValue("X-Forwarded-For", out var xff) ? xff.ToString() : null;
    if (!RommShareGate.IsAllowed(req.HttpContext.Connection.RemoteIpAddress, forwardedFor, o.ResolvedTokenShareCidrs))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    if (string.IsNullOrWhiteSpace(user_id))
        return Results.BadRequest(new { detail = "user_id is required" });

    // The raw client id (Xbox NonRoamableId) is too long / character-unsafe to be a RomM username; map it
    // to a stable, RomM-safe form and use that same username for BOTH provisioning and login.
    string rommUser = RommUsername.FromClientId(user_id);

    try
    {
        // 1. Ensure the RomM user exists (password = the shared admin token).
        var provisioner = new RommUserProvisioner(httpFactory.CreateClient("romm-admin"));
        await provisioner.EnsureUserAsync(rommUser, o.RommApiToken, req.HttpContext.RequestAborted);

        // 2. Log in AS that user to mint a per-user access token (the client never sees the admin token).
        var tokenClient = new RommTokenClient(httpFactory.CreateClient("romm-login"));
        string accessToken = await tokenClient.LoginAsync(rommUser, o.RommApiToken, req.HttpContext.RequestAborted);

        return Results.Ok(new { url = o.RommUrl, token = accessToken });
    }
    catch (Exception ex)
    {
        return Results.Problem($"failed to provision RomM user: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
    }
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

static bool EnvBool(string name, bool fallback)
    => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v
        ? v.Trim() is "1" or "true" or "TRUE" or "True" or "yes" or "on"
        : fallback;

public sealed record IndexRefreshRequest(string[]? FeedKeys);
public sealed record IngestItemDto(string Kind, int CsdbId);
public sealed record IngestRequest(List<IngestItemDto> Items, bool Force = false);

// Expose for WebApplicationFactory tests if needed later
public partial class Program;
