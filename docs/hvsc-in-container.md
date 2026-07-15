# HVSC (host tree, like GameBase64)

The **High Voltage SID Collection (HVSC)** is stored on the **host** next to GameBase64, not baked into the RomM image.

| Host path | Container path | Role |
|-----------|----------------|------|
| `./hvsc/` | `/romm/library/hvsc/` (ro bind) | HVSC root |
| `./hvsc/MUSICIANS/` | same | Composer tree |
| `./hvsc/GAMES/` | same | Game-music tree |
| `./hvsc/DEMOS/` | same | Demo music tree |
| `./hvsc/.hvsc-origin.txt` | same | Download provenance marker |

Same content model as `./gb64/`: large third-party tree on disk, gitignored, prepared once on the host, mounted into containers that need it.

HVSC is **not** placed under `roms/` so RomM does not treat `MUSICIANS` / letter folders as multi-file games.

## Map NFO → file

GameBase field:

```text
SID:               MUSICIANS\W\Whittaker_David\180.sid
```

Resolve:

```text
./hvsc/ + path with \ → /
= ./hvsc/MUSICIANS/W/Whittaker_David/180.sid

# inside containers:
/romm/library/hvsc/MUSICIANS/W/Whittaker_David/180.sid
```

Host resolver (PowerShell):

```powershell
.\scripts\Resolve-SidPath.ps1 -SidPath 'MUSICIANS\W\Whittaker_David\180.sid' -HvscRoot .\hvsc
```

Env `HVSC_ROOT` defaults to `/romm/library/hvsc` inside containers.

## Why host tree (not image embed)

1. **Same pattern as GB64**: operator-owned content on the host, not image layers.
2. **Fast image builds**: no multi-GB HVSC extract during `docker compose build`.
3. **Stable join key** from each game package: NFO `SID:`.
4. **One root** next to the library: `/romm/library/hvsc` via bind mount.
5. Games stay under `/romm/library/roms/…`; music stays addressable without polluting C64 ROM scan.

Optional later steps (not required for storage):

- Copy/link resolved SIDs into per-game folders as extras.
- Flatten SIDs into a dedicated RomM platform folder only if you want SIDs browsable as their own library.

## Download (host)

Requires .NET 8 SDK (C# `tools/HvscFetch`):

```powershell
.\scripts\Download-Hvsc.ps1
# equivalent:
.\scripts\Download-Hvsc.ps1 -Dest .\hvsc
```

Pin or override the archive if discovery fails:

```powershell
$env:HVSC_URL = 'https://hvsc.brona.dk/HVSC/HVSC_85-all-of-them.7z'
.\scripts\Download-Hvsc.ps1
```

Discovery order (`tools/HvscFetch` / `scripts/Download-Hvsc.ps1`):

1. `HVSC_URL` if set
2. Official API `complete.url` from `https://www.hvsc.c64.org/api/v1/version/7z`
3. Fallback mirror `https://hvsc.brona.dk/HVSC/HVSC_{version}-all-of-them.7z`

**Note:** The API sometimes points at mirrors that redirect to HTML. The tool rejects HTML and tries the next candidate.

## Compose mounts

```yaml
# romm and csdb-bridge
- ./hvsc:/romm/library/hvsc:ro
```

Create an empty `./hvsc` folder (or run the download) before `docker compose up` so the bind mount is valid.

## Verify

```powershell
.\scripts\Resolve-SidPath.ps1 'GAMES\A-F\Boulder_Dash.sid' -HvscRoot .\hvsc
docker compose exec romm sh -c 'test -d /romm/library/hvsc/MUSICIANS && find /romm/library/hvsc -iname "*.sid" | wc -l'
docker compose exec romm cat /romm/library/hvsc/.hvsc-origin.txt
```

## Related host trees

| Path | Content |
|------|---------|
| `./gb64/` | GameBase64 Games / Screenshots / ROMs |
| `./hvsc/` | Full HVSC (this doc) |
| `./runtime/` | Mutable RomM state + CSDb package folders |

## Legal / size

HVSC is a third-party hobby collection. The complete pack is ~80–100+ MB compressed and larger on disk after extract. Do not redistribute HVSC content without rights.
