using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CsdbBridge.Configuration;
using CsdbBridge.Models;
using Microsoft.Extensions.Options;

namespace CsdbBridge.Services;

public sealed partial class CsdbClient : IDisposable
{
    private static readonly Regex ReleaseRe = ReleasePattern();
    private static readonly Regex SidRe = SidPattern();

    private readonly HttpClient _http;
    private readonly BridgeOptions _options;
    private readonly object _throttleLock = new();
    private DateTime _lastRequestUtc = DateTime.MinValue;
    private bool _loggedIn;

    public CsdbClient(HttpClient http, IOptions<BridgeOptions> options)
    {
        _http = http;
        _options = options.Value;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(_options.CsdbBaseUrl.TrimEnd('/') + "/");
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _options.CsdbUserAgent);
        _http.Timeout = TimeSpan.FromSeconds(_options.CsdbRequestTimeoutS);
    }

    public bool IdentityConfigured =>
        !string.IsNullOrWhiteSpace(_options.CsdbUser) && !string.IsNullOrWhiteSpace(_options.CsdbPassword);

    public bool IdentityActive => _loggedIn;

    public async Task<IReadOnlyDictionary<string, object>> EnsureSessionAsync(CancellationToken ct = default)
    {
        await GetAsync("/", ct).ConfigureAwait(false);
        if (IdentityConfigured && !_loggedIn)
        {
            try
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["loginuser"] = _options.CsdbUser,
                    ["loginpass"] = _options.CsdbPassword,
                    ["login"] = "Login",
                });
                await SendAsync(HttpMethod.Post, "login.php", content, ct).ConfigureAwait(false);
                _loggedIn = true;
            }
            catch
            {
                _loggedIn = false;
            }
        }

        return new Dictionary<string, object>
        {
            ["anonymous_ok"] = true,
            ["identity_configured"] = IdentityConfigured,
            ["identity_active"] = _loggedIn,
        };
    }

    /// <summary>
    /// Full-catalog CSDb search (not RSS-limited). Uses site search endpoints
    /// that query the entire database; results are still rate-limited and capped.
    /// </summary>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query,
        IEnumerable<string>? kinds = null,
        int limit = 25,
        CancellationToken ct = default)
    {
        var q = query.Trim();
        if (q.Length == 0)
            return Array.Empty<SearchHit>();

        var kindSet = new HashSet<string>(
            (kinds ?? new[] { "demo", "crack", "sid" }).Select(k => k.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
        var hits = new List<SearchHit>();
        var seen = new HashSet<(string Kind, int Id)>();

        // Full release catalog (demos, cracks, tools, graphics, …) then filter by kind.
        if (kindSet.Overlaps(new[] { "demo", "crack", "other" }))
        {
            await CollectReleaseHitsAsync(
                $"search/?seinsel=releases&search={Uri.EscapeDataString(q)}",
                kindSet, hits, seen, limit, ct).ConfigureAwait(false);
        }

        // Full SID catalog search
        if (kindSet.Contains("sid") && hits.Count < limit)
        {
            await CollectSidHitsAsync(
                $"search/?seinsel=sids&search={Uri.EscapeDataString(q)}",
                hits, seen, limit, ct).ConfigureAwait(false);
        }

        return hits;
    }

    private async Task CollectReleaseHitsAsync(
        string path,
        HashSet<string> kindSet,
        List<SearchHit> hits,
        HashSet<(string Kind, int Id)> seen,
        int limit,
        CancellationToken ct)
    {
        var html = await GetStringAsync(path, ct).ConfigureAwait(false);
        foreach (Match m in ReleaseRe.Matches(html))
        {
            var id = int.Parse(m.Groups[2].Value);
            var title = Clean(m.Groups[3].Value);
            var csdbType = Clean(m.Groups[4].Value);
            var kind = CsdbClassifier.ClassifyReleaseType(csdbType);
            if (!kindSet.Contains(kind))
                continue;
            if (!seen.Add((kind, id)))
                continue;
            hits.Add(new SearchHit(
                kind, id, title, csdbType,
                new Uri(_http.BaseAddress!, m.Groups[1].Value.TrimStart('/')).ToString()));
            if (hits.Count >= limit)
                return;
        }
    }

    private async Task CollectSidHitsAsync(
        string path,
        List<SearchHit> hits,
        HashSet<(string Kind, int Id)> seen,
        int limit,
        CancellationToken ct)
    {
        var html = await GetStringAsync(path, ct).ConfigureAwait(false);
        foreach (Match m in SidRe.Matches(html))
        {
            var id = int.Parse(m.Groups[2].Value);
            if (!seen.Add(("sid", id)))
                continue;
            var title = Clean(m.Groups[3].Value);
            hits.Add(new SearchHit(
                "sid", id, title, "SID",
                new Uri(_http.BaseAddress!, m.Groups[1].Value.TrimStart('/')).ToString()));
            if (hits.Count >= limit)
                return;
        }
    }

    public async Task<ReleaseDetail> GetReleaseAsync(int csdbId, int depth = 2, CancellationToken ct = default)
    {
        var xml = await GetStringAsync($"webservice/?type=release&id={csdbId}&depth={depth}", ct)
            .ConfigureAwait(false);
        var doc = XDocument.Parse(xml);
        var rel = doc.Root?.Element("Release")
            ?? throw new InvalidOperationException($"CSDb release {csdbId} not found");
        var name = (rel.Element("Name")?.Value ?? $"release-{csdbId}").Trim();
        var csdbType = (rel.Element("Type")?.Value ?? "").Trim();
        var screenshot = rel.Element("ScreenShot")?.Value;
        var links = new List<DownloadLink>();
        foreach (var dl in rel.Element("DownloadLinks")?.Elements("DownloadLink") ?? Enumerable.Empty<XElement>())
        {
            var link = (dl.Element("Link")?.Value ?? "").Trim();
            if (link.Length == 0)
                continue;
            int? downloads = int.TryParse(dl.Element("Downloads")?.Value, out var d) ? d : null;
            links.Add(new DownloadLink(
                link,
                dl.Element("CounterLink")?.Value,
                (dl.Element("Status")?.Value ?? "Ok").Trim(),
                downloads));
        }

        return new ReleaseDetail(csdbId, name, csdbType, links, screenshot);
    }

    public async Task<SidDetail> GetSidAsync(int csdbId, int depth = 1, CancellationToken ct = default)
    {
        var xml = await GetStringAsync($"webservice/?type=sid&id={csdbId}&depth={depth}", ct)
            .ConfigureAwait(false);
        var doc = XDocument.Parse(xml);
        var sid = doc.Root?.Element("SID")
            ?? throw new InvalidOperationException($"CSDb SID {csdbId} not found");
        var hvsc = sid.Element("HVSCPath")?.Value?.Trim().Replace('\\', '/');
        if (!string.IsNullOrEmpty(hvsc) && !hvsc.StartsWith('/'))
            hvsc = "/" + hvsc;
        return new SidDetail(
            csdbId,
            (sid.Element("Name")?.Value ?? $"sid-{csdbId}").Trim(),
            hvsc,
            sid.Element("Author")?.Value);
    }

    public async Task<(byte[] Data, string? FileName)> DownloadBytesAsync(string url, CancellationToken ct = default)
    {
        await ThrottleAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync(MakeUri(url), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        string? fileName = null;
        var cd = resp.Content.Headers.ContentDisposition;
        if (cd?.FileName is not null)
            fileName = cd.FileName.Trim('"');
        return (data, fileName);
    }

    public void Dispose()
    {
        // HttpClient owned by IHttpClientFactory when registered that way;
        // safe no-op if factory-managed (do not dispose shared client).
    }

    private async Task<string> GetStringAsync(string path, CancellationToken ct)
    {
        using var resp = await GetAsync(path, ct).ConfigureAwait(false);
        return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct)
        => await SendAsync(HttpMethod.Get, path, null, ct).ConfigureAwait(false);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        await ThrottleAsync(ct).ConfigureAwait(false);
        using var req = new HttpRequestMessage(method, MakeUri(path)) { Content = content };
        var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return resp;
    }

    private Uri MakeUri(string pathOrUrl)
    {
        if (pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return new Uri(pathOrUrl);
        return new Uri(_http.BaseAddress!, pathOrUrl.TrimStart('/'));
    }

    private async Task ThrottleAsync(CancellationToken ct)
    {
        if (_options.CsdbRateLimitMs <= 0)
            return;
        TimeSpan wait;
        lock (_throttleLock)
        {
            var elapsed = DateTime.UtcNow - _lastRequestUtc;
            var need = TimeSpan.FromMilliseconds(_options.CsdbRateLimitMs) - elapsed;
            wait = need > TimeSpan.Zero ? need : TimeSpan.Zero;
            if (wait <= TimeSpan.Zero)
                _lastRequestUtc = DateTime.UtcNow;
        }

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, ct).ConfigureAwait(false);
            lock (_throttleLock)
                _lastRequestUtc = DateTime.UtcNow;
        }
    }

    private static string Clean(string s)
        => Regex.Replace(WebUtility.HtmlDecode(s) ?? s, @"\s+", " ").Trim();

    [GeneratedRegex(@"href=""(/release/\?id=(\d+))""[^>]*>([^<]+)</a>\s*\(([^)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ReleasePattern();

    [GeneratedRegex(@"href=""(/sid/\?id=(\d+))""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex SidPattern();
}
