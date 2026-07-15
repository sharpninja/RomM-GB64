# HVSC in the RomM container

The Docker image downloads the **complete High Voltage SID Collection (HVSC)** at build time and installs it where GameBase64 `VERSION.NFO` `SID:` paths resolve with a single prefix.

## Layout

| Path | Role |
|------|------|
| `/romm/library/hvsc/` | HVSC root inside the image |
| `/romm/library/hvsc/MUSICIANS/` | Composer tree |
| `/romm/library/hvsc/GAMES/` | Game-music tree |
| `/romm/library/hvsc/DEMOS/` | Demo music tree |
| `/romm/library/hvsc/.hvsc-origin.txt` | Build provenance marker |
| `/romm/library/roms/` | RomM Structure A games (separate; not HVSC) |

HVSC is **not** placed under `roms/` so RomM does not treat `MUSICIANS` / letter folders as multi-file games.

## Map NFO → file

GameBase field:

```text
SID:               MUSICIANS\W\Whittaker_David\180.sid
```

Resolve:

```text
/romm/library/hvsc/ + path with \ → /
= /romm/library/hvsc/MUSICIANS/W/Whittaker_David/180.sid
```

Host resolver (PowerShell):

```powershell
.\scripts\Resolve-SidPath.ps1 -SidPath 'MUSICIANS\W\Whittaker_David\180.sid' -HvscRoot .\runtime\hvsc
```

Inside the RomM image, the tree is under `/romm/library/hvsc`. Env `HVSC_ROOT` defaults to that path for tools that need it.

## Why this is easy for RomM

1. **Stable join key** from each game package: NFO `SID:` (already used by GameBase).
2. **One root** next to the library: `/romm/library/hvsc`.
3. **No host bind** of HVSC required; content is in the image layer.
4. Games stay under `/romm/library/roms/c64/` (when embedded later); music stays addressable without polluting the C64 ROM scan.

Optional later steps (not required for storage):

- Copy/link resolved SIDs into per-game folders as extras.
- Flatten SIDs into a dedicated RomM platform folder only if you want SIDs browsable as their own library (requires platform slug + avoid deep multi-file folders).

## Build

```bash
# From repo root
cp .env.example .env   # set secrets
docker compose build
docker compose up -d
```

Pin or override the archive if discovery fails:

```bash
docker compose build --build-arg HVSC_URL=https://hvsc.brona.dk/HVSC/HVSC_85-all-of-them.7z
```

Discovery order (`tools/HvscFetch` C# / `scripts/Download-Hvsc.ps1`):

1. `HVSC_URL` if set  
2. Official API `complete.url` from `https://www.hvsc.c64.org/api/v1/version/7z`  
3. Fallback mirror `https://hvsc.brona.dk/HVSC/HVSC_{version}-all-of-them.7z`  

**Note:** The API sometimes points at mirrors that redirect to HTML. The build script rejects HTML and tries the next candidate.

## Verify

```powershell
docker compose exec romm sh -c 'test -d /romm/library/hvsc/MUSICIANS && find /romm/library/hvsc -iname "*.sid" | wc -l'
docker compose exec romm cat /romm/library/hvsc/.hvsc-origin.txt
.\scripts\Resolve-SidPath.ps1 'GAMES\A-F\Boulder_Dash.sid' -HvscRoot .\runtime\hvsc
```

## Runtime volume warning

Do **not** mount a host path over `/romm/library`. An empty host dir hides the embedded HVSC (and any future embedded ROMs). Mutable data uses:

- `./runtime/assets` → `/romm/assets`
- `./runtime/config` → `/romm/config`
- named volumes for DB / resources / redis

## Host-only download (no Docker)

Uses C# `tools/HvscFetch` via PowerShell (requires .NET 8 SDK):

```powershell
.\scripts\Download-Hvsc.ps1 -Dest .\runtime\hvsc
.\scripts\Resolve-SidPath.ps1 'MUSICIANS\W\Whittaker_David\180.sid' -HvscRoot .\runtime\hvsc
```

## Legal / size

HVSC is a third-party hobby collection. Rebuilds re-download the complete pack (~80–100+ MB compressed). Keep redistributed images private unless you have rights to ship HVSC content.
