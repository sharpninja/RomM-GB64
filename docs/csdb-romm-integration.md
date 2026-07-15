# CSDb → RomM integration design

## Politeness policy (hard rules)

**Pulling all of CSDb (or any large bulk dump) at once is forbidden.**

| Allowed | Not allowed |
|---------|-------------|
| **Full-catalog search** via CSDb site search (`/search/?seinsel=…`) - one query, capped results | Full site **mirror** or “download entire CSDb” |
| RSS **recent-window** feeds for a local delta index | ID-range crawl (`id=1…N`) via webservice |
| Rate-limited calls | Unbounded multi-page scrape of every result page |
| On-demand webservice **for one id** (detail / download links) | Bulk file download of entire unbounded result sets |
| Download **only user-selected** releases/SIDs into `roms/` | Auto-ingest every hit without selection |

**Search vs dump:** Searching the full CSDb catalog (live search) is fine and expected. **Dumping** the catalog is not. Keep `limit` small (default 25, cap 50).

RSS feeds remain a **rolling recent window** for local indexing only; they are not the full archive.

Default stance for file storage: **select then download**. Earlier “download all search results” language is **revoked**.

## Auth: identity not required

Probed **without login** (2026-07-14):

| Endpoint | Anonymous? |
|----------|------------|
| Webservice `type=release\|sid&id=` | **Yes** (XML 200) |
| RSS feeds | **Yes** |
| HTML search `/search/` | **Yes** |
| `getinternalfile.php/...` downloads | **Yes** (octet-stream) |
| `release/download.php?id=` | **Yes** (redirect to file) |

CSDb may set an anonymous `PHPSESSID` cookie; that is not an account.  
Optional `CSDB_USER` / `CSDB_PASSWORD` in `.env` are supported by the bridge for future restricted features; leave empty for anonymous mode.

## Search engine service (in compose)

Service: **`csdb-bridge`** (`services/csdb-bridge`, **C# / ASP.NET Core**)

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/health` | Liveness + auth mode flags |
| `GET` | `/csdb/v1/search?q=&kinds=&limit=&source=` | **Full catalog** search (`source=live`, default) or local RSS (`source=index`) or `both` |
| `GET` | `/csdb/v1/index/search?q=` | Search **local RSS index** only |
| `POST` | `/csdb/v1/index/refresh` | Re-download configured RSS feeds only (recent windows) |
| `POST` | `/csdb/v1/ingest` | Download **explicit CSDb ids** into `roms/` (selected items only) |
| `GET` | `/csdb/v1/releases/{id}` | One release by id (webservice) |
| `GET` | `/csdb/v1/sids/{id}` | One SID by id (webservice) |

Default port: **8090** (`CSDB_BRIDGE_PORT`).

```bash
# Full CSDb catalog search (demos/cracks/SIDs), capped
curl "http://localhost:8090/csdb/v1/search?q=boulder&kinds=demo&kinds=crack&kinds=sid&limit=20&source=live"

# Local RSS index only (recent window)
curl "http://localhost:8090/csdb/v1/search?q=boulder&source=index&limit=20"

# Refresh RSS recent feeds (metadata only)
curl -X POST "http://localhost:8090/csdb/v1/index/refresh"

# Ingest only chosen ids
curl -X POST "http://localhost:8090/csdb/v1/ingest" \
  -H "Content-Type: application/json" \
  -d "{\"items\":[{\"kind\":\"demo\",\"csdb_id\":263020}]}"
```

If `CSDB_BRIDGE_API_KEY` is set, send header `X-Api-Key: ...`.

RomM’s own `GET /api/roms?search_term=` only searches **already scanned** library entries. After selective ingest, run a RomM scan so those files appear in RomM.

## Goal

Allow users to **discover** CSDb material (SID / demos / cracks) via:

1. **Full-catalog live search** against CSDb (`source=live`, default), and/or  
2. A **local RSS recent-window index** (`source=index`),

then **download only selected items** into attached storage under `roms/` for RomM to scan.

## Important constraints

### What CSDb provides

| Surface | URL / form | Capability |
|---------|------------|------------|
| RSS | `/rss/latestreleases.php`, additions, news, … | Recent-window only |
| Webservice (XML) | `?type=release\|sid\|…&id=&depth=` | Detail by id; downloads; SID `HVSCPath`. `type=search` returns empty in practice |
| Site search (HTML) | `/search/?seinsel=releases\|sids\|all&search={q}` | **Full-catalog** free-text search |
| Advanced search | `/search/advancedresult.php?form[…]` | Filtered full-catalog queries |
| File download | `getinternalfile.php/…` | Bytes for a **selected** release |

**Implication:** Use **site search for full-catalog discovery**, **webservice for one-id detail/download**, **RSS for a small local recent index**. Cap result counts; never crawl all ids.

### What RomM provides

| Surface | Capability |
|---------|------------|
| UI search | Library text search only (local catalog) |
| `GET /api/roms?search_term=` | Search **already-imported** ROMs |
| `GET /api/search/roms` | Search **metadata providers** (IGDB/SS/…), not CSDb |
| Library tree | Structure A: `/romm/library/roms/{platform}/` |
| Scan | Import new files under `roms/` into the DB |

**RomM has no first-class plugin slot for “CSDb search.”** Integration is a **companion service** (sidecar) that:

1. Indexes **RSS recent-windows** and supports **bounded** search.
2. Downloads only **user-selected** ids into the shared library volume.
3. Optionally calls RomM’s scan/API afterward.
4. Exposes its own API/UI (can sit behind the same reverse proxy as RomM).

Embedding CSDb inside the official RomM UI would require **forking RomM** (out of scope unless decided later).

### Policy / ops

- **Bulk download of all search hits** can be large and may include unrelated titles. Require explicit confirmation, **max results**, and **rate limiting**.
- CSDb is a community site: throttle requests, cache XML, identify a polite User-Agent, honor failures/404s.
- **Copyright / cracks:** operators are responsible for what they store. Document that this feature automates scene-archive retrieval for personal/library use only.
- Do **not** re-host CSDb’s site; only store downloaded files the operator requested via search.

---

## Architecture

```text
┌─────────────┐     search query      ┌──────────────────────┐
│  User / UI  │ ───────────────────► │  csdb-bridge service │
│ (browser or │ ◄── result list +    │  (sidecar container) │
│  API client)│     download status  └──────────┬───────────┘
└─────────────┘                                 │
                                                │ 1) HTML search (filter type)
                                                │ 2) webservice type=release|sid
                                                │ 3) download files
                                                ▼
                                     ┌──────────────────────┐
                                     │ Attached storage     │
                                     │ /romm/library/roms/… │
                                     │ /romm/library/hvsc/  │
                                     └──────────┬───────────┘
                                                │
                                     ┌──────────▼───────────┐
                                     │ RomM (rommapp/romm)  │
                                     │ scan → catalog/API   │
                                     └──────────────────────┘
```

### Components

| Component | Role |
|-----------|------|
| **csdb-bridge** | C# ASP.NET Core service: RSS index refresh, local search, selective ingest, health |
| **Shared volume** | Writable `roms/c64-csdb-*` mounts into RomM; RSS index under `runtime/csdb` |
| **RomM** | Custom/base image; scans library after selective ingest |
| **Optional UI** | Query + kinds + **multi-select** download (never “grab entire result set” by default) |

### Platform folders (RomM Structure A)

RomM is the library authority. It ships built-in Commodore platform slugs (`c64`, `c128`, `c16`, `c-plus-4`, `vic-20`, `cpet`, `commodore-cdtv`) and creates those directories under Structure A on first run. **C64 scene and GameBase content uses `roms/c64/`.**

```text
/romm/library/
  roms/
    c64/                 # C64: GB64 + CSDb demos/cracks/SIDs
    c128/                # required C128 library (native slug)
    c-plus-4/            # required Plus/4 library (native slug; not plus4)
    vic-20/              # required VIC-20 library (native slug; not vic20)
    c16/ cpet/ …         # optional other Commodore platforms
  hvsc/                  # host ./hvsc bind (not a platform under roms/)
    MUSICIANS/ GAMES/ DEMOS/
```

**SID music strategy (prefer HVSC link):**

1. Search SIDs on CSDb (HTML search `seinsel` for SIDs, or release type music).
2. For each SID id: `webservice/?type=sid&id=…` → read `HVSCPath`.
3. If file exists at `/romm/library/hvsc` + `HVSCPath`: hardlink or symlink into  
   `roms/c64/{Name} (csdb-{id}).sid` (flat, RomM-safe).
4. If missing from HVSC: download from CSDb if a download link exists; else record HVSC miss.

**Demo / Crack strategy:**

1. HTML search (and/or filter advanced result types: Demo, One-File Demo, Crack, …).
2. For each release id: webservice `type=release&id=&depth=2` → `Type`, `Name`, `DownloadLinks`.
3. Download **every** `DownloadLink` with `Status=Ok` into:

```text
roms/c64/{Name} (csdb-{id})/{original-filename}
# multi-file when multiple downloads; single file when one
```

4. Filename tags: `(csdb-{id})` plus optional type tag `(Demo)` / `(Crack)`.

**Map CSDb `Type` → Structure A path**

| CSDb `Type` (examples) | Target under `roms/` |
|------------------------|----------------------|
| `C64 Demo`, `C64 One-File Demo`, `C64 Intro`, … | `c64/` with `(csdb-{id})` |
| `C64 Crack`, cracked games | `c64/` with `(csdb-{id})` |
| SID / music entries | `c64/` (link from HVSC) |
| Other selected types | `c64/` or another **built-in** slug if non-C64 |

**Legacy staging (transition only):** folders `c64-csdb-demo|crack|sid|misc` may still exist on disk. If used, remap in `config.yml` so the UI collapses them into C64:

```yaml
system:
  platforms:
    c64-csdb-demo: c64
    c64-csdb-crack: c64
    c64-csdb-sid: c64
    c64-csdb-misc: c64
```

Preferred end state: no staging folders; bridge writes only under `roms/c64/`.

---

## Flows

### A. Index RSS (phase 1 - current focus)

```text
POST /csdb/v1/index/refresh
```

1. Fetch only the configured **small** RSS URLs (see `DEFAULT_FEEDS` in code).  
2. Save raw XML under `runtime/csdb/rss/`.  
3. Upsert items into SQLite + FTS (metadata only: title, type, ids, download URL strings, screenshots).  
4. **Do not** download release binaries during refresh.

Typical volume: on the order of **tens to low hundreds of items per feed**, not the full CSDb corpus.

### B. Selective ingest (later / explicit)

```text
POST /csdb/v1/ingest
{ "items": [ { "kind": "demo", "csdb_id": 263020 } ] }
```

1. Cap list length (e.g. max 20 ids per request).  
2. For each id: webservice detail → download files / HVSC link.  
3. Rate-limit between CSDb calls.  
4. Write under `roms/c64-csdb-{demo|crack|sid|misc}/{Name} (csdb-{id})/`.  
5. If a download is an **archive** (`.zip`, `.7z`, `.rar`, … by extension or magic bytes), **extract into that package folder** and do **not** keep the archive file. Loose files (`.d64`, `.prg`, …) stay as single files in the folder.

### C. Live search (optional, bounded)

`GET /csdb/v1/search` hits CSDb HTML once per kind with a **strict limit** (default 20–50). No automatic file download.

---

## API surface (csdb-bridge)

| Method | Path | Purpose |
|--------|------|---------|
| `GET /health` | Liveness |
| `POST /csdb/v1/index/refresh` | Pull configured RSS feeds only |
| `GET /csdb/v1/index/stats` | Feed/item counts |
| `GET /csdb/v1/index/search` | Local FTS over indexed RSS items |
| `GET /csdb/v1/search` | Bounded live CSDb HTML search (no auto-download) |
| `POST /csdb/v1/ingest` | Download **explicit** ids only |
| `GET /csdb/v1/releases/{id}` | One release |
| `GET /csdb/v1/sids/{id}` | One SID |

Auth: optional `CSDB_BRIDGE_API_KEY` (`X-Api-Key`). Never expose unauthenticated ingest on a public network.

---

## Compose integration

Implemented in root `docker-compose.yml`:

- **csdb-bridge** builds from `services/csdb-bridge`, port **8090**
- Writes to `./runtime/library/roms` → `/data/roms` in the bridge
- RomM mounts each `c64-csdb-*` subfolder into `/romm/library/roms/...`
- HVSC for SID hardlinks: `./hvsc` → `/romm/library/hvsc` (ro) on RomM and the bridge

```bash
docker compose up -d csdb-bridge
curl http://localhost:8090/health
```

---

## Rate limiting & reliability

- ≥ 1–2 s between CSDb HTTP calls (configurable).
- Retry 429/5xx with backoff; skip permanent failures.
- Deduplicate by `csdb-{id}` directory (skip re-download if present unless `force=true`).
- Disk free-space check before job.
- Manifest + structured logs for audit.

---

## Testing strategy (Byrd-aligned)

| Layer | Tests |
|-------|-------|
| Unit | Parse HTML search fixtures → ids/types; parse release XML `DownloadLinks`; map Type → folder; HVSCPath join |
| Unit | RSS parse → index rows; FTS finds title |
| Unit | ingest of **one** mocked release id writes tree |
| Integration (opt-in live) | refresh RSS feeds only; assert item count is feed-sized, not full catalog |
| RomM | After **selected** files land, scan finds them |

---

## Out of scope

- Full CSDb mirror or bulk ID crawl  
- Auto-download of every search hit  
- Patching RomM frontend (unless later decided)  
- Uploading to CSDb  
- Automatic full HVSC rebuild from CSDb  

---

## Implementation phases

1. **RSS download + local index** (recent feeds only, metadata only).  
2. **Local index search API** for RomM/tools.  
3. **Selective ingest** by explicit id list (small cap).  
4. Bounded live search (optional).  
5. RomM scan hook + platform remap.  
6. UI multi-select (no “download all results” default).

---

## References

- CSDb webservice intro: https://csdb.dk/webservice/
- CSDb RSS index: https://csdb.dk/rss/index.php
- RomM library structure: https://docs.romm.app/latest/getting-started/folder-structure/
- RomM API: `/api/roms?search_term=`, `/api/docs`
- Local HVSC layout: `docs/hvsc-in-container.md`
