# Architecture overview

## Purpose

Operate a **RomM** instance for Commodore 64 content sourced from a local **GameBase64** tree (`./gb64`), with **HVSC** for SID resolution and a **CSDb bridge** for scene search and selective acquisition into the RomM library.

## Runtime components

```text
┌─────────────────┐     HTTP :8080      ┌──────────────┐
│  Browser / apps │ ──────────────────► │ RomM         │
└─────────────────┘                     │ (custom img) │
                                        │ + HVSC embed │
┌─────────────────┐     HTTP :8090      └──────┬───────┘
│ Tools / UI /    │ ──────────────────► ┌──────┴───────┐
│ curl / scripts  │                     │ csdb-bridge  │
└─────────────────┘                     │ (ASP.NET 8)  │
                                        └──────┬───────┘
                                               │ shared host dirs
                                        ┌──────▼───────┐
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
| `romm` | `Dockerfile` FROM `rommapp/romm` + HVSC layer | Library UI, scan, play, metadata |
| `romm-db` | `mariadb:lts` | RomM database |
| `csdb-bridge` | `services/csdb-bridge` | CSDb search, RSS index, selective ingest |

## Data planes

### GameBase64 (source of truth on host)

| Path | Content |
|------|---------|
| `gb64/Games/` | Letter-bucket ZIPs (GameBase packages with `VERSION.NFO`) |
| `gb64/Screenshots/` | Screenshots keyed by NFO `Screenshot:` paths |
| `gb64/ROMs/` | Firmware / kernals (GameBase GEMUS layout) |

Not yet fully reorganized into RomM Structure A under `roms/c64/`; mapping rules are in `docs/gb64-romm-tag-mapping.md`.

### HVSC

- Embedded at **image build** via C# `tools/HvscFetch` into `/romm/library/hvsc/`.
- Host helper: `scripts/Download-Hvsc.ps1`.
- NFO `SID:` paths resolve as `/romm/library/hvsc/` + path with `\` → `/`.
- Not under `roms/` (avoids multi-file scan noise from `MUSICIANS/` trees).

### CSDb packages (writable)

Host:

```text
runtime/library/roms/c64-csdb-demo/
runtime/library/roms/c64-csdb-crack/
runtime/library/roms/c64-csdb-sid/
runtime/library/roms/c64-csdb-misc/
runtime/csdb/          # SQLite index + raw RSS
```

Mounted into RomM as **subpaths** of `/romm/library/roms/` so embedded HVSC is not masked.

Ingest rules:

- Only **explicit** CSDb ids via `POST /csdb/v1/ingest` (max 20).
- Archives extracted into package folders; archive file not retained.
- SIDs prefer hardlink/copy from HVSC when `HVSCPath` is present.

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
