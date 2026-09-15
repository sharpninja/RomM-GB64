# Functional Requirements (MCP Server)

## FR-AUTH-001 FR-AUTH-001

Placeholder requirement backfilled for TODO link FR-AUTH-001.
Scope: layer-1+

## FR-AUTH-002 FR-AUTH-002

Placeholder requirement backfilled for TODO link FR-AUTH-002.
Scope: layer-1+

## FR-CSDB-001 Search CSDb for SID demos and cracks

Users must be able to search the Commodore Scene Database (CSDb) for SID music, demos, and cracks via a service integrated with the RomM stack (companion/sidecar API and/or UI). Search must cover free-text queries and support filtering by kind (sid, demo, crack). Results must be returned to the caller as a structured list (CSDb id, title, type, kind).
Scope: layer-1+

## FR-CSDB-002 Selective CSDb ingest to library storage

When a user requests ingest of explicitly selected CSDb entries (ids and kinds), each selected item must be downloaded or linked into RomM Structure A storage under the built-in c64 platform path library/roms/c64/ (preferred), using package folders or flat files tagged (csdb-{id}). Maximum 20 items per request. Deduplicate by CSDb id; skip existing packages unless force is set. Archives must be extracted into the package folder; archive files must not be retained. Do not auto-download entire unbounded search result sets. Temporary staging folders c64-csdb-* are allowed only if config.yml remaps them to c64; end state is direct write under roms/c64/.
Scope: layer-1+

## FR-CSDB-003 Link CSDb SID results to HVSC or roms

For CSDb SID results, resolve HVSCPath from the CSDb webservice when present and hardlink or symlink into roms/c64/ (Structure A, built-in c64 slug) from the host HVSC tree bind-mounted at /romm/library/hvsc (host ./hvsc, not image-baked) when the file exists; otherwise download from CSDb when a file URL is available. Name files with (csdb-{id}) tags so GameBase and CSDb SID references remain joinable. Do not treat HVSC as a RomM platform folder under roms/.
Scope: layer-1+

## FR-CSDB-004 FR-CSDB-004

Placeholder requirement backfilled for TODO link FR-CSDB-004.
Scope: layer-1+

## FR-CSDB-005 FR-CSDB-005

Placeholder requirement backfilled for TODO link FR-CSDB-005.
Scope: layer-1+

## FR-CSDB-006 FR-CSDB-006

Placeholder requirement backfilled for TODO link FR-CSDB-006.
Scope: layer-1+

## FR-CSDB-007 FR-CSDB-007

Placeholder requirement backfilled for TODO link FR-CSDB-007.
Scope: layer-1+

## FR-CSDB-008 FR-CSDB-008

Placeholder requirement backfilled for TODO link FR-CSDB-008.
Scope: layer-1+

## FR-DX-001 FR-DX-001

Placeholder requirement backfilled for TODO link FR-DX-001.
Scope: layer-1+

## FR-GB64-001 GameBase64 source tree and Structure A landing

GB64 remains host-sourced under ./gb64 (Games ZIPs, Screenshots, ROMs). C64 game media is imported only under runtime/library/roms/c64/. Letter buckets (a1, b2, 0) are never RomM platform or parent folders. gb64/ROMs firmware is not auto-imported into roms/ (optional library/bios is a separate root).

Acceptance:
1. Import output is under roms/c64/ only.
2. No letter-bucket directory is created as a RomM parent.
3. Missing gb64/Games causes prepare to SKIP game import with a log line.
Scope: layer-1+

## FR-GB64-002 Screenshot staging into library/screenshots

NFO Screenshot: values are GB64-relative (A\Alfabug.png). Prepare shall robocopy gb64/Screenshots into runtime/library/screenshots when the library screenshot tree is empty (or ForceLibraryBuild), then write marker .screenshots-synced.

Acceptance:
1. After sync, Screenshot: A\file.png resolves to library/screenshots/A/file.png.
2. Existing screenshot library data skips sync unless Force.
3. Missing gb64/Screenshots logs SKIP and does not throw.
Scope: layer-1+

## FR-GB64-003 VERSION.NFO tagging and extract

tools/Gb64Import shall unzip each GB64 game ZIP, parse VERSION.NFO, and name output from NFO Name (sanitized) plus RomM tags per docs/gb64-romm-tag-mapping.md: language, PAL/NTSC, revision, (gb64-id), TrueDrive when NFO says Yes. Multi-file media becomes a folder; single media a file. VERSION.NFO is not left in the scannable ROM tree.
Scope: layer-1+

## FR-GB64-004 Conditional import, resume, force, and markers

Prepare runs Gb64Import only when C64 library media is missing (no media files and no .gb64-library-built). Importer persists .gb64-import-state.txt. ForceLibraryBuild rewrites state and re-imports. Success requires the game marker file.
Scope: layer-1+

## FR-GB64-005 SID NFO fixer against HVSC

Gb64Import --fix-sids indexes HVSC basenames and rewrites VERSION.NFO SID: fields: keep if resolvable; remap if the SID exists under a different HVSC-relative path; set SID: (None) if unresolvable.
Scope: layer-1+

## FR-GB64-006 Asset path validation

Gb64Import --validate-assets checks that each NFO Screenshot: and SID: relative path resolves under attached library/screenshots and library/hvsc and reports ok/missing counts.
Scope: layer-1+

## FR-HVSC-001 HVSC lives on attached library storage

The High Voltage SID Collection (HVSC) shall live on the host under runtime/library/hvsc (container /romm/library/hvsc). Layout includes MUSICIANS/, GAMES/, DEMOS/. HVSC is gitignored, not a RomM platform under roms/, and not baked into Docker image layers.

Acceptance:
1. Compose bind makes HVSC visible at /romm/library/hvsc.
2. Dockerfile does not COPY or RUN an HVSC fetch into the image.
3. SID: MUSICIANS\W\Whittaker_David\180.sid resolves under that root.
Scope: layer-1+

## FR-HVSC-002 HVSC download and normalize

Operators fetch HVSC with scripts/Download-Hvsc.ps1 which runs tools/HvscFetch (C#, no Python). Default dest is runtime/library/hvsc. Discovers archive URL or HVSC_URL, extracts, and normalizes MUSICIANS/GAMES/DEMOS at dest root.
Scope: layer-1+

## FR-HVSC-003 Prepare does not download HVSC

Prepare-RomMLibrary.ps1 shall not download HVSC. If library/hvsc lacks MUSICIANS or any .sid, it logs WARN and the Download-Hvsc command line.
Scope: layer-1+

## FR-PLT-001 FR-PLT-001

Placeholder requirement backfilled for TODO link FR-PLT-001.
Scope: layer-1+

## FR-ROM-001 FR-ROM-001

Placeholder requirement backfilled for TODO link FR-ROM-001.
Scope: layer-1+

## FR-ROM-002 FR-ROM-002

Placeholder requirement backfilled for TODO link FR-ROM-002.
Scope: layer-1+

## FR-ROM-003 FR-ROM-003

Placeholder requirement backfilled for TODO link FR-ROM-003.
Scope: layer-1+

## FR-ROM-004 FR-ROM-004

Placeholder requirement backfilled for TODO link FR-ROM-004.
Scope: layer-1+

## FR-ROMM-001 Use RomM Structure A and built-in Commodore platforms

The stack must treat upstream RomM as the library authority. Storage follows RomM Structure A: library/roms/{platform}/. Commodore 8-bit libraries that operators must support as first-class platform roots use RomM built-in folder slugs only: c64 (Commodore C64/128/MAX), c128 (Commodore 128), c-plus-4 (Commodore Plus/4), vic-20 (Commodore VIC-20). Also recognize other built-in Commodore slugs when present (c16, cpet, commodore-cdtv) without inventing alternate folder names. Empty platform folders created by RomM on first run are expected and must be host-persisted under runtime/library/roms/. Custom side folders (if any) must remap via config.yml system.platforms to one of these built-in slugs or be retired.
Scope: layer-1+

## FR-ROMM-002 Maintain c64, c128, Plus/4, and VIC-20 libraries

The operator library must include distinct Structure A trees for at least four Commodore platforms, using exact RomM slugs: roms/c64/, roms/c128/, roms/c-plus-4/, and roms/vic-20/. Each tree is a separate RomM platform for scan, metadata, and UI. Content must be placed in the platform that matches the software (do not dump VIC-20 or Plus/4 titles into c64). Host paths under runtime/library/roms/{slug}/ must exist and be bind-mounted so libraries survive container recreate. Optional additional platforms (c16, cpet, commodore-cdtv) follow the same pattern when content is added.
Scope: layer-1+

## FR-ROMM-003 Preserve RomM config and conditional GB64 library prepare on redeploy

Redeploy shall preserve .env, runtime/config/config.yml, runtime/assets, runtime/library, runtime/csdb, host HVSC, and gb64. Prepare-RomMLibrary.ps1 never overwrites a non-empty config.yml. GB64 game import and screenshot sync run only when the corresponding attached library data is missing, unless ForceLibraryBuild.

Acceptance:
1. Existing non-empty config.yml is byte-identical after redeploy.
2. Populated roms/c64 or marker .gb64-library-built skips game import.
3. Empty roms/c64 plus gb64/Games runs Gb64Import.
4. Populated library/screenshots or marker .screenshots-synced skips screenshot robocopy.
Scope: layer-1+

## FR-ROMM-004 HVSC and screenshots on attached library for metadata paths

GB64 VERSION.NFO SID: and Screenshot: fields store relative paths only. At runtime those paths must resolve against roots on host-attached library storage mounted into the container: HVSC under library/hvsc (SID: MUSICIANS\... -> library/hvsc/MUSICIANS/...), screenshots under library/screenshots (Screenshot: A\file.png -> library/screenshots/A/file.png). Both roots must sit under the Structure A library parent (runtime/library on host, /romm/library in container) so they survive container recreate and are visible to RomM and csdb-bridge without baking into the image. Source ./gb64 may remain import-only; resolvable screenshot files must be staged into library/screenshots. HVSC is downloaded into library/hvsc, not only a path outside the library mount.
Scope: layer-1+

## FR-ROMM-005 REST heartbeat and authentication

The RomM HTTP API on port 8080 exposes GET /api/heartbeat and accepts Authorization: Bearer (client token rmm_...) or the documented OAuth/password flow. Tokens must not appear in URL query strings. Unauthenticated protected routes return 401.
Scope: layer-1+

## FR-ROMM-006 Platforms API

GET /api/platforms lists platforms with numeric id and slug. GET /api/platforms/{id} returns one platform. Slugs include c64, c128, c-plus-4, vic-20 when those libraries exist.
Scope: layer-1+

## FR-ROMM-007 ROM list, search, page, and char index

GET /api/roms supports search_term, platform_ids, limit, offset, order_by, order_dir. Page payload includes items, total, offset, and a character index for A-Z jump.
Scope: layer-1+

## FR-ROMM-008 ROM detail

GET /api/roms/{id} returns detailed ROM metadata including files (fs_name), cover fields, and summary. Unknown id returns 404.
Scope: layer-1+

## FR-ROMM-009 ROM content download

Authenticated download of a ROM file by id and file name streams bytes. Missing file returns 404.
Scope: layer-1+

## FR-ROMM-010 Library scan task

The server exposes a tasks API so a client can trigger a library scan after ingest and poll status until complete.
Scope: layer-1+

## FR-ROMM-011 Collections (lists)

The server persists user collections: list, create, rename, delete, add roms, remove roms. Smart/virtual collections are read-only.
Scope: layer-1+

## FR-ROMM-012 Cover art fetch

Cover images are fetchable. Public url_cover may be unauthenticated. Server path_cover_* resources require Bearer. Missing artwork does not 500 the ROM list.
Scope: layer-1+

## FR-SPEC-001 FR-SPEC-001

Placeholder requirement backfilled for TODO link FR-SPEC-001.
Scope: layer-1+

## FR-SYS-001 FR-SYS-001

Placeholder requirement backfilled for TODO link FR-SYS-001.
Scope: layer-1+

## FR-TASK-001 FR-TASK-001

Placeholder requirement backfilled for TODO link FR-TASK-001.
Scope: layer-1+

## FR-TASK-002 FR-TASK-002

Placeholder requirement backfilled for TODO link FR-TASK-002.
Scope: layer-1+

