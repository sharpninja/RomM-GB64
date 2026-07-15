using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CsdbBridge.Configuration;
using CsdbBridge.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace CsdbBridge.Services;

/// <summary>
/// Downloads CSDb RSS recent-window feeds and indexes metadata only.
/// Never full-site crawls or binary downloads.
/// </summary>
public sealed partial class RssIndexService
{
    public static readonly (string Key, string Path)[] DefaultFeeds =
    [
        ("available_feeds", "/rss/index.php"),
        ("scene_news", "/rss/scenenews.php"),
        ("latest_forum_posts", "/rss/latestforumposts.php"),
        ("latest_releases", "/rss/latestreleases.php"),
        ("latest_additions_release", "/rss/latestadditions.php?type=release"),
        ("latest_additions_sid", "/rss/latestadditions.php?type=sid"),
        ("latest_comments", "/rss/latestcomments.php"),
        ("upcoming_events", "/rss/upcomingevents.php"),
    ];

    private readonly BridgeOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly object _throttleLock = new();
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public RssIndexService(IOptions<BridgeOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        Directory.CreateDirectory(Path.GetDirectoryName(_options.ResolvedIndexDb)!);
        Directory.CreateDirectory(_options.ResolvedRssRawDir);
        EnsureSchema();
    }

    public async Task<IReadOnlyList<FeedFetchResult>> RefreshAsync(
        IEnumerable<string>? feedKeys = null,
        CancellationToken ct = default)
    {
        var selected = DefaultFeeds.AsEnumerable();
        if (feedKeys is not null)
        {
            var set = feedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            selected = DefaultFeeds.Where(f => set.Contains(f.Key));
            if (!selected.Any())
                throw new ArgumentException("No matching feeds for given keys.");
        }

        var client = _httpClientFactory.CreateClient("csdb");
        client.BaseAddress ??= new Uri(_options.CsdbBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _options.CsdbUserAgent);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(_options.CsdbRequestTimeoutS, 60));

        var results = new List<FeedFetchResult>();
        foreach (var (key, path) in selected)
            results.Add(await FetchOneAsync(client, key, path, ct).ConfigureAwait(false));
        return results;
    }

    public IndexStats GetStats()
    {
        using var conn = Open();
        var feeds = ScalarInt(conn, "SELECT COUNT(*) FROM feeds");
        var items = ScalarInt(conn, "SELECT COUNT(*) FROM items");
        var byKind = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT kind, COUNT(*) AS c FROM items GROUP BY kind";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                byKind[r.IsDBNull(0) ? "unknown" : r.GetString(0)] = r.GetInt32(1);
        }

        string? last = null;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT MAX(fetched_at) FROM feeds";
            last = cmd.ExecuteScalar()?.ToString();
        }

        return new IndexStats(feeds, items, byKind, last);
    }

    public IReadOnlyList<Dictionary<string, object?>> ListFeeds()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT feed_key, url, title, fetched_at, http_status, item_count, error FROM feeds ORDER BY feed_key";
        using var r = cmd.ExecuteReader();
        var list = new List<Dictionary<string, object?>>();
        while (r.Read())
        {
            list.Add(new Dictionary<string, object?>
            {
                ["feed_key"] = r.GetString(0),
                ["url"] = r.GetString(1),
                ["title"] = r.IsDBNull(2) ? null : r.GetString(2),
                ["fetched_at"] = r.GetString(3),
                ["http_status"] = r.IsDBNull(4) ? null : r.GetInt32(4),
                ["item_count"] = r.GetInt32(5),
                ["error"] = r.IsDBNull(6) ? null : r.GetString(6),
            });
        }

        return list;
    }

    public IReadOnlyList<Dictionary<string, object?>> Search(
        string query, IReadOnlyList<string>? kinds = null, int limit = 25, int offset = 0)
    {
        limit = Math.Clamp(limit, 1, 500);
        offset = Math.Max(0, offset);
        var q = (query ?? "").Trim();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        if (q.Length == 0)
        {
            cmd.CommandText = "SELECT * FROM items";
            if (kinds is { Count: > 0 })
            {
                var ph = string.Join(",", kinds.Select((_, i) => $"@k{i}"));
                cmd.CommandText += $" WHERE kind IN ({ph})";
                for (var i = 0; i < kinds.Count; i++)
                    cmd.Parameters.AddWithValue($"@k{i}", kinds[i]);
            }

            cmd.CommandText += " ORDER BY COALESCE(pub_ts, 0) DESC, id DESC LIMIT @lim OFFSET @off";
            cmd.Parameters.AddWithValue("@lim", limit);
            cmd.Parameters.AddWithValue("@off", offset);
        }
        else
        {
            // Simple LIKE search (portable; avoids FTS5 packaging issues). Polite local-only.
            cmd.CommandText = """
                SELECT * FROM items
                WHERE (title LIKE @q OR description_text LIKE @q OR IFNULL(csdb_type,'') LIKE @q)
                """;
            if (kinds is { Count: > 0 })
            {
                var ph = string.Join(",", kinds.Select((_, i) => $"@k{i}"));
                cmd.CommandText += $" AND kind IN ({ph})";
                for (var i = 0; i < kinds.Count; i++)
                    cmd.Parameters.AddWithValue($"@k{i}", kinds[i]);
            }

            cmd.CommandText += " ORDER BY COALESCE(pub_ts, 0) DESC, id DESC LIMIT @lim OFFSET @off";
            cmd.Parameters.AddWithValue("@q", $"%{q}%");
            cmd.Parameters.AddWithValue("@lim", limit);
            cmd.Parameters.AddWithValue("@off", offset);
        }

        using var r = cmd.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (r.Read())
            rows.Add(ReadItem(r));
        return rows;
    }

    private async Task<FeedFetchResult> FetchOneAsync(
        HttpClient client, string feedKey, string path, CancellationToken ct)
    {
        var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : new Uri(client.BaseAddress!, path.TrimStart('/')).ToString();
        var now = DateTime.UtcNow.ToString("o");
        try
        {
            await ThrottleAsync(ct).ConfigureAwait(false);
            using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
            var status = (int)resp.StatusCode;
            resp.EnsureSuccessStatusCode();
            var raw = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            var rawPath = Path.Combine(_options.ResolvedRssRawDir, $"{feedKey}.xml");
            await File.WriteAllBytesAsync(rawPath, raw, ct).ConfigureAwait(false);
            var text = System.Text.Encoding.UTF8.GetString(raw);

            if (feedKey == "available_feeds")
            {
                UpsertFeed(feedKey, url, "CSDb available feeds", now, status, 0, rawPath, null);
                return new FeedFetchResult(feedKey, url, true, 0);
            }

            var items = ParseRssItems(text, feedKey);
            UpsertFeed(feedKey, url, ChannelTitle(text), now, status, items.Count, rawPath, null);
            foreach (var it in items)
                UpsertItem(feedKey, it, now);
            return new FeedFetchResult(feedKey, url, true, items.Count);
        }
        catch (Exception ex)
        {
            UpsertFeed(feedKey, url, null, now, null, 0, null, ex.Message);
            return new FeedFetchResult(feedKey, url, false, 0, ex.Message);
        }
    }

    internal static List<Dictionary<string, object?>> ParseRssItems(string xmlText, string feedKey)
    {
        var outList = new List<Dictionary<string, object?>>();
        var doc = XDocument.Parse(xmlText);
        var channel = doc.Root?.Element("channel");
        if (channel is null)
            return outList;

        foreach (var item in channel.Elements("item"))
        {
            var title = (item.Element("title")?.Value ?? "").Trim();
            var link = (item.Element("link")?.Value ?? "").Trim();
            var guid = (item.Element("guid")?.Value ?? link ?? title).Trim();
            var descHtml = WebUtility.HtmlDecode(item.Element("description")?.Value ?? "") ?? "";
            var pubDate = (item.Element("pubDate")?.Value ?? "").Trim();
            long? pubTs = ParsePubTs(pubDate);
            var plain = StripHtml(descHtml);
            var (entryType, entryId) = DetectEntry(link ?? "", guid ?? "", descHtml);
            var csdbType = ExtractType(descHtml);
            var kind = KindFor(entryType, csdbType, feedKey);
            var (downloadUrl, downloadFileUrl) = ExtractDownloads(descHtml);
            var screenshot = ExtractImg(descHtml);
            var groups = ExtractGroups(descHtml);
            outList.Add(new Dictionary<string, object?>
            {
                ["guid"] = guid,
                ["title"] = title,
                ["link"] = link,
                ["description_text"] = plain,
                ["description_html"] = descHtml,
                ["pub_date"] = string.IsNullOrEmpty(pubDate) ? null : pubDate,
                ["pub_ts"] = pubTs,
                ["entry_type"] = entryType,
                ["entry_id"] = entryId,
                ["kind"] = kind,
                ["csdb_type"] = csdbType,
                ["download_url"] = downloadUrl,
                ["download_file_url"] = downloadFileUrl,
                ["screenshot_url"] = screenshot,
                ["groups_json"] = groups,
                ["raw_hash"] = HashCode.Combine(guid, title, plain.Length).ToString(),
            });
        }

        return outList;
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS feeds (
                feed_key TEXT PRIMARY KEY,
                url TEXT NOT NULL,
                title TEXT,
                fetched_at TEXT NOT NULL,
                http_status INTEGER,
                item_count INTEGER NOT NULL DEFAULT 0,
                raw_path TEXT,
                error TEXT
            );
            CREATE TABLE IF NOT EXISTS items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                feed_key TEXT NOT NULL,
                guid TEXT,
                title TEXT,
                link TEXT,
                description_text TEXT,
                description_html TEXT,
                pub_date TEXT,
                pub_ts INTEGER,
                entry_type TEXT,
                entry_id INTEGER,
                kind TEXT,
                csdb_type TEXT,
                download_url TEXT,
                download_file_url TEXT,
                screenshot_url TEXT,
                groups_json TEXT,
                raw_hash TEXT,
                indexed_at TEXT NOT NULL,
                UNIQUE(feed_key, guid)
            );
            CREATE INDEX IF NOT EXISTS idx_items_entry ON items(entry_type, entry_id);
            CREATE INDEX IF NOT EXISTS idx_items_kind ON items(kind);
            CREATE INDEX IF NOT EXISTS idx_items_title ON items(title);
            CREATE INDEX IF NOT EXISTS idx_items_pub_ts ON items(pub_ts);
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_options.ResolvedIndexDb}");
        conn.Open();
        return conn;
    }

    private void UpsertFeed(
        string feedKey, string url, string? title, string fetchedAt,
        int? httpStatus, int itemCount, string? rawPath, string? error)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO feeds(feed_key, url, title, fetched_at, http_status, item_count, raw_path, error)
            VALUES ($k,$u,$t,$f,$s,$c,$p,$e)
            ON CONFLICT(feed_key) DO UPDATE SET
              url=excluded.url, title=excluded.title, fetched_at=excluded.fetched_at,
              http_status=excluded.http_status, item_count=excluded.item_count,
              raw_path=excluded.raw_path, error=excluded.error
            """;
        cmd.Parameters.AddWithValue("$k", feedKey);
        cmd.Parameters.AddWithValue("$u", url);
        cmd.Parameters.AddWithValue("$t", (object?)title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$f", fetchedAt);
        cmd.Parameters.AddWithValue("$s", (object?)httpStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$c", itemCount);
        cmd.Parameters.AddWithValue("$p", (object?)rawPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$e", (object?)error ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void UpsertItem(string feedKey, Dictionary<string, object?> it, string indexedAt)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO items(
              feed_key, guid, title, link, description_text, description_html,
              pub_date, pub_ts, entry_type, entry_id, kind, csdb_type,
              download_url, download_file_url, screenshot_url, groups_json,
              raw_hash, indexed_at
            ) VALUES (
              $fk,$g,$t,$l,$dt,$dh,$pd,$pt,$et,$ei,$k,$ct,$du,$df,$ss,$gj,$rh,$ia
            )
            ON CONFLICT(feed_key, guid) DO UPDATE SET
              title=excluded.title, link=excluded.link,
              description_text=excluded.description_text,
              description_html=excluded.description_html,
              pub_date=excluded.pub_date, pub_ts=excluded.pub_ts,
              entry_type=excluded.entry_type, entry_id=excluded.entry_id,
              kind=excluded.kind, csdb_type=excluded.csdb_type,
              download_url=excluded.download_url, download_file_url=excluded.download_file_url,
              screenshot_url=excluded.screenshot_url, groups_json=excluded.groups_json,
              raw_hash=excluded.raw_hash, indexed_at=excluded.indexed_at
            """;
        cmd.Parameters.AddWithValue("$fk", feedKey);
        cmd.Parameters.AddWithValue("$g", it["guid"] ?? "");
        cmd.Parameters.AddWithValue("$t", it["title"] ?? "");
        cmd.Parameters.AddWithValue("$l", it["link"] ?? "");
        cmd.Parameters.AddWithValue("$dt", it["description_text"] ?? "");
        cmd.Parameters.AddWithValue("$dh", it["description_html"] ?? "");
        cmd.Parameters.AddWithValue("$pd", it["pub_date"] ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$pt", it["pub_ts"] ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$et", it["entry_type"] ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$ei", it["entry_id"] ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$k", it["kind"] ?? "other");
        cmd.Parameters.AddWithValue("$ct", it["csdb_type"] ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$du", it["download_url"] ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$df", it["download_file_url"] ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$ss", it["screenshot_url"] ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$gj", it["groups_json"] ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$rh", it["raw_hash"] ?? "");
        cmd.Parameters.AddWithValue("$ia", indexedAt);
        cmd.ExecuteNonQuery();
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

    private static int ScalarInt(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static Dictionary<string, object?> ReadItem(SqliteDataReader r)
    {
        var d = new Dictionary<string, object?>();
        for (var i = 0; i < r.FieldCount; i++)
            d[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        return d;
    }

    private static string? ChannelTitle(string xml)
    {
        try
        {
            return XDocument.Parse(xml).Root?.Element("channel")?.Element("title")?.Value?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static long? ParsePubTs(string pubDate)
    {
        if (string.IsNullOrWhiteSpace(pubDate))
            return null;
        if (DateTimeOffset.TryParse(pubDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var dto))
            return dto.ToUnixTimeSeconds();
        return null;
    }

    private static string StripHtml(string s)
    {
        var text = BrPattern().Replace(s, "\n");
        text = TagPattern().Replace(text, " ");
        text = WebUtility.HtmlDecode(text) ?? text;
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static (string? Type, int? Id) DetectEntry(string link, string guid, string desc)
    {
        var blob = $"{link} {guid} {desc}";
        var m = ReleaseIdPattern().Match(blob);
        if (m.Success) return ("release", int.Parse(m.Groups[1].Value));
        m = SidIdPattern().Match(blob);
        if (m.Success) return ("sid", int.Parse(m.Groups[1].Value));
        return (null, null);
    }

    private static string? ExtractType(string descHtml)
    {
        var m = TypePattern().Match(descHtml);
        return m.Success ? Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim() : null;
    }

    private static (string? DownloadUrl, string? FileUrl) ExtractDownloads(string descHtml)
    {
        var m = DownloadTitlePattern().Match(descHtml);
        if (m.Success)
            return (m.Groups[1].Value, WebUtility.HtmlDecode(m.Groups[2].Value));
        m = DownloadHrefPattern().Match(descHtml);
        return m.Success ? (m.Groups[1].Value, null) : (null, null);
    }

    private static string? ExtractImg(string descHtml)
    {
        var m = ImgPattern().Match(descHtml);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractGroups(string descHtml)
    {
        var names = GroupNamePattern().Matches(descHtml)
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)?.Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
        return names.Count == 0 ? null : JsonSerializer.Serialize(names);
    }

    private static string KindFor(string? entryType, string? csdbType, string feedKey)
    {
        if (entryType == "sid" || feedKey.EndsWith("_sid", StringComparison.OrdinalIgnoreCase))
            return "sid";
        if (!string.IsNullOrEmpty(csdbType))
            return CsdbClassifier.ClassifyReleaseType(csdbType);
        if (entryType == "release")
            return "other";
        if (feedKey is "scene_news" or "latest_forum_posts" or "latest_comments")
            return "meta";
        return "other";
    }

    [GeneratedRegex(@"release/\?id=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ReleaseIdPattern();

    [GeneratedRegex(@"sid/\?id=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SidIdPattern();

    [GeneratedRegex(@"Type:\s*(?:<[^>]+>)*([^<]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TypePattern();

    [GeneratedRegex(@"href=""(https://csdb\.dk/release/download\.php\?id=\d+)""[^>]*title=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadTitlePattern();

    [GeneratedRegex(@"href=""(https://csdb\.dk/(?:release/download\.php\?id=\d+|getinternalfile\.php/[^""]+))""", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadHrefPattern();

    [GeneratedRegex(@"<img[^>]+src=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgPattern();

    [GeneratedRegex(@"href=""https://csdb\.dk/group/\?id=\d+""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex GroupNamePattern();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrPattern();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagPattern();
}
