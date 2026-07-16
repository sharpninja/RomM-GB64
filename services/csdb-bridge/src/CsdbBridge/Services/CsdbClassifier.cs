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

    /// <summary>
    /// RomM Structure A platform slug. All C64 scene content goes under built-in <c>c64</c>
    /// so the UI does not invent unidentified platforms like c64-csdb-demo.
    /// Kind is expressed with filename/folder tags via <see cref="TypeTagForKind"/>.
    /// </summary>
    public static string PlatformFolderForKind(string kind) => "c64";

    /// <summary>Optional RomM-style type tag for CSDb kind (empty when none).</summary>
    public static string TypeTagForKind(string kind) => kind switch
    {
        "demo" => "(Demo)",
        "crack" => "(Crack)",
        "sid" => "(SID)",
        _ => "",
    };

    /// <summary>Package base name: {Name} [(Type)] (csdb-{id})</summary>
    public static string PackageBaseName(string title, string kind, int csdbId)
    {
        var name = SanitizeName(title);
        var type = TypeTagForKind(kind);
        return string.IsNullOrEmpty(type)
            ? $"{name} (csdb-{csdbId})"
            : $"{name} {type} (csdb-{csdbId})";
    }

    public static string SanitizeName(string name, int maxLen = 120)
    {
        var cleaned = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "untitled";
        return cleaned.Length <= maxLen ? cleaned : cleaned[..maxLen];
    }
}
