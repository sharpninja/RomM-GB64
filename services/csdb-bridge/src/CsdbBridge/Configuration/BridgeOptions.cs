namespace CsdbBridge.Configuration;

public sealed class BridgeOptions
{
    public const string SectionName = "Bridge";

    public string CsdbBaseUrl { get; set; } = "https://csdb.dk";
    public string CsdbUser { get; set; } = "";
    public string CsdbPassword { get; set; } = "";
    public string CsdbUserAgent { get; set; } = "RomM-GB64-CSDbBridge/1.0 (+local; C#)";
    public int CsdbRateLimitMs { get; set; } = 1200;
    public double CsdbRequestTimeoutS { get; set; } = 45;

    public string LibraryRomsRoot { get; set; } = "/data/roms";
    public string HvscRoot { get; set; } = "/romm/library/hvsc";

    public string CsdbDataRoot { get; set; } = "/data/csdb";
    public string CsdbIndexDb { get; set; } = "";
    public string CsdbRssRawDir { get; set; } = "";

    public int MaxResultsDefault { get; set; } = 25;
    public int MaxResultsCap { get; set; } = 50;

    public string BridgeApiKey { get; set; } = "";
    public string RommUrl { get; set; } = "http://romm:8080";
    public string RommApiToken { get; set; } = "";

    public string ResolvedIndexDb =>
        string.IsNullOrWhiteSpace(CsdbIndexDb)
            ? Path.Combine(CsdbDataRoot, "index.sqlite3")
            : CsdbIndexDb;

    public string ResolvedRssRawDir =>
        string.IsNullOrWhiteSpace(CsdbRssRawDir)
            ? Path.Combine(CsdbDataRoot, "rss")
            : CsdbRssRawDir;
}
