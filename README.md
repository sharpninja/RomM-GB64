# RomM + GameBase64 (C64)

Self-hosted [RomM](https://github.com/rommapp/romm) stack for a local **GameBase64** collection under `./gb64`, with HVSC for SID path resolution, a CSDb bridge for scene search and selective library ingest, and **.NET 10 client libraries** for RomM REST plus client-side CSDb search/ingest.

| | |
|--|--|
| **Stack** | Docker Compose: RomM, MariaDB, CSDb bridge |
| **Languages** | **C#** and **PowerShell** only (no Python) |
| **Client libraries** | `RomM.Client`, `RomM.Client.Csdb` (`net10.0`, Nuke pack/publish) |
| **Repo** | Azure DevOps `RomM` (origin); GitHub mirror `RomM-GB64` |

## What you get

- **RomM** (`rommapp/romm`) with host **HVSC** bind-mounted at `/romm/library/hvsc` (same content model as `./gb64`)
- **CSDb bridge** (ASP.NET Core): full-catalog search, RSS recent-window index, selective ingest
- **.NET 10 clients**: typed RomM API (auth, platforms, ROMs, tasks/scan) and preferred **client-side CSDb** search/ingest into Structure A `roms/c64/` with optional RomM scan (no RomM server fork)
- **Gb64Import** C# tool: Structure A import, SID NFO fix, asset path validation
- **Archive extract** on bridge ingest: zip/7z/rar unpack into package folders under `roms/`
- **GameBase64** source tree (`Games`, `Screenshots`, `ROMs`) plus tag-mapping docs for RomM filenames
- **Polite CSDb usage**: no full-site dump; search is capped; download only explicit ids

## Repository layout

```text
├── README.md
├── docker-compose.yml          # romm, romm-db, csdb-bridge
├── Dockerfile                  # thin wrapper around rommapp/romm (no content bake-in)
├── RomM.Client.slnx            # client libraries + tests
├── build/                      # Nuke (PackNuGet, PublishNuGet)
├── build.ps1
├── .env.example                # secrets template (.env is gitignored)
├── openapi/romm-5.0.0.json     # pinned RomM OpenAPI snapshot
├── gb64/                       # GameBase64 source assets (gitignored)
├── hvsc/                       # HVSC source assets (gitignored; download once)
├── runtime/                    # mutable mounts (assets, config, library)
├── scripts/
│   ├── Download-Hvsc.ps1
│   ├── Prepare-RomMLibrary.ps1
│   ├── Resolve-SidPath.ps1
│   └── Resolve-ScreenshotPath.ps1
├── services/csdb-bridge/       # C# CSDb bridge (CsdbBridge.sln)
├── src/
│   ├── RomM.Client/            # RomM REST client (net10.0)
│   └── RomM.Client.Csdb/       # CSDb search + Structure A ingest
├── tests/RomM.Client.Tests/
├── tools/
│   ├── Gb64Import/
│   ├── Gb64Import.Tests/
│   └── HvscFetch/
└── docs/                       # architecture, CSDb, HVSC, GB64 mapping, client usage
```

## Prerequisites

- Docker Desktop / Docker Compose
- .NET 8 SDK (bridge, HVSC host tools)
- .NET 10 SDK (RomM.Client libraries and Nuke build)

## Quick start (local Docker)

### 1. Configure secrets

```powershell
Copy-Item .env.example .env
# Set at least:
#   DB_PASSWD
#   MARIADB_ROOT_PASSWORD
#   ROMM_AUTH_SECRET_KEY   # e.g. openssl rand -hex 32
```

### 2. Prepare host content trees

```powershell
# GameBase64: place or keep under ./gb64 (Games, Screenshots, ROMs)
# HVSC: download once to ./hvsc (same idea as gb64, not baked into the image)
.\scripts\Download-Hvsc.ps1
```

### 3. Start the stack

```powershell
docker compose up -d --build
docker compose ps
```

| Service | URL |
|---------|-----|
| RomM UI | http://localhost:8080 (`ROMM_PORT`) |
| CSDb bridge | http://localhost:8090 (`CSDB_BRIDGE_PORT`) |
| MariaDB | internal only (`romm-db`) |

Open RomM and complete the first-run admin wizard.

### 4. CSDb bridge (optional)

```powershell
# Full-catalog search (capped; not a dump)
Invoke-RestMethod "http://localhost:8090/csdb/v1/search?q=boulder&kinds=demo&kinds=crack&limit=20&source=live"

# RSS recent-window index refresh (metadata only)
Invoke-RestMethod -Method Post "http://localhost:8090/csdb/v1/index/refresh"

# Ingest selected ids only (archives extract into package folders)
$body = @{ items = @(@{ kind = "demo"; csdb_id = 263020 }) } | ConvertTo-Json
Invoke-RestMethod -Method Post "http://localhost:8090/csdb/v1/ingest" `
  -ContentType "application/json" -Body $body
```

If `CSDB_BRIDGE_API_KEY` is set, send header `X-Api-Key`.

After ingest, run a **RomM library scan** so new files under `roms/c64/` (tagged `(csdb-{id})`) appear in RomM.

### 5. HVSC helpers (host)

```powershell
.\scripts\Download-Hvsc.ps1
.\scripts\Resolve-SidPath.ps1 'MUSICIANS\W\Whittaker_David\180.sid' -HvscRoot .\hvsc
```

Optional archive URL override:

```powershell
$env:HVSC_URL = 'https://hvsc.brona.dk/HVSC/HVSC_85-all-of-them.7z'
.\scripts\Download-Hvsc.ps1
```

## Volume rules

RomM Structure A under one attached library volume. **Required game platforms:** `c64`, `c128`, `c-plus-4`, `vic-20`. Metadata path roots for GB64 NFO fields live **next to** `roms/` on the same mount.

| Host | Container | Notes |
|------|-----------|--------|
| `runtime/library` | `/romm/library` | Entire library parent (roms + hvsc + screenshots + bios) |
| `…/roms/c64` etc. | `/romm/library/roms/…` | Game packages |
| `…/hvsc` | `/romm/library/hvsc` | NFO `SID:` resolution (`Download-Hvsc.ps1`) |
| `…/screenshots` | `/romm/library/screenshots` | NFO `Screenshot:` resolution (synced from `gb64/Screenshots`) |
| `runtime/assets`, `runtime/config` | `/romm/assets`, `/romm/config` | Saves / config |
| named volumes | DB / resources / redis | Provider-fetched art cache, etc. |

```powershell
.\scripts\Download-Hvsc.ps1              # -> runtime/library/hvsc
.\scripts\Prepare-RomMLibrary.ps1        # platforms + screenshots sync + gated game import
.\scripts\Resolve-SidPath.ps1 'MUSICIANS\W\Whittaker_David\180.sid'
.\scripts\Resolve-ScreenshotPath.ps1 'A\Alfabug.png'
```

## Development

```powershell
# CSDb bridge unit tests
dotnet test .\services\csdb-bridge\CsdbBridge.sln -c Release

# RomM.Client + CSDb client unit tests (net10.0)
dotnet test .\RomM.Client.slnx -c Release

# HVSC fetch tool
dotnet build .\tools\HvscFetch\HvscFetch.csproj -c Release

# Gb64Import tool
dotnet test .\tools\Gb64Import.Tests\Gb64Import.Tests.csproj -c Release
```

### Client libraries (NuGet)

```powershell
# Pack RomM.Client and RomM.Client.Csdb into artifacts/nupkg
.\build.ps1 PackNuGet

# Publish to nuget.org (requires NUGET_API_KEY or nuget_api_key in the environment)
.\build.ps1 PublishNuGet
```

See [docs/romm-client/usage.md](docs/romm-client/usage.md) for API samples (auth, ROM list, CSDb selective ingest + scan).

## Documentation

| Doc | Description |
|-----|-------------|
| [docs/architecture/overview.md](docs/architecture/overview.md) | System architecture |
| [docs/csdb-romm-integration.md](docs/csdb-romm-integration.md) | CSDb bridge design, politeness rules, API |
| [docs/romm-client/usage.md](docs/romm-client/usage.md) | RomM.Client + RomM.Client.Csdb usage |
| [docs/hvsc-in-container.md](docs/hvsc-in-container.md) | Host HVSC tree (like GB64) and SID path mapping |
| [docs/gb64-romm-tag-mapping.md](docs/gb64-romm-tag-mapping.md) | GameBase64 `VERSION.NFO` → RomM tags |
| [docs/wiki.yaml](docs/wiki.yaml) | Wiki export manifest |

## GameBase64 notes

- Games: `gb64/Games/{bucket}/*.zip` (letter buckets; do not mount as RomM platform folders as-is).
- Screenshots: `gb64/Screenshots/`; best join key is NFO `Screenshot:`.
- SID paths in NFO are HVSC-relative (`MUSICIANS\…`, `GAMES\…`).
- RomM Structure A expects `roms/c64/…` for game media after extract/organize.

## License and content

Upstream RomM is AGPLv3. GameBase64, HVSC, and CSDb content are third-party: only host material you are allowed to store; keep `./gb64` and `./hvsc` private and out of redistributed images.
