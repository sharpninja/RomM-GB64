namespace RomM.Client.Csdb;

public interface ICsdbLibraryWriter
{
    Task<CsdbIngestResult> IngestAsync(
        IReadOnlyList<CsdbSelection> selections,
        CsdbIngestOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class CsdbLibraryWriter : ICsdbLibraryWriter
{
    private readonly ICsdbClient _client;
    private readonly CsdbLibraryOptions _options;

    public CsdbLibraryWriter(ICsdbClient client, CsdbLibraryOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public async Task<CsdbIngestResult> IngestAsync(
        IReadOnlyList<CsdbSelection> selections,
        CsdbIngestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CsdbIngestOptions();
        if (selections is null || selections.Count == 0)
        {
            throw new ArgumentException("Selections must contain at least one item.", nameof(selections));
        }

        if (selections.Count > _options.MaxIngestBatch)
        {
            throw new CsdbPolitenessException(
                $"Ingest batch size {selections.Count} exceeds MaxIngestBatch {_options.MaxIngestBatch}.");
        }

        var jobId = options.JobId ?? $"job-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var romsRoot = Path.GetFullPath(_options.LibraryRomsRoot);
        Directory.CreateDirectory(romsRoot);
        var items = new List<CsdbIngestItemResult>();

        foreach (var sel in selections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var paths = sel.Kind == CsdbKind.Sid
                    ? await IngestSidAsync(sel, romsRoot, options.Force, cancellationToken).ConfigureAwait(false)
                    : await IngestReleaseAsync(sel, romsRoot, options.Force, cancellationToken).ConfigureAwait(false);
                items.Add(new CsdbIngestItemResult(
                    sel.CsdbId, sel.Kind, paths.Title,
                    paths.Paths.Count > 0 ? "ok" : "skipped",
                    paths.Paths));
            }
            catch (Exception ex)
            {
                items.Add(new CsdbIngestItemResult(
                    sel.CsdbId, sel.Kind, $"csdb-{sel.CsdbId}", "error",
                    Array.Empty<string>(), ex.Message));
            }
        }

        return new CsdbIngestResult(jobId, selections.Count, items);
    }

    private async Task<(string Title, List<string> Paths)> IngestReleaseAsync(
        CsdbSelection sel, string romsRoot, bool force, CancellationToken ct)
    {
        var detail = await _client.GetReleaseAsync(sel.CsdbId, ct).ConfigureAwait(false);
        var kind = sel.Kind == CsdbKind.Other
            ? CsdbClassifier.ParseKind(CsdbClassifier.ClassifyReleaseType(detail.CsdbType))
            : sel.Kind;
        var folder = CsdbClassifier.PlatformFolderForKind(kind);
        var baseDir = Path.Combine(romsRoot, folder);
        Directory.CreateDirectory(baseDir);
        var package = CsdbClassifier.PackageBaseName(detail.Name, kind, detail.CsdbId);
        var destDir = Path.Combine(baseDir, package);
        if (Directory.Exists(destDir) && !force)
        {
            return (detail.Name, Directory.GetFiles(destDir, "*", SearchOption.AllDirectories).ToList());
        }

        var okLinks = detail.DownloadLinks
            .Where(d => d.Status.Equals("Ok", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.Url))
            .ToList();
        if (okLinks.Count == 0)
        {
            throw new CsdbException($"No Ok download links for release {detail.CsdbId}");
        }

        if (force && Directory.Exists(destDir))
        {
            Directory.Delete(destDir, recursive: true);
        }

        Directory.CreateDirectory(destDir);
        var idx = 0;
        foreach (var link in okLinks)
        {
            var (data, fileName) = await _client.DownloadBytesAsync(link.Url, ct).ConfigureAwait(false);
            fileName = CsdbClassifier.SanitizeName(fileName ?? $"file-{++idx}.bin", 180);
            var outPath = Path.Combine(destDir, fileName);
            await File.WriteAllBytesAsync(outPath, data, ct).ConfigureAwait(false);
        }

        return (detail.Name, Directory.GetFiles(destDir, "*", SearchOption.AllDirectories).ToList());
    }

    private async Task<(string Title, List<string> Paths)> IngestSidAsync(
        CsdbSelection sel, string romsRoot, bool force, CancellationToken ct)
    {
        var detail = await _client.GetSidAsync(sel.CsdbId, ct).ConfigureAwait(false);
        var baseDir = Path.Combine(romsRoot, CsdbClassifier.PlatformFolderForKind(CsdbKind.Sid));
        Directory.CreateDirectory(baseDir);
        var destName = $"{CsdbClassifier.PackageBaseName(detail.Name, CsdbKind.Sid, detail.CsdbId)}.sid";
        var dest = Path.Combine(baseDir, destName);
        if (File.Exists(dest) && !force)
        {
            return (detail.Name, new List<string> { dest });
        }

        if (!string.IsNullOrWhiteSpace(detail.HvscPath) && !string.IsNullOrWhiteSpace(_options.HvscRoot))
        {
            var rel = detail.HvscPath.TrimStart('/').Replace('\\', '/');
            var src = Path.Combine(_options.HvscRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(src))
            {
                if (File.Exists(dest))
                {
                    File.Delete(dest);
                }

                File.Copy(src, dest, overwrite: true);
                return (detail.Name, new List<string> { dest });
            }
        }

        throw new CsdbException(
            $"SID {detail.CsdbId} has no local HVSC file"
            + (detail.HvscPath is null ? "" : $" (looked for {detail.HvscPath})"));
    }
}
