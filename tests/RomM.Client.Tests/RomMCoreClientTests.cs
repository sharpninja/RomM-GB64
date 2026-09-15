using System.Net;
using System.Text;
using System.Text.Json;
using RomM.Client;
using RomM.Client.Auth;
using RomM.Client.Models;

namespace RomM.Client.Tests;

public sealed class RomMCoreClientTests
{
    [Fact]
    public async Task Heartbeat_deserializes_via_shipped_client()
    {
        var handler = new ScriptedHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith("/api/heartbeat", req.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Json(new
            {
                SYSTEM = new { VERSION = "5.0.0", SHOW_SETUP_WIZARD = false },
                METADATA_SOURCES = new { },
                FILESYSTEM = new { },
                EMULATION = new { },
                FRONTEND = new { },
                OIDC = new { },
                TASKS = new { },
            });
        });

        await using var client = RomMClient.Create(
            new RomMClientOptions
            {
                BaseAddress = new Uri("http://romm.test/"),
                Auth = RomMAuth.ClientApiToken("rmm_testtoken"),
            },
            handler);

        var hb = await client.System.GetHeartbeatAsync();
        Assert.Equal("5.0.0", hb.Version);
        Assert.False(hb.ShowSetupWizard);
    }

    [Fact]
    public async Task Heartbeat_top_level_VERSION_is_not_the_live_contract()
    {
        var handler = new ScriptedHandler(_ => Json(new { VERSION = "5.0.0", SHOW_SETUP_WIZARD = true }));
        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        var hb = await client.System.GetHeartbeatAsync();
        Assert.Null(hb.Version);
        Assert.Null(hb.ShowSetupWizard);
    }

    [Fact]
    public async Task Platforms_List_and_Get_use_shipped_clients()
    {
        var handler = new ScriptedHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/platforms", StringComparison.Ordinal))
            {
                return Json(new[]
                {
                    new { id = 7, slug = "c64", fs_slug = "c64", name = "Commodore 64", display_name = "C64", rom_count = 12 },
                });
            }

            if (path.EndsWith("/api/platforms/7", StringComparison.Ordinal))
            {
                return Json(new { id = 7, fs_slug = "c64", display_name = "C64" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        var list = await client.Platforms.ListAsync();
        Assert.Single(list);
        Assert.Equal("c64", list[0].FsSlug);
        var one = await client.Platforms.GetAsync(7);
        Assert.Equal(7, one.Id);
    }

    [Fact]
    public async Task Roms_Get_returns_detail_and_404()
    {
        var handler = new ScriptedHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/roms/11", StringComparison.Ordinal))
            {
                return Json(new { id = 11, name = "Elite", summary = "space", fs_name = "elite.d64" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("missing"),
            };
        });

        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        var detail = await client.Roms.GetAsync(11);
        Assert.Equal(11, detail.Id);
        Assert.Equal("Elite", detail.Name);
        await Assert.ThrowsAsync<RomMApiException>(() => client.Roms.GetAsync(99));
    }

    [Fact]
    public async Task Roms_List_builds_query_and_Enumerate_stops()
    {
        var offsets = new List<string?>();
        var handler = new ScriptedHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            var q = req.RequestUri!.Query;
            Assert.Contains("search_term=Boulder", q);
            Assert.Contains("platform_ids=3", q);
            Assert.Contains("limit=2", q);
            string? offset = null;
            foreach (var part in q.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("offset=", StringComparison.Ordinal))
                {
                    offset = part["offset=".Length..];
                    break;
                }
            }

            offsets.Add(offset);
            if (offset is null or "0")
            {
                return Json(new
                {
                    items = new[]
                    {
                        new { id = 1, name = "Boulder A", platform_id = 3 },
                        new { id = 2, name = "Boulder B", platform_id = 3 },
                    },
                    total = 3,
                    limit = 2,
                    offset = 0,
                });
            }

            Assert.Equal("2", offset);
            return Json(new
            {
                items = new[] { new { id = 3, name = "Boulder C", platform_id = 3 } },
                total = 3,
                limit = 2,
                offset = 2,
            });
        });

        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        var page = await client.Roms.ListAsync(new RomListQuery
        {
            SearchTerm = "Boulder",
            PlatformIds = new[] { 3 },
            Limit = 2,
            Offset = 0,
        });
        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.Items.Count);

        var all = new List<SimpleRomSchema>();
        await foreach (var rom in client.Roms.EnumerateAsync(new RomListQuery
        {
            SearchTerm = "Boulder",
            PlatformIds = new[] { 3 },
            Limit = 2,
        }))
        {
            all.Add(rom);
        }

        Assert.Equal(3, all.Count);
        Assert.Contains("2", offsets);
    }

    [Fact]
    public async Task Enumerate_continues_when_total_is_omitted()
    {
        var calls = 0;
        var handler = new ScriptedHandler(req =>
        {
            calls++;
            var q = req.RequestUri!.Query;
            if (q.Contains("offset=2", StringComparison.Ordinal))
            {
                return Json(new
                {
                    items = new[] { new { id = 3, name = "C" } },
                    limit = 2,
                    offset = 2,
                });
            }

            return Json(new
            {
                items = new[]
                {
                    new { id = 1, name = "A" },
                    new { id = 2, name = "B" },
                },
                limit = 2,
                offset = 0,
            });
        });

        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        var all = new List<SimpleRomSchema>();
        await foreach (var rom in client.Roms.EnumerateAsync(new RomListQuery { Limit = 2 }))
        {
            all.Add(rom);
        }

        Assert.Equal(3, all.Count);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Enumerate_does_not_yield_after_cancellation()
    {
        var handler = new ScriptedHandler(_ => Json(new
        {
            items = new[]
            {
                new { id = 1, name = "A" },
                new { id = 2, name = "B" },
            },
            total = 100,
            limit = 2,
            offset = 0,
        }));

        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        using var cts = new CancellationTokenSource();
        var count = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.Roms.EnumerateAsync(new RomListQuery { Limit = 2 }, cts.Token))
            {
                count++;
                cts.Cancel();
            }
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DownloadContent_uses_ResponseHeadersRead_path()
    {
        var handler = new ScriptedHandler(req =>
        {
            Assert.Contains("/api/roms/9/content/", req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("ROMDATA"))
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream") },
                },
            };
        });

        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        await using var stream = await client.Roms.DownloadContentAsync(9, "game.d64");
        using var reader = new StreamReader(stream);
        Assert.Equal("ROMDATA", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Tasks_Run_Wait_and_ScanLibrary_use_shipped_Tasks_client()
    {
        var ranScan = false;
        var polled = 0;
        var handler = new ScriptedHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/tasks", StringComparison.Ordinal) && req.Method == HttpMethod.Get)
            {
                return Json(new[] { new { name = "scan", title = "Scan library" } });
            }

            if (path.Contains("/api/tasks/run/scan", StringComparison.Ordinal) && req.Method == HttpMethod.Post)
            {
                ranScan = true;
                return Json(new { task_id = "t-1", status = "running" });
            }

            if (path.EndsWith("/api/tasks/t-1", StringComparison.Ordinal))
            {
                polled++;
                return Json(new { task_id = "t-1", status = polled >= 2 ? "completed" : "running" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        await client.Tasks.ScanLibraryAsync();
        Assert.True(ranScan);
        Assert.True(polled >= 2);
    }

    [Fact]
    public async Task Task_status_finished_is_terminal_success()
    {
        var handler = new ScriptedHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("/api/tasks/run/", StringComparison.Ordinal))
            {
                return Json(new { task_name = "scan_library", task_id = "job-9", status = "queued" });
            }

            if (path.Contains("/api/tasks/job-9", StringComparison.Ordinal))
            {
                return Json(new { task_name = "scan_library", task_id = "job-9", status = "finished" });
            }

            if (path.EndsWith("/api/tasks", StringComparison.Ordinal))
            {
                return Json(new[] { new { name = "scan_library", title = "Scan", description = "d", type = "scan" } });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        var run = await client.Tasks.RunAsync("scan_library");
        Assert.Equal("job-9", run.ResolveTaskId());
        await client.Tasks.WaitAsync("job-9", TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("stopped")]
    [InlineData("canceled")]
    public async Task Task_status_failed_stopped_canceled_are_terminal_failure(string status)
    {
        var handler = new ScriptedHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("/api/tasks/run/", StringComparison.Ordinal))
            {
                return Json(new { task_name = "scan_library", task_id = "job-fail", status = "queued" });
            }

            if (path.Contains("/api/tasks/job-fail", StringComparison.Ordinal))
            {
                return Json(new { task_name = "scan_library", task_id = "job-fail", status });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        await Assert.ThrowsAsync<RomMApiException>(() =>
            client.Tasks.WaitAsync("job-fail", TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Transport_SendAsync_maps_401_via_shipped_pipeline()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("nope"),
        });
        await using var client = RomMClient.Create(new Uri("http://romm.test/"), handler: handler);
        await Assert.ThrowsAsync<RomMAuthException>(() =>
            client.Transport.GetJsonAsync<HeartbeatResponse>("api/heartbeat"));
    }

    private static HttpResponseMessage Json(object body)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _impl;

        public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> impl) => _impl = impl;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_impl(request));
    }
}
