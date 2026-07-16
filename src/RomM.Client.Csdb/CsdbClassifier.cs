namespace RomM.Client.Csdb;

public static class CsdbClassifier
{
    public static string ClassifyReleaseType(string? csdbType)
    {
        var t = (csdbType ?? "").ToLowerInvariant();
        if (t.Contains("sid") || t.Contains("music"))
        {
            return "sid";
        }

        if (t.Contains("crack"))
        {
            return "crack";
        }

        if (t.Contains("demo") || t.Contains("intro") || t.Contains("one-file")
            || t.Contains("onefile") || t.Contains("invitation")
            || t.Contains("diskmag") || t.Contains("magazine"))
        {
            return "demo";
        }

        return "other";
    }

    public static CsdbKind ParseKind(string kind) => kind.ToLowerInvariant() switch
    {
        "demo" => CsdbKind.Demo,
        "crack" => CsdbKind.Crack,
        "sid" => CsdbKind.Sid,
        _ => CsdbKind.Other,
    };

    public static string KindString(CsdbKind kind) => kind switch
    {
        CsdbKind.Demo => "demo",
        CsdbKind.Crack => "crack",
        CsdbKind.Sid => "sid",
        _ => "other",
    };

    /// <summary>RomM Structure A platform slug. All C64 scene content under built-in c64.</summary>
    public static string PlatformFolderForKind(CsdbKind kind) => "c64";

    public static string PlatformFolderForKind(string kind) => "c64";

    public static string TypeTagForKind(CsdbKind kind) => kind switch
    {
        CsdbKind.Demo => "(Demo)",
        CsdbKind.Crack => "(Crack)",
        CsdbKind.Sid => "(SID)",
        _ => "",
    };

    public static string TypeTagForKind(string kind) => TypeTagForKind(ParseKind(kind));

    public static string PackageBaseName(string title, CsdbKind kind, int csdbId)
    {
        var name = SanitizeName(title);
        var type = TypeTagForKind(kind);
        return string.IsNullOrEmpty(type)
            ? $"{name} (csdb-{csdbId})"
            : $"{name} {type} (csdb-{csdbId})";
    }

    public static string PackageBaseName(string title, string kind, int csdbId) =>
        PackageBaseName(title, ParseKind(kind), csdbId);

    public static string SanitizeName(string name, int maxLen = 120)
    {
        var cleaned = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "untitled";
        }

        return cleaned.Length <= maxLen ? cleaned : cleaned[..maxLen];
    }
}
