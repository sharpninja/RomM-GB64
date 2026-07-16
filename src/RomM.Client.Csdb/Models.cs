namespace RomM.Client.Csdb;

public enum CsdbKind
{
    Demo,
    Crack,
    Sid,
    Other,
}

public sealed record CsdbSearchHit(int CsdbId, string Title, CsdbKind Kind, string? CsdbType, int? Year = null, string? Url = null);

public sealed record CsdbSelection(int CsdbId, CsdbKind Kind);

public sealed record CsdbSearchRequest(string Query, IReadOnlyList<CsdbKind>? Kinds = null, int Limit = 25);

public sealed record CsdbDownloadLink(string Url, string Status, int? Downloads = null);

public sealed record CsdbReleaseDetail(int CsdbId, string Name, string CsdbType, IReadOnlyList<CsdbDownloadLink> DownloadLinks, string? Screenshot = null);

public sealed record CsdbSidDetail(int CsdbId, string Name, string? HvscPath, string? Author = null);

public sealed record CsdbIngestItemResult(
    int CsdbId,
    CsdbKind Kind,
    string Title,
    string Status,
    IReadOnlyList<string> Paths,
    string? Error = null);

public sealed record CsdbIngestResult(
    string JobId,
    int Requested,
    IReadOnlyList<CsdbIngestItemResult> Items);

public sealed record CsdbIngestAndScanResult(
    CsdbIngestResult Ingest,
    bool ScanRequested,
    bool ScanCompleted);

public sealed class CsdbIngestOptions
{
    public bool Force { get; set; }
    public string? JobId { get; set; }
}
