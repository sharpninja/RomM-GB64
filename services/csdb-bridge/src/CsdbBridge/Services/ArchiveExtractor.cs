using System.IO.Compression;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace CsdbBridge.Services;

/// <summary>
/// Detects archives and extracts them into a destination folder under roms/.
/// Supports zip (BCL + SharpCompress), 7z, rar, tar, gzip, bzip2, lzip, xz.
/// </summary>
public static class ArchiveExtractor
{
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar", ".tar", ".tgz", ".gz", ".bz2", ".xz", ".lzh", ".lha", ".arj",
    };

    public static bool IsArchiveFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;
        var ext = Path.GetExtension(fileName);
        if (ArchiveExtensions.Contains(ext))
            return true;
        // compound: .tar.gz
        var name = fileName.ToLowerInvariant();
        return name.EndsWith(".tar.gz") || name.EndsWith(".tar.bz2") || name.EndsWith(".tar.xz");
    }

    public static bool LooksLikeZip(ReadOnlySpan<byte> data)
        => data.Length >= 4 && data[0] == (byte)'P' && data[1] == (byte)'K';

    public static bool LooksLike7z(ReadOnlySpan<byte> data)
        => data.Length >= 6
           && data[0] == 0x37 && data[1] == 0x7A && data[2] == 0xBC
           && data[3] == 0xAF && data[4] == 0x27 && data[5] == 0x1C;

    public static bool LooksLikeRar(ReadOnlySpan<byte> data)
        => data.Length >= 7
           && data[0] == 0x52 && data[1] == 0x61 && data[2] == 0x72
           && data[3] == 0x21 && data[4] == 0x1A && data[5] == 0x07;

    public static bool IsArchive(byte[] data, string? fileName)
    {
        if (IsArchiveFileName(fileName))
            return true;
        var span = data.AsSpan();
        return LooksLikeZip(span) || LooksLike7z(span) || LooksLikeRar(span);
    }

    /// <summary>
    /// Extract archive bytes into <paramref name="destDir"/>. Returns extracted file paths.
    /// Does not leave the archive file on disk.
    /// </summary>
    public static IReadOnlyList<string> ExtractToDirectory(byte[] data, string? fileName, string destDir)
    {
        Directory.CreateDirectory(destDir);
        var written = new List<string>();

        // Prefer System.IO.Compression for plain zip (no extra native deps).
        if (LooksLikeZip(data) || string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream(data, writable: false);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'))
                    continue; // directory entry
                var relative = SanitizeEntryPath(entry.FullName);
                if (relative is null)
                    continue;
                var outPath = Path.Combine(destDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                using var entryStream = entry.Open();
                using var outFs = File.Create(outPath);
                entryStream.CopyTo(outFs);
                written.Add(outPath);
            }

            if (written.Count > 0)
                return FlattenSingleRoot(destDir, written);
        }

        // SharpCompress for 7z/rar/tar/etc. and zip fallback.
        written.Clear();
        using (var ms = new MemoryStream(data, writable: false))
        using (var archive = ArchiveFactory.OpenArchive(ms))
        {
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var relative = SanitizeEntryPath(entry.Key);
                if (relative is null)
                    continue;
                var outPath = Path.Combine(destDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                using var entryStream = entry.OpenEntryStream();
                using var outFs = File.Create(outPath);
                entryStream.CopyTo(outFs);
                written.Add(outPath);
            }
        }

        if (written.Count == 0)
            throw new InvalidOperationException($"Archive produced no files ({fileName ?? "unknown"}).");

        return FlattenSingleRoot(destDir, written);
    }

    /// <summary>
    /// If the archive contains a single top-level directory, leave it as-is
    /// (RomM multi-file game folder is the package dir we already use).
    /// Returns the list of file paths under destDir.
    /// </summary>
    private static IReadOnlyList<string> FlattenSingleRoot(string destDir, List<string> written)
    {
        // Optional flatten: if exactly one top-level folder and no loose files, hoist contents up.
        var topEntries = Directory.GetFileSystemEntries(destDir);
        if (topEntries.Length == 1 && Directory.Exists(topEntries[0]))
        {
            var onlyDir = topEntries[0];
            foreach (var path in Directory.GetFileSystemEntries(onlyDir))
            {
                var name = Path.GetFileName(path);
                var target = Path.Combine(destDir, name);
                if (Directory.Exists(path))
                    Directory.Move(path, target);
                else
                    File.Move(path, target, overwrite: true);
            }

            Directory.Delete(onlyDir, recursive: true);
        }

        return Directory.GetFiles(destDir, "*", SearchOption.AllDirectories).ToList();
    }

    /// <summary>Reject absolute paths and path traversal.</summary>
    internal static string? SanitizeEntryPath(string? entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            return null;
        var name = entryName.Replace('\\', '/').TrimStart('/');
        if (name.Length == 0 || name.EndsWith('/'))
            return null;
        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => p is "." or ".."))
            return null;
        // Drop drive-like prefixes
        if (parts[0].Contains(':'))
            return null;
        var joined = Path.Combine(parts);
        // Final safety: must stay relative
        if (Path.IsPathRooted(joined))
            return null;
        return joined;
    }
}
