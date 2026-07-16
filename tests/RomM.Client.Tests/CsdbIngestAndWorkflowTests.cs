using System.Net;
using System.Text;
using RomM.Client;
using RomM.Client.Csdb;

namespace RomM.Client.Tests;

public sealed class CsdbIngestAndWorkflowTests
{
    [Fact]
    public async Task Ingest_writes_only_under_c64_with_csdb_tag()
    {
        var root = Path.Combine(Path.GetTempPath(), "romm-csdb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var xml = ReleaseXml("Cool Demo", "https://csdb.dk/file.d64");
            var client = new FakeCsdbClient(xml, fileBytes: Encoding.UTF8.GetBytes("DISK"));
            var opts = new CsdbLibraryOptions { LibraryRomsRoot = root, MinRequestInterval = TimeSpan.Zero };
            var writer = new CsdbLibraryWriter(client, opts);

            var result = await writer.IngestAsync(new[] { new CsdbSelection(55, CsdbKind.Demo) });
            Assert.Equal(1, result.Requested);
            Assert.Equal("ok", result.Items[0].Status);
            Assert.All(result.Items[0].Paths, p =>
            {
                Assert.Contains($"{Path.DirectorySeparatorChar}c64{Path.DirectorySeparatorChar}", p);
                Assert.DoesNotContain("c64-csdb", p);
                Assert.Contains("csdb-55", p);
            });
            Assert.Contains(Directory.GetDirectories(Path.Combine(root, "c64")), d => d.Contains("csdb-55"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Ingest_empty_selection_throws()
    {
        var opts = new CsdbLibraryOptions { LibraryRomsRoot = Path.GetTempPath() };
        var writer = new CsdbLibraryWriter(new FakeCsdbClient("", Array.Empty<byte>()), opts);
        await Assert.ThrowsAsync<ArgumentException>(() => writer.IngestAsync(Array.Empty<CsdbSelection>()));
    }

    [Fact]
    public async Task Ingest_over_MaxIngestBatch_throws()
    {
        var opts = new CsdbLibraryOptions { LibraryRomsRoot = Path.GetTempPath(), MaxIngestBatch = 2 };
        var writer = new CsdbLibraryWriter(new FakeCsdbClient("", Array.Empty<byte>()), opts);
        var sels = Enumerable.Range(1, 3).Select(i => new CsdbSelection(i, CsdbKind.Demo)).ToList();
        await Assert.ThrowsAsync<CsdbPolitenessException>(() => writer.IngestAsync(sels));
    }

    [Fact]
    public async Task Ingest_skips_when_exists_unless_force()
    {
        var root = Path.Combine(Path.GetTempPath(), "romm-csdb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var client = new FakeCsdbClient(ReleaseXml("Dup", "https://csdb.dk/a.bin"), Encoding.UTF8.GetBytes("A"));
            var opts = new CsdbLibraryOptions { LibraryRomsRoot = root, MinRequestInterval = TimeSpan.Zero };
            var writer = new CsdbLibraryWriter(client, opts);
            var first = await writer.IngestAsync(new[] { new CsdbSelection(7, CsdbKind.Demo) });
            Assert.Equal("ok", first.Items[0].Status);
            client.DownloadCount = 0;
            var second = await writer.IngestAsync(new[] { new CsdbSelection(7, CsdbKind.Demo) });
            Assert.Equal("ok", second.Items[0].Status);
            Assert.Equal(0, client.DownloadCount); // skipped without re-download
            client.DownloadCount = 0;
            var forced = await writer.IngestAsync(
                new[] { new CsdbSelection(7, CsdbKind.Demo) },
                new CsdbIngestOptions { Force = true });
            Assert.Equal("ok", forced.Items[0].Status);
            Assert.Equal(1, client.DownloadCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Sid_ingest_uses_HVSC_when_present()
    {
        var root = Path.Combine(Path.GetTempPath(), "romm-csdb-" + Guid.NewGuid().ToString("N"));
        var hvsc = Path.Combine(root, "hvsc");
        var roms = Path.Combine(root, "roms");
        Directory.CreateDirectory(Path.Combine(hvsc, "MUSICIANS", "T"));
        var sidSrc = Path.Combine(hvsc, "MUSICIANS", "T", "tune.sid");
        await File.WriteAllBytesAsync(sidSrc, Encoding.UTF8.GetBytes("SIDBYTES"));
        try
        {
            var client = new FakeCsdbClient(
                releaseXml: "",
                fileBytes: Array.Empty<byte>(),
                sidXml: SidXml("Tune", "/MUSICIANS/T/tune.sid"));
            var opts = new CsdbLibraryOptions
            {
                LibraryRomsRoot = roms,
                HvscRoot = hvsc,
                MinRequestInterval = TimeSpan.Zero,
            };
            var writer = new CsdbLibraryWriter(client, opts);
            var result = await writer.IngestAsync(new[] { new CsdbSelection(9, CsdbKind.Sid) });
            Assert.Equal("ok", result.Items[0].Status);
            var path = result.Items[0].Paths[0];
            Assert.Contains($"{Path.DirectorySeparatorChar}c64{Path.DirectorySeparatorChar}", path);
            Assert.Contains("(SID)", path);
            Assert.Contains("csdb-9", path);
            Assert.Equal("SIDBYTES", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Workflow_scans_only_after_successful_ingest()
    {
        var root = Path.Combine(Path.GetTempPath(), "romm-csdb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var csdb = new FakeCsdbClient(ReleaseXml("W", "https://csdb.dk/w.bin"), Encoding.UTF8.GetBytes("W"));
            var writer = new CsdbLibraryWriter(csdb, new CsdbLibraryOptions { LibraryRomsRoot = root, MinRequestInterval = TimeSpan.Zero });
            var scanCalls = 0;
            var rommHandler = new ScriptedHandler(req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (path.EndsWith("/api/tasks") && req.Method == HttpMethod.Get)
                {
                    return Json(new[] { new { name = "scan" } });
                }

                if (path.Contains("/api/tasks/run/scan"))
                {
                    scanCalls++;
                    return Json(new { task_id = "s1", status = "completed" });
                }

                if (path.EndsWith("/api/tasks/s1"))
                {
                    return Json(new { task_id = "s1", status = "completed" });
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });
            await using var romm = RomMClient.Create(new Uri("http://romm.test/"), handler: rommHandler);
            var workflow = new CsdbRomMWorkflow(writer, romm);

            var ok = await workflow.IngestSelectedAsync(new[] { new CsdbSelection(1, CsdbKind.Demo) }, scanAfterIngest: true);
            Assert.True(ok.ScanRequested);
            Assert.True(ok.ScanCompleted);
            Assert.Equal(1, scanCalls);

            // All-fail path: no scan
            var failClient = new FakeCsdbClient(ReleaseXml("X", ""), Array.Empty<byte>(), failDownloads: true);
            var failWriter = new CsdbLibraryWriter(failClient, new CsdbLibraryOptions { LibraryRomsRoot = root, MinRequestInterval = TimeSpan.Zero });
            scanCalls = 0;
            var workflow2 = new CsdbRomMWorkflow(failWriter, romm);
            // Download links empty -> error status
            var badXmlClient = new FakeCsdbClient(
                """
                <?xml version="1.0"?><CSDbResponse><Release><Name>X</Name><Type>Demo</Type><DownloadLinks/></Release></CSDbResponse>
                """,
                Array.Empty<byte>());
            var badWriter = new CsdbLibraryWriter(badXmlClient, new CsdbLibraryOptions { LibraryRomsRoot = root, MinRequestInterval = TimeSpan.Zero });
            var workflow3 = new CsdbRomMWorkflow(badWriter, romm);
            var failed = await workflow3.IngestSelectedAsync(new[] { new CsdbSelection(2, CsdbKind.Demo) }, scanAfterIngest: true);
            Assert.All(failed.Ingest.Items, i => Assert.Equal("error", i.Status));
            // anyOk is false for only errors - scan should not run
            // Wait - error status means anyOk is false because we check ok or skipped only
            Assert.False(failed.ScanRequested);
            Assert.Equal(0, scanCalls);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static string ReleaseXml(string name, string link) => $"""
        <?xml version="1.0"?>
        <CSDbResponse>
          <Release>
            <Name>{name}</Name>
            <Type>C64 Demo</Type>
            <DownloadLinks>
              <DownloadLink>
                <Link>{link}</Link>
                <Status>Ok</Status>
              </DownloadLink>
            </DownloadLinks>
          </Release>
        </CSDbResponse>
        """;

    private static string SidXml(string name, string hvsc) => $"""
        <?xml version="1.0"?>
        <CSDbResponse>
          <SID>
            <Name>{name}</Name>
            <HVSCPath>{hvsc}</HVSCPath>
          </SID>
        </CSDbResponse>
        """;

    private static HttpResponseMessage Json(object body)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class FakeCsdbClient : ICsdbClient
    {
        private readonly string _releaseXml;
        private readonly string? _sidXml;
        private readonly byte[] _bytes;
        private readonly bool _failDownloads;

        public int DownloadCount { get; set; }

        public FakeCsdbClient(string releaseXml, byte[] fileBytes, bool failDownloads = false, string? sidXml = null)
        {
            _releaseXml = releaseXml;
            _bytes = fileBytes;
            _failDownloads = failDownloads;
            _sidXml = sidXml;
        }

        public Task<IReadOnlyList<CsdbSearchHit>> SearchAsync(CsdbSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CsdbSearchHit>>(Array.Empty<CsdbSearchHit>());

        public Task<CsdbReleaseDetail> GetReleaseAsync(int csdbId, CancellationToken cancellationToken = default)
        {
            // Parse via real client path: minimal manual parse
            var doc = System.Xml.Linq.XDocument.Parse(_releaseXml);
            var rel = doc.Root!.Element("Release")!;
            var links = rel.Element("DownloadLinks")?.Elements("DownloadLink")
                .Select(dl => new CsdbDownloadLink(
                    (dl.Element("Link")?.Value ?? "").Trim(),
                    (dl.Element("Status")?.Value ?? "Ok").Trim()))
                .Where(l => l.Url.Length > 0)
                .ToList() ?? new List<CsdbDownloadLink>();
            return Task.FromResult(new CsdbReleaseDetail(
                csdbId,
                (rel.Element("Name")?.Value ?? "x").Trim(),
                (rel.Element("Type")?.Value ?? "").Trim(),
                links));
        }

        public Task<CsdbSidDetail> GetSidAsync(int csdbId, CancellationToken cancellationToken = default)
        {
            var doc = System.Xml.Linq.XDocument.Parse(_sidXml!);
            var sid = doc.Root!.Element("SID")!;
            return Task.FromResult(new CsdbSidDetail(
                csdbId,
                (sid.Element("Name")?.Value ?? "s").Trim(),
                sid.Element("HVSCPath")?.Value?.Trim()));
        }

        public Task<(byte[] Data, string? FileName)> DownloadBytesAsync(string url, CancellationToken cancellationToken = default)
        {
            if (_failDownloads || string.IsNullOrEmpty(url))
            {
                throw new CsdbException("download failed");
            }

            DownloadCount++;
            return Task.FromResult<(byte[] Data, string? FileName)>((_bytes, "file.bin"));
        }
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _impl;
        public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> impl) => _impl = impl;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_impl(request));
    }
}
