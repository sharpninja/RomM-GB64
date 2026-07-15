namespace CsdbBridge.Models;

public sealed record SearchHit(
    string Kind,
    int CsdbId,
    string Title,
    string CsdbType,
    string PageUrl);

public sealed record DownloadLink(
    string Url,
    string? CounterUrl = null,
    string Status = "Ok",
    int? Downloads = null);

public sealed record ReleaseDetail(
    int CsdbId,
    string Name,
    string CsdbType,
    IReadOnlyList<DownloadLink> DownloadLinks,
    string? Screenshot = null);

public sealed record SidDetail(
    int CsdbId,
    string Name,
    string? HvscPath = null,
    string? Author = null);

public sealed record IngestItemResult(
    int CsdbId,
    string Kind,
    string Title,
    string Status,
    IReadOnlyList<string> Paths,
    string? Error = null);

public sealed record IngestJobResult(
    string JobId,
    string Query,
    IReadOnlyList<string> Kinds,
    int Total,
    IReadOnlyList<IngestItemResult> Items);

public sealed record FeedFetchResult(
    string FeedKey,
    string Url,
    bool Ok,
    int ItemCount = 0,
    string? Error = null);

public sealed record IndexStats(
    int Feeds,
    int Items,
    IReadOnlyDictionary<string, int> ByKind,
    string? LastFetchedAt);
