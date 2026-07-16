using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RomM.Client.Csdb;

public interface ICsdbClient
{
    Task<IReadOnlyList<CsdbSearchHit>> SearchAsync(CsdbSearchRequest request, CancellationToken cancellationToken = default);
    Task<CsdbReleaseDetail> GetReleaseAsync(int csdbId, CancellationToken cancellationToken = default);
    Task<CsdbSidDetail> GetSidAsync(int csdbId, CancellationToken cancellationToken = default);
    Task<(byte[] Data, string? FileName)> DownloadBytesAsync(string url, CancellationToken cancellationToken = default);
}

public sealed partial class CsdbClient : ICsdbClient, IDisposable
{
    private static readonly Regex ReleaseRe = ReleasePattern();
    private static readonly Regex SidRe = SidPattern();

    private readonly HttpClient _http;
    private readonly CsdbLibraryOptions _options;
    private readonly TimeProvider _clock;
    private readonly object _throttleLock = new();
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;
    private readonly bool _ownsHttp;

    public CsdbClient(HttpClient http, CsdbLibraryOptions options, TimeProvider? clock = null, bool ownsHttp = false)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _clock = clock ?? TimeProvider.System;
        _ownsHttp = ownsHttp;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://csdb.dk/");
        }

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _options.UserAgent);
        }
    }

    public static CsdbClient Create(CsdbLibraryOptions options, HttpMessageHandler? handler = null)
    {
        options.Validate();
        var http = new HttpClient(handler ?? new HttpClientHandler())
        {
            BaseAddress = new Uri("https://csdb.dk/"),
        };
        return new CsdbClient(http, options, ownsHttp: true);
    }

    public async Task<IReadOnlyList<CsdbSearchHit>> SearchAsync(
        CsdbSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var q = request.Query?.Trim() ?? "";
        if (q.Length == 0)
        {
            return Array.Empty<CsdbSearchHit>();
        }

        var limit = _options.ClampSearchLimit(request.Limit);
        var kinds = request.Kinds is { Count: > 0 }
            ? request.Kinds.ToHashSet()
            : new HashSet<CsdbKind> { CsdbKind.Demo, CsdbKind.Crack, CsdbKind.Sid };

        var hits = new List<CsdbSearchHit>();
        var seen = new HashSet<(CsdbKind, int)>();

        if (kinds.Overlaps(new[] { CsdbKind.Demo, CsdbKind.Crack, CsdbKind.Other }))
        {
            await CollectReleaseHitsAsync(
                $"search/?seinsel=releases&search={Uri.EscapeDataString(q)}",
                kinds, hits, seen, limit, cancellationToken).ConfigureAwait(false);
        }

        if (kinds.Contains(CsdbKind.Sid) && hits.Count < limit)
        {
            await CollectSidHitsAsync(
                $"search/?seinsel=sids&search={Uri.EscapeDataString(q)}",
                hits, seen, limit, cancellationToken).ConfigureAwait(false);
        }

        return hits;
    }

    public async Task<CsdbReleaseDetail> GetReleaseAsync(int csdbId, CancellationToken cancellationToken = default)
    {
        var xml = await GetStringAsync($"webservice/?type=release&id={csdbId}&depth=2", cancellationToken)
            .ConfigureAwait(false);
        var doc = XDocument.Parse(xml);
        var rel = doc.Root?.Element("Release")
            ?? throw new CsdbException($"CSDb release {csdbId} not found");
        var name = (rel.Element("Name")?.Value ?? $"release-{csdbId}").Trim();
        var csdbType = (rel.Element("Type")?.Value ?? "").Trim();
        var links = new List<CsdbDownloadLink>();
        foreach (var dl in rel.Element("DownloadLinks")?.Elements("DownloadLink") ?? Enumerable.Empty<XElement>())
        {
            var link = (dl.Element("Link")?.Value ?? "").Trim();
            if (link.Length == 0)
            {
                continue;
            }

            int? downloads = int.TryParse(dl.Element("Downloads")?.Value, out var d) ? d : null;
            links.Add(new CsdbDownloadLink(
                link,
                (dl.Element("Status")?.Value ?? "Ok").Trim(),
                downloads));
        }

        return new CsdbReleaseDetail(csdbId, name, csdbType, links, rel.Element("ScreenShot")?.Value);
    }

    public async Task<CsdbSidDetail> GetSidAsync(int csdbId, CancellationToken cancellationToken = default)
    {
        var xml = await GetStringAsync($"webservice/?type=sid&id={csdbId}&depth=1", cancellationToken)
            .ConfigureAwait(false);
        var doc = XDocument.Parse(xml);
        var sid = doc.Root?.Element("SID")
            ?? throw new CsdbException($"CSDb SID {csdbId} not found");
        var hvsc = sid.Element("HVSCPath")?.Value?.Trim().Replace('\\', '/');
        if (!string.IsNullOrEmpty(hvsc) && !hvsc.StartsWith('/'))
        {
            hvsc = "/" + hvsc;
        }

        return new CsdbSidDetail(
            csdbId,
            (sid.Element("Name")?.Value ?? $"sid-{csdbId}").Trim(),
            hvsc,
            sid.Element("Author")?.Value);
    }

    public async Task<(byte[] Data, string? FileName)> DownloadBytesAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        await ThrottleAsync(cancellationToken).ConfigureAwait(false);
        using var resp = await _http.GetAsync(MakeUri(url), cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var fileName = resp.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        return (data, fileName);
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private async Task CollectReleaseHitsAsync(
        string path,
        HashSet<CsdbKind> kindSet,
        List<CsdbSearchHit> hits,
        HashSet<(CsdbKind, int)> seen,
        int limit,
        CancellationToken ct)
    {
        var html = await GetStringAsync(path, ct).ConfigureAwait(false);
        foreach (Match m in ReleaseRe.Matches(html))
        {
            var id = int.Parse(m.Groups[2].Value);
            var title = Clean(m.Groups[3].Value);
            var csdbType = Clean(m.Groups[4].Value);
            var kind = CsdbClassifier.ParseKind(CsdbClassifier.ClassifyReleaseType(csdbType));
            if (!kindSet.Contains(kind))
            {
                continue;
            }

            if (!seen.Add((kind, id)))
            {
                continue;
            }

            hits.Add(new CsdbSearchHit(
                id, title, kind, csdbType,
                Url: new Uri(_http.BaseAddress!, m.Groups[1].Value.TrimStart('/')).ToString()));
            if (hits.Count >= limit)
            {
                return;
            }
        }
    }

    private async Task CollectSidHitsAsync(
        string path,
        List<CsdbSearchHit> hits,
        HashSet<(CsdbKind, int)> seen,
        int limit,
        CancellationToken ct)
    {
        var html = await GetStringAsync(path, ct).ConfigureAwait(false);
        foreach (Match m in SidRe.Matches(html))
        {
            var id = int.Parse(m.Groups[2].Value);
            if (!seen.Add((CsdbKind.Sid, id)))
            {
                continue;
            }

            hits.Add(new CsdbSearchHit(
                id, Clean(m.Groups[3].Value), CsdbKind.Sid, "SID",
                Url: new Uri(_http.BaseAddress!, m.Groups[1].Value.TrimStart('/')).ToString()));
            if (hits.Count >= limit)
            {
                return;
            }
        }
    }

    private async Task<string> GetStringAsync(string path, CancellationToken ct)
    {
        await ThrottleAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync(MakeUri(path), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private Uri MakeUri(string pathOrUrl)
    {
        if (pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(pathOrUrl);
        }

        return new Uri(_http.BaseAddress!, pathOrUrl.TrimStart('/'));
    }

    private async Task ThrottleAsync(CancellationToken ct)
    {
        if (_options.MinRequestInterval <= TimeSpan.Zero)
        {
            return;
        }

        TimeSpan wait;
        lock (_throttleLock)
        {
            var elapsed = _clock.GetUtcNow() - _lastRequest;
            wait = _options.MinRequestInterval - elapsed;
            if (wait <= TimeSpan.Zero)
            {
                _lastRequest = _clock.GetUtcNow();
                wait = TimeSpan.Zero;
            }
        }

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, ct).ConfigureAwait(false);
            lock (_throttleLock)
            {
                _lastRequest = _clock.GetUtcNow();
            }
        }
    }

    private static string Clean(string s) =>
        Regex.Replace(WebUtility.HtmlDecode(s) ?? s, @"\s+", " ").Trim();

    [GeneratedRegex(@"href=""(/release/\?id=(\d+))""[^>]*>([^<]+)</a>\s*\(([^)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ReleasePattern();

    [GeneratedRegex(@"href=""(/sid/\?id=(\d+))""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex SidPattern();
}
