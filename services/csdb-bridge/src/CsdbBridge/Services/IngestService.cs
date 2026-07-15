using System.Text.Json;
using CsdbBridge.Configuration;
using CsdbBridge.Models;
using Microsoft.Extensions.Options;

namespace CsdbBridge.Services;

public sealed class IngestService
{
    private readonly CsdbClient _client;
    private readonly BridgeOptions _options;

    public IngestService(CsdbClient client, IOptions<BridgeOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<IngestJobResult> IngestAsync(
        IReadOnlyList<SearchHit> hits,
        string? jobId = null,
        bool force = false,
        CancellationToken ct = default)
    {
        jobId ??= $"job-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var romsRoot = Path.GetFullPath(_options.LibraryRomsRoot);
        var hvscRoot = Path.GetFullPath(_options.HvscRoot);
        Directory.CreateDirectory(romsRoot);

        var items = new List<IngestItemResult>();
        foreach (var hit in hits)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var paths = hit.Kind.Equals("sid", StringComparison.OrdinalIgnoreCase)
                    ? await IngestSidAsync(hit, romsRoot, hvscRoot, force, ct).ConfigureAwait(false)
                    : await IngestReleaseAsync(hit, romsRoot, force, ct).ConfigureAwait(false);
                items.Add(new IngestItemResult(
                    hit.CsdbId, hit.Kind, hit.Title,
                    paths.Count > 0 ? "ok" : "skipped",
                    paths.Select(p => p).ToList()));
            }
            catch (Exception ex)
            {
                items.Add(new IngestItemResult(
                    hit.CsdbId, hit.Kind, hit.Title, "error",
                    Array.Empty<string>(), ex.Message));
            }
        }

        var result = new IngestJobResult(
            jobId,
            "",
            hits.Select(h => h.Kind).Distinct().OrderBy(x => x).ToList(),
            hits.Count,
            items);

        var manifestDir = Path.GetFileName(romsRoot).Equals("roms", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(Path.GetDirectoryName(romsRoot)!, "csdb-ingest")
            : Path.Combine(romsRoot, "csdb-ingest");
        Directory.CreateDirectory(manifestDir);
        var manifestPath = Path.Combine(manifestDir, $"{jobId}.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
            ct).ConfigureAwait(false);

        return result;
    }

    private async Task<List<string>> IngestReleaseAsync(
        SearchHit hit, string romsRoot, bool force, CancellationToken ct)
    {
        var detail = await _client.GetReleaseAsync(hit.CsdbId, 2, ct).ConfigureAwait(false);
        var kind = string.IsNullOrEmpty(hit.Kind)
            ? CsdbClassifier.ClassifyReleaseType(detail.CsdbType)
            : hit.Kind;
        var folder = CsdbClassifier.PlatformFolderForKind(kind);
        var baseDir = Path.Combine(romsRoot, folder);
        Directory.CreateDirectory(baseDir);
        var package = $"{CsdbClassifier.SanitizeName(detail.Name)} (csdb-{detail.CsdbId})";
        var destDir = Path.Combine(baseDir, package);
        if (Directory.Exists(destDir) && !force)
            return Directory.GetFiles(destDir, "*", SearchOption.AllDirectories).ToList();

        var okLinks = detail.DownloadLinks
            .Where(d => d.Status.Equals("Ok", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.Url))
            .ToList();
        if (okLinks.Count == 0)
            throw new InvalidOperationException($"No Ok download links for release {detail.CsdbId}");

        if (force && Directory.Exists(destDir))
            Directory.Delete(destDir, recursive: true);

        Directory.CreateDirectory(destDir);
        var written = new List<string>();
        var idx = 0;
        foreach (var link in okLinks)
        {
            var (data, fileName) = await _client.DownloadBytesAsync(link.Url, ct).ConfigureAwait(false);
            var originalName = fileName;
            fileName = CsdbClassifier.SanitizeName(fileName ?? $"file-{++idx}.bin", 180);

            if (ArchiveExtractor.IsArchive(data, originalName ?? fileName))
            {
                // Extract archive into the package folder under roms/; do not keep the archive.
                var extracted = ArchiveExtractor.ExtractToDirectory(data, originalName ?? fileName, destDir);
                written.AddRange(extracted);
            }
            else
            {
                var outPath = Path.Combine(destDir, fileName);
                await File.WriteAllBytesAsync(outPath, data, ct).ConfigureAwait(false);
                written.Add(outPath);
            }
        }

        // Prefer returning actual files currently under the package folder.
        return Directory.Exists(destDir)
            ? Directory.GetFiles(destDir, "*", SearchOption.AllDirectories).ToList()
            : written;
    }

    private async Task<List<string>> IngestSidAsync(
        SearchHit hit, string romsRoot, string hvscRoot, bool force, CancellationToken ct)
    {
        var detail = await _client.GetSidAsync(hit.CsdbId, 1, ct).ConfigureAwait(false);
        var baseDir = Path.Combine(romsRoot, CsdbClassifier.PlatformFolderForKind("sid"));
        Directory.CreateDirectory(baseDir);
        var destName = $"{CsdbClassifier.SanitizeName(detail.Name)} (csdb-{detail.CsdbId}).sid";
        var dest = Path.Combine(baseDir, destName);
        if (File.Exists(dest) && !force)
            return new List<string> { dest };

        if (!string.IsNullOrWhiteSpace(detail.HvscPath))
        {
            var rel = detail.HvscPath.TrimStart('/').Replace('\\', '/');
            var src = Path.Combine(hvscRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(src))
            {
                // Prefer hardlink when available (saves disk); fall back to copy.
                if (File.Exists(dest))
                    File.Delete(dest);
                try
                {
                    if (OperatingSystem.IsWindows())
                        CreateHardLinkWindows(dest, src);
                    else
                        File.Copy(src, dest, overwrite: true);
                }
                catch
                {
                    File.Copy(src, dest, overwrite: true);
                }

                return new List<string> { dest };
            }
        }

        throw new InvalidOperationException(
            $"SID {detail.CsdbId} has no local HVSC file"
            + (detail.HvscPath is null ? "" : $" (looked for {detail.HvscPath})"));
    }

    [System.Runtime.InteropServices.DllImport("Kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    private static void CreateHardLinkWindows(string dest, string src)
    {
        if (!CreateHardLink(dest, src, IntPtr.Zero))
            throw new IOException($"CreateHardLink failed for {dest}");
    }
}
