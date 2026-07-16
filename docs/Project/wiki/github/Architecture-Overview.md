# Architecture overview

## Purpose

Operate a **RomM** instance for Commodore 64 content sourced from a local **GameBase64** tree (`./gb64`), with **HVSC** for SID resolution and a **CSDb bridge** for scene search and selective acquisition into the RomM library.

## Runtime components

```text
┌─────────────────┐     HTTP :8080      ┌──────────────┐
│  Browser / apps │ ──────────────────► │ RomM         │
└─────────────────┘                     │ (upstream)   │
┌─────────────────┐     HTTP :8090      └──────┬───────┘
│ Tools / UI /    │ ──────────────────► ┌──────┴───────┐
│ curl / scripts  │                     │ csdb-bridge  │
└─────────────────┘                     │ (ASP.NET 8)  │
                                        └──────┬───────┘
                                               │ shared host dirs
                                        ┌──────▼───────┐
                                        │ ./gb64/      │
                                        │ ./hvsc/      │
                                        │ runtime/     │
                                        │  library/roms│
                                        │  csdb/       │
                                        │  assets/     │
                                        └──────────────┘
                                               │
                                        ┌──────▼───────┐
                                        │ MariaDB      │
                                        └──────────────┘
```

| Container | Image / build | Role |
|-----------|---------------|------|
| `romm` | Thin `Dockerfile` FROM `rommapp/romm` | Library UI, scan, play, metadata |
| `romm-db` | `mariadb:lts` | RomM database |
| `csdb-bridge` | `services/csdb-bridge` | CSDb search, RSS index, selective ingest |

## Data planes

### RomM Structure A (library authority)

Upstream RomM defines platforms and folder slugs. **Required** operator libraries (host `runtime/library/roms/{slug}/`, exact slugs):

| Folder slug | Platform | Role |
|-------------|----------|------|
| `c64` | Commodore C64/128/MAX | Required; GB64 + CSDb C64 content |
| `c128` | Commodore 128 | Required library root |
| `c-plus-4` | Commodore Plus/4 | Required library root (not `plus4`) |
| `vic-20` | Commodore VIC-20 | Required library root (not `vic20`) |

**Optional** (same pattern when content is added):

| Folder slug | Platform |
|-------------|----------|
| `c16` | Commodore 16 |
| `cpet` | Commodore PET |
| `commodore-cdtv` | Commodore CDTV |

Layout: `/romm/library/roms/{platform}/…` (Structure A). Keep platform trees distinct: do not place VIC-20/Plus/4/C128 titles under `c64`. Use only RomM slugs (hyphenated where documented).

### GameBase64 (source of truth on host)

| Path | Content |
|------|---------|
| `gb64/Games/` | Letter-bucket ZIPs (GameBase packages with `VERSION.NFO`) |
| `gb64/Screenshots/` | Screenshots keyed by NFO `Screenshot:` paths |
| `gb64/ROMs/` | Firmware / kernals (GameBase GEMUS layout) |

When organized for RomM, game media goes under **`roms/c64/`** with tags from `docs/gb64-romm-tag-mapping.md`. Never use GB64 letter buckets as RomM parents.

### Attached library media roots (metadata path resolution)

Single host bind: `runtime/library` → `/romm/library` (Structure A parent of `roms/`).

| Host path | Container | Metadata field |
|-----------|-----------|----------------|
| `runtime/library/roms/{platform}/` | `/romm/library/roms/…` | Game packages (Structure A) |
| `runtime/library/hvsc/` | `/romm/library/hvsc` | NFO `SID:` (HVSC-relative) |
| `runtime/library/screenshots/` | `/romm/library/screenshots` | NFO `Screenshot:` (`Letter\file.png`) |
| `runtime/library/bios/` | `/romm/library/bios` | Optional firmware |

- HVSC: download with `scripts/Download-Hvsc.ps1` into `runtime/library/hvsc` (not image layers).
- Screenshots: sync from source `gb64/Screenshots` into `runtime/library/screenshots` via prepare (so runtime does not need the whole `gb64` mount).
- Source `./gb64/Games` remains import-only; **resolvable media for SID/Screenshot must be under attached `runtime/library`**.

### Redeploy behavior

- **Preserve** operator state: `.env`, `runtime/config`, entire `runtime/library` (roms + hvsc + screenshots), `runtime/assets`, `runtime/csdb`, source `gb64/` if present.
- **Prepare** (`scripts/Prepare-RomMLibrary.ps1`): platform dirs; screenshot sync if library screenshots missing; game import only if `roms/c64` media missing; warns if HVSC absent under `library/hvsc`.

### CSDb selective ingest (writable)

Preferred: write packages into **`runtime/library/roms/c64/`** (host) → `/romm/library/roms/c64/`.

Host:

```text
runtime/library/roms/c64/          # required
runtime/library/roms/c128/         # required
runtime/library/roms/c-plus-4/     # required
runtime/library/roms/vic-20/       # required
runtime/csdb/                      # SQLite index + raw RSS
```

CSDb C64 ingest writes under `roms/c64/`. Transition: legacy `c64-csdb-*` staging may exist; if so, `config.yml` remaps them to `c64`.

Ingest rules:

- Only **explicit** CSDb ids via `POST /csdb/v1/ingest` (max 20).
- Archives extracted into package folders; archive file not retained.
- SIDs prefer hardlink/copy from HVSC into `roms/c64/` when `HVSCPath` is present.

## CSDb bridge API (summary)

| Endpoint | Notes |
|----------|--------|
| `GET /health` | Includes `runtime: csharp` |
| `GET /csdb/v1/search` | `source=live\|index\|both`; live = full catalog site search |
| `POST /csdb/v1/index/refresh` | RSS recent feeds, metadata only |
| `GET /csdb/v1/index/search` | Local index FTS/LIKE |
| `POST /csdb/v1/ingest` | Selected ids only; archive extract |
| `GET /csdb/v1/releases/{id}` | Webservice detail |
| `GET /csdb/v1/sids/{id}` | Webservice SID + HVSCPath |

Politeness: no full-site dump, no id-range crawl, rate-limited HTTP, small search caps.

## Language policy

| Kind | Stack |
|------|--------|
| Services | C# (.NET 8) |
| Host scripts | PowerShell |
| Container base for RomM | Upstream `rommapp/romm` image |
| Not used | Python, bash project scripts |

## Related docs

- [CSDb integration](../csdb-romm-integration.md)
- [HVSC in container](../hvsc-in-container.md)
- [GB64 tag mapping](../gb64-romm-tag-mapping.md)
