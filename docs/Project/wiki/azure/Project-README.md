# RomM + GameBase64 (C64)

Self-hosted [RomM](https://github.com/rommapp/romm) stack oriented around a local **GameBase64** collection under `./gb64`, with:

- **HVSC** (High Voltage SID Collection) embedded in the RomM image for SID path resolution
- **CSDb bridge** (C# ASP.NET Core) for full-catalog search, RSS recent-window index, and **selective** download into `roms/`
- **PowerShell** host helpers (no Python)

Implementation language policy: **C# or PowerShell only**.

## Features

| Area | Status |
|------|--------|
| Docker Compose: RomM + MariaDB | Shipped (`docker-compose.yml`) |
| Custom RomM image with HVSC at `/romm/library/hvsc` | Shipped (`Dockerfile` + `tools/HvscFetch`) |
| GameBase64 assets under `./gb64` (Games, Screenshots, ROMs) | Present on disk; library reorg for Structure A is planned |
| GB64 `VERSION.NFO` → RomM filename tag mapping | Documented (`docs/gb64-romm-tag-mapping.md`) |
| CSDb full-catalog search + selective ingest | Shipped (`services/csdb-bridge`) |
| Archive extract on CSDb ingest (zip/7z/rar → folder under `roms/`) | Shipped |
| Polite CSDb usage (no full dump / no bulk auto-download) | Documented + enforced in API caps |

## Repository layout

```text
├── docker-compose.yml          # romm, romm-db, csdb-bridge
├── Dockerfile                  # rommapp/romm + HVSC embed (C# HvscFetch)
├── .env.example                # secrets template (.env is gitignored)
├── gb64/                       # GameBase64 source (Games, Screenshots, ROMs)
├── runtime/                    # host mounts (assets, config, csdb index, roms)
├── scripts/
│   ├── Download-Hvsc.ps1       # host HVSC download via C# tool
│   └── Resolve-SidPath.ps1     # resolve NFO SID: paths
├── services/csdb-bridge/       # C# ASP.NET Core CSDb bridge
├── tools/HvscFetch/            # C# HVSC download + normalize
└── docs/                       # design and mapping docs
```

## Quick start

### Prerequisites

- Docker / Docker Compose
- .NET 8 SDK (for host builds and HVSC host download)
- Optional: CSDb account in `.env` (not required for public search/download)

### Configure

```powershell
Copy-Item .env.example .env
# Edit .env: DB_PASSWD, MARIADB_ROOT_PASSWORD, ROMM_AUTH_SECRET_KEY
# openssl rand -hex 32  (or equivalent) for ROMM_AUTH_SECRET_KEY
```

### Run stack

```powershell
docker compose build
docker compose up -d
```

| Service | URL / port |
|---------|------------|
| RomM UI | http://localhost:8080 (or `ROMM_PORT`) |
| CSDb bridge | http://localhost:8090 (or `CSDB_BRIDGE_PORT`) |
| MariaDB | internal `romm-db` |

First RomM start: open the UI and complete the admin setup wizard.

### CSDb bridge examples

```powershell
# Full-catalog search (capped; not a dump)
Invoke-RestMethod "http://localhost:8090/csdb/v1/search?q=boulder&kinds=demo&kinds=crack&limit=20&source=live"

# Refresh RSS recent-window index (metadata only)
Invoke-RestMethod -Method Post "http://localhost:8090/csdb/v1/index/refresh"

# Ingest only selected ids (archives extract into package folders)
$body = @{ items = @(@{ kind = "demo"; csdb_id = 263020 }) } | ConvertTo-Json
Invoke-RestMethod -Method Post "http://localhost:8090/csdb/v1/ingest" -ContentType "application/json" -Body $body
```

If `CSDB_BRIDGE_API_KEY` is set, send header `X-Api-Key`.

After ingest, run a **RomM library scan** so new files under `roms/c64-csdb-*` appear in RomM search.

### HVSC on the host (optional)

```powershell
.\scripts\Download-Hvsc.ps1 -Dest .\runtime\hvsc
.\scripts\Resolve-SidPath.ps1 'MUSICIANS\W\Whittaker_David\180.sid' -HvscRoot .\runtime\hvsc
```

Docker image build embeds HVSC via `tools/HvscFetch` (override with build-arg `HVSC_URL` if needed).

## Important volume rules

- **Do not** bind-mount a host path over entire `/romm/library` on the RomM container (hides embedded HVSC).
- CSDb packages use subpath mounts: `runtime/library/roms/c64-csdb-*` → `/romm/library/roms/c64-csdb-*`.
- Mutable RomM data: `runtime/assets`, `runtime/config`, named volumes for DB/resources/redis.

## Development

```powershell
# CSDb bridge tests
dotnet test .\services\csdb-bridge\CsdbBridge.sln -c Release

# HVSC tool build
dotnet build .\tools\HvscFetch\HvscFetch.csproj -c Release
```

## Documentation

| Doc | Description |
|-----|-------------|
| [docs/architecture/overview.md](docs/architecture/overview.md) | System architecture |
| [docs/csdb-romm-integration.md](docs/csdb-romm-integration.md) | CSDb bridge design, politeness policy, API |
| [docs/hvsc-in-container.md](docs/hvsc-in-container.md) | HVSC embed layout and SID path mapping |
| [docs/gb64-romm-tag-mapping.md](docs/gb64-romm-tag-mapping.md) | GameBase64 VERSION.NFO → RomM tags |
| [docs/wiki.yaml](docs/wiki.yaml) | Wiki export manifest |

## GameBase64 notes

- Games live under `gb64/Games/{letter-bucket}/*.zip` (~29k packages).
- Screenshots under `gb64/Screenshots/`; primary link is NFO `Screenshot:` field.
- SID paths in NFO are HVSC-relative (`MUSICIANS\…`, `GAMES\…`).
- RomM expects Structure A (`roms/c64/…`); letter buckets must not be mounted as platform folders as-is.

## License / content

RomM is AGPLv3 (upstream). GameBase64, HVSC, and CSDb material are third-party collections: host only content you are allowed to store; do not redistribute custom images containing full archives without rights.
