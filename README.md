# RomM + GameBase64 (C64)

Self-hosted [RomM](https://github.com/rommapp/romm) stack for a local **GameBase64** collection under `./gb64`, with HVSC for SID path resolution and a CSDb bridge for scene search and selective library ingest.

| | |
|--|--|
| **Stack** | Docker Compose: RomM, MariaDB, CSDb bridge |
| **Languages** | **C#** and **PowerShell** only (no Python) |
| **Deploy** | Octopus Deploy project **RomM** → target **PAYTON-DESKTOP** |
| **Repo** | `https://github.com/sharpninja/RomM-GB64.git` (`master`) |

## What you get

- **RomM** custom image (`rommapp/romm`) with **HVSC** at `/romm/library/hvsc`
- **CSDb bridge** (ASP.NET Core): full-catalog search, RSS recent-window index, selective ingest
- **Archive extract** on ingest: zip/7z/rar unpack into package folders under `roms/`
- **GameBase64** source tree (`Games`, `Screenshots`, `ROMs`) plus tag-mapping docs for RomM filenames
- **Polite CSDb usage**: no full-site dump; search is capped; download only explicit ids

## Repository layout

```text
├── README.md
├── docker-compose.yml          # romm, romm-db, csdb-bridge
├── Dockerfile                  # RomM image + HVSC via tools/HvscFetch
├── .env.example                # secrets template (.env is gitignored)
├── gb64/                       # GameBase64 source assets
├── runtime/                    # host volume mounts (not fully committed)
├── scripts/
│   ├── Download-Hvsc.ps1
│   └── Resolve-SidPath.ps1
├── services/csdb-bridge/       # C# CSDb bridge (CsdbBridge.sln)
├── tools/HvscFetch/            # C# HVSC download + normalize
└── docs/                       # architecture, CSDb, HVSC, GB64 mapping
```

## Prerequisites

- Docker Desktop / Docker Compose
- .NET 8 SDK (host builds and HVSC host download)
- Optional: Octopus CLI + API access for deploy from CI/workstation

## Quick start (local Docker)

### 1. Configure secrets

```powershell
Copy-Item .env.example .env
# Set at least:
#   DB_PASSWD
#   MARIADB_ROOT_PASSWORD
#   ROMM_AUTH_SECRET_KEY   # e.g. openssl rand -hex 32
```

### 2. Start the stack

```powershell
docker compose build
docker compose up -d
docker compose ps
```

| Service | URL |
|---------|-----|
| RomM UI | http://localhost:8080 (`ROMM_PORT`) |
| CSDb bridge | http://localhost:8090 (`CSDB_BRIDGE_PORT`) |
| MariaDB | internal only (`romm-db`) |

Open RomM and complete the first-run admin wizard.

### 3. CSDb bridge (optional)

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

### 4. HVSC helpers (host)

```powershell
.\scripts\Download-Hvsc.ps1 -Dest .\runtime\hvsc
.\scripts\Resolve-SidPath.ps1 'MUSICIANS\W\Whittaker_David\180.sid' -HvscRoot .\runtime\hvsc
```

Image builds embed HVSC with `tools/HvscFetch`. Override URL if needed:

```powershell
docker compose build --build-arg HVSC_URL=https://hvsc.brona.dk/HVSC/HVSC_85-all-of-them.7z
```

## Volume rules

- **Do not** mount a host path over the whole `/romm/library` on the RomM container (hides embedded HVSC).
- CSDb packages use **subpath** mounts:
  - `runtime/library/roms/c64-csdb-*` → `/romm/library/roms/c64-csdb-*`
- Mutable RomM data: `runtime/assets`, `runtime/config`, plus named volumes for DB / resources / redis.

## Octopus Deploy (PAYTON-DESKTOP)

Octopus Server: `http://payton-desktop:8065` (Space: **Default**).

| Setting | Value |
|---------|--------|
| Project | **RomM** (`Projects-7`) |
| Target | **PAYTON-DESKTOP** (role `web-server`) |
| Lifecycle | Default Lifecycle (first deploy: **Production**) |
| Deploy path | `C:\deploy\RomM` (`RomM.DeployPath`) |
| Git | `https://github.com/sharpninja/RomM-GB64.git` branch `master` |

### Trigger: GitHub Release → Octopus → PAYTON-DESKTOP

Publishing a **GitHub Release** on `sharpninja/RomM-GB64` runs [`.github/workflows/octopus-on-release.yml`](.github/workflows/octopus-on-release.yml):

1. Creates Octopus release **RomM** with version = tag (leading `v` stripped)
2. Pins git resource to that tag
3. Deploys to **Production** (lifecycle **RomM Auto Deploy** also auto-targets Production)

**GitHub repository secrets** (Settings → Secrets and variables → Actions):

| Secret | Example |
|--------|---------|
| `OCTOPUS_SERVER_URL` | `http://payton-desktop:8065` |
| `OCTOPUS_API_KEY` | `API-…` |
| `OCTOPUS_SPACE` | `Default` (optional) |

GitHub-hosted runners must **reach** Octopus. If `payton-desktop` is LAN-only, use a **self-hosted runner** on the LAN (uncomment `runs-on: [self-hosted, …]` in the workflow) or expose Octopus via VPN/tunnel.

**Create a GitHub Release** (triggers the workflow when secrets + self-hosted runner are set):

```powershell
# After pushing the commit you want tagged
gh release create v1.0.0 --title "v1.0.0" --notes "RomM stack deploy" --target master
```

**Or use the installed Octopus CLI locally** (no Actions):

```powershell
# OCTOPUS_URL + OCTOPUS_API_KEY already in your environment
.\scripts\New-OctopusReleaseFromGitHubTag.ps1 -Tag v1.0.0
```

### Deploy from CLI (manual)

```powershell
# Requires OCTOPUS_URL, OCTOPUS_API_KEY (octopus CLI on PATH)
$ver = Get-Date -Format 'yyyyMMdd.HHmmss'
octopus release create --project RomM --version $ver --space Default --no-prompt
octopus release deploy --project RomM --version $ver --environment Production --space Default --no-prompt -f basic
```

UI: [RomM project](http://payton-desktop:8065/app#/Spaces-1/projects/Projects-7)

### What the deploy step does

On the Tentacle (**PAYTON-DESKTOP**, role `web-server`):

1. Clone or update `C:\deploy\RomM` from GitHub (`RomM-GB64`)
2. Check out the **release tag** matching Octopus release number (or `v` + version), else `origin/master`
3. Preserve `.env`, `runtime/`, `gb64/`
4. `docker compose up -d --build`
5. Best-effort health probes for RomM and csdb-bridge

**Note:** First image build can take a long time (HVSC layer). Put real secrets in `C:\deploy\RomM\.env` on PAYTON-DESKTOP before production use.

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
| [docs/hvsc-in-container.md](docs/hvsc-in-container.md) | HVSC embed and SID path mapping |
| [docs/gb64-romm-tag-mapping.md](docs/gb64-romm-tag-mapping.md) | GameBase64 `VERSION.NFO` → RomM tags |
| [docs/wiki.yaml](docs/wiki.yaml) | Wiki export manifest |

## GameBase64 notes

- Games: `gb64/Games/{bucket}/*.zip` (letter buckets; do not mount as RomM platform folders as-is).
- Screenshots: `gb64/Screenshots/`; best join key is NFO `Screenshot:`.
- SID paths in NFO are HVSC-relative (`MUSICIANS\…`, `GAMES\…`).
- RomM Structure A expects `roms/c64/…` for game media after extract/organize.

## License and content

Upstream RomM is AGPLv3. GameBase64, HVSC, and CSDb content are third-party: only host material you are allowed to store; do not redistribute full custom images containing those archives without rights.
