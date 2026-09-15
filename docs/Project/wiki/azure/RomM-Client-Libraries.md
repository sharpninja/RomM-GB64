# RomM.Client usage

## RomM REST (`RomM.Client`)

```csharp
await using var romm = RomMClient.Create(new RomMClientOptions
{
    BaseAddress = new Uri("http://localhost:8080/"),
    Auth = RomMAuth.ClientApiToken(Environment.GetEnvironmentVariable("ROMM_TOKEN")!),
});

var hb = await romm.System.GetHeartbeatAsync();
var platforms = await romm.Platforms.ListAsync();
await foreach (var rom in romm.Roms.EnumerateAsync(new RomListQuery { SearchTerm = "Boulder", Limit = 50 }))
    Console.WriteLine(rom.Name);
await romm.Tasks.ScanLibraryAsync();
```

Heartbeat JSON follows RomM 5.0.0: `SYSTEM.VERSION` and `SYSTEM.SHOW_SETUP_WIZARD`. Credentials (Bearer, Basic, OAuth) are sent only to the configured `BaseAddress` host. Absolute URLs on other hosts do not receive `Authorization`. Remaining OpenAPI paths are reachable via `romm.Transport.SendAsync(...)` (explicit transport allowlist; unknown `/api/*` paths are not auto-covered).

## CSDb client-side (`RomM.Client.Csdb`)

Search CSDb, ingest **selected** ids only into Structure A `roms/c64/`, then optional RomM scan (shared filesystem with RomM).

```csharp
var lib = new CsdbLibraryOptions { LibraryRomsRoot = @"C:\deploy\RomM-clean\library\roms", HvscRoot = @"...\hvsc" };
using var csdb = CsdbClient.Create(lib);
var hits = await csdb.SearchAsync(new CsdbSearchRequest("boulder", new[] { CsdbKind.Demo }, Limit: 20));
var pick = hits.Take(1).Select(h => new CsdbSelection(h.CsdbId, h.Kind)).ToList();
var writer = new CsdbLibraryWriter(csdb, lib);
var workflow = new CsdbRomMWorkflow(writer, romm);
await workflow.IngestSelectedAsync(pick, scanAfterIngest: true);
```

Policy: max search limit 50; max ingest batch 20; never bulk-all-hits; platform always `c64`.
