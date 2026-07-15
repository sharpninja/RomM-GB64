namespace CsdbBridge.Services;

public static class CsdbClassifier
{
    public static string ClassifyReleaseType(string? csdbType)
    {
        var t = (csdbType ?? "").ToLowerInvariant();
        if (t.Contains("sid") || t.Contains("music"))
            return "sid";
        if (t.Contains("crack"))
            return "crack";
        if (t.Contains("demo") || t.Contains("intro") || t.Contains("one-file")
            || t.Contains("onefile") || t.Contains("invitation")
            || t.Contains("diskmag") || t.Contains("magazine"))
            return "demo";
        return "other";
    }

    public static string PlatformFolderForKind(string kind) => kind switch
    {
        "demo" => "c64-csdb-demo",
        "crack" => "c64-csdb-crack",
        "sid" => "c64-csdb-sid",
        _ => "c64-csdb-misc",
    };

    public static string SanitizeName(string name, int maxLen = 120)
    {
        var cleaned = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "untitled";
        return cleaned.Length <= maxLen ? cleaned : cleaned[..maxLen];
    }
}
