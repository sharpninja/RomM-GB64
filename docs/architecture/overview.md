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

### HVSC (host tree, same model as GB64)

| Path | Content |
|------|---------|
| `hvsc/` | Full HVSC (`MUSICIANS/`, `GAMES/`, `DEMOS/`, …) |

- Downloaded on the **host** with C# `tools/HvscFetch` via `scripts/Download-Hvsc.ps1` (default dest `./hvsc`).
- Bind-mounted read-only: `./hvsc` → `/romm/library/hvsc` on `romm` and `csdb-bridge`.
- **Not** a platform under `roms/`; **not** baked into the Docker image.
- NFO `SID:` paths resolve as `/romm/library/hvsc/` + path with `\` → `/`.

### Redeploy behavior

- **Preserve** operator state across git checkout: `.env`, `runtime/config` (including `config.yml`), `runtime/library`, `runtime/assets`, `runtime/csdb`, `hvsc/`, `gb64/`.
- **Library prepare** (`scripts/Prepare-RomMLibrary.ps1`): always ensures platform dirs `c64`, `c128`, `c-plus-4`, `vic-20`; runs GB64 library import **only if** prepared library data under `roms/c64` is missing (or force). If GB64 source is absent or library already populated, skip import.

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
