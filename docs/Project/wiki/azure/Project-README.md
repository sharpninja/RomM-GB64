# RomM + GameBase64 (C64)

Self-hosted [RomM](https://github.com/rommapp/romm) stack for a local **GameBase64** collection under `./gb64`, with HVSC for SID path resolution and a CSDb bridge for scene search and selective library ingest.

| | |
|--|--|
| **Stack** | Docker Compose: RomM, MariaDB, CSDb bridge |
| **Languages** | **C#** and **PowerShell** only (no Python) |
| **Repo** | `https://github.com/sharpninja/RomM-GB64.git` (`master`) |

## What you get

- **RomM** (`rommapp/romm`) with host **HVSC** bind-mounted at `/romm/library/hvsc` (same content model as `./gb64`)
- **CSDb bridge** (ASP.NET Core): full-catalog search, RSS recent-window index, selective ingest
- **Archive extract** on ingest: zip/7z/rar unpack into package folders under `roms/`
- **GameBase64** source tree (`Games`, `Screenshots`, `ROMs`) plus tag-mapping docs for RomM filenames
- **Polite CSDb usage**: no full-site dump; search is capped; download only explicit ids

## Repository layout

```text
├── README.md
├── docker-compose.yml          # romm, romm-db, csdb-bridge
├── Dockerfile                  # thin wrapper around rommapp/romm (no content bake-in)
├── .env.example                # secrets template (.env is gitignored)
├── gb64/                       # GameBase64 source assets (gitignored)
├── hvsc/                       # HVSC source assets (gitignored; download once)
├── runtime/                    # mutable mounts (assets, config, CSDb packages)
├── scripts/
│   ├── Download-Hvsc.ps1
│   └── Resolve-SidPath.ps1
├── services/csdb-bridge/       # C# CSDb bridge (CsdbBridge.sln)
├── tools/HvscFetch/            # C# HVSC download + normalize (host)
└── docs/                       # architecture, CSDb, HVSC, GB64 mapping
```

## Prerequisites

- Docker Desktop / Docker Compose
- .NET 8 SDK (HVSC host download via `tools/HvscFetch`)

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

After ingest, run a **RomM library scan** so `roms/c64-csdb-*` entries appear in RomM.

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

RomM Structure A: `/romm/library/roms/{platform}/` with **built-in** Commodore slugs. **Required libraries:** `c64`, `c128`, `c-plus-4`, `vic-20` (exact names; not `plus4` / `vic20`). C64 games (GB64 + CSDb) go under **`roms/c64/`**; other machines use their own roots.

| Host | Container | Notes |
|------|-----------|--------|
| `./hvsc` | `/romm/library/hvsc` (ro) | Host SID collection (not a platform under `roms/`) |
| `runtime/library/roms` | `/romm/library/roms` | Structure A platforms (`c64/`, …); persists across recreates |
| `runtime/assets`, `runtime/config` | `/romm/assets`, `/romm/config` | Mutable RomM state |
| named volumes | DB / resources / redis | — |

## Development

```powershell
# CSDb bridge unit tests
dotnet test .\services\csdb-bridge\CsdbBridge.sln -c Release

# HVSC fetch tool
dotnet build .\tools\HvscFetch\HvscFetch.csproj -c Release
```

## Documentation

| Doc | Description |
|-----|-------------|
| [docs/architecture/overview.md](docs/architecture/overview.md) | System architecture |
| [docs/csdb-romm-integration.md](docs/csdb-romm-integration.md) | CSDb bridge design, politeness rules, API |
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
