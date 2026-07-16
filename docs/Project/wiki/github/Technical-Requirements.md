# Technical Requirements (MCP Server)

## TR-AUTH-001

**TR-AUTH-001** — Placeholder requirement backfilled for TODO link TR-AUTH-001.
**Status:** pending
Scope: layer-1+

## TR-CI-001

**TR-CI-001** — Placeholder requirement backfilled for TODO link TR-CI-001.
**Status:** pending
Scope: layer-1+

## TR-CSDB-001

**TR-CSDB-001** — Placeholder requirement backfilled for TODO link TR-CSDB-001.
**Status:** pending
Scope: layer-1+

## TR-CSDB-002

**TR-CSDB-002** — Placeholder requirement backfilled for TODO link TR-CSDB-002.
**Status:** pending
Scope: layer-1+

## TR-CSDB-ARCH-001

**CSDb bridge sidecar service (C#)** — Implement integration as companion service csdb-bridge in C# ASP.NET Core (.NET 8), not a RomM core fork and not Python. Share Structure A library storage with RomM: LIBRARY_ROMS_ROOT maps to host runtime/library/roms (containing c64/ and any other platform dirs). Expose REST for full-catalog search (source=live), RSS recent-window index, and selective ingest by explicit id list. Rate limits and search caps apply. HVSC_ROOT is the host HVSC bind, not under roms/.
**Covered by:** FR: FR-CSDB-001, FR-CSDB-002; TEST: TEST-CSDB-001, TEST-CSDB-002, TEST-CSDB-003, TEST-ROMM-001
**Status:** pending
Scope: layer-1+

## TR-CSDB-CLIENT-001

**CSDb search and webservice clients** — Search free-text via CSDb site search HTML (or advanced search) filtered for demos, cracks, and SIDs. Resolve metadata and DownloadLinks via https://csdb.dk/webservice/?type=release|sid&id={id}&depth=2. Do not assume a free-text webservice search exists. Parse DownloadLink Link URLs for file retrieval.
**Covered by:** FR: FR-CSDB-001, FR-CSDB-002, FR-CSDB-003; TEST: TEST-CSDB-001, TEST-CSDB-002, TEST-CSDB-003, TEST-ROMM-001, TEST-HVSC-001
**Status:** pending
Scope: layer-1+

## TR-CSDB-INGEST-001

**Selective ingest pipeline with archive extract** — POST /csdb/v1/ingest accepts 1..20 explicit {kind, csdb_id} items. For each item: fetch webservice detail, download Ok links or HVSC-link SIDs from HVSC_ROOT (/romm/library/hvsc from host ./hvsc), write under LIBRARY_ROMS_ROOT/c64/ (Structure A). If a download is an archive (zip/7z/rar by extension or magic), extract into the package folder and do not keep the archive. Rate-limit CSDb HTTP. Persist job manifest under csdb data root. Optional RomM scan trigger when ROMM_API_TOKEN is configured.
**Covered by:** FR: FR-CSDB-002, FR-CSDB-003; TEST: TEST-CSDB-002, TEST-CSDB-003, TEST-ROMM-001, TEST-CSDB-001, TEST-HVSC-001
**Status:** pending
Scope: layer-1+

## TR-CSDB-LAYOUT-001

**CSDb ingest paths under roms** — Preferred write targets for C64 scene content: roms/c64/{Name} (csdb-{id})/ or flat roms/c64/{Name} (csdb-{id}).{ext}. Tags: (csdb-{id}) plus optional (Demo)/(Crack). Never create letter-bucket parents. Never invent non-RomM platform slugs for C64. If legacy staging paths roms/c64-csdb-demo|crack|sid|misc are used during transition, config.yml must remap each to c64. SIDs prefer hardlink/copy from HVSC_ROOT into roms/c64/, not into a separate sid platform.
**Covered by:** FR: FR-CSDB-002, FR-CSDB-003; TEST: TEST-CSDB-002, TEST-CSDB-003, TEST-ROMM-001, TEST-CSDB-001, TEST-HVSC-001
**Status:** pending
Scope: layer-1+

## TR-ERR-001

**TR-ERR-001** — Placeholder requirement backfilled for TODO link TR-ERR-001.
**Status:** pending
Scope: layer-1+

## TR-GEN-001

**TR-GEN-001** — Placeholder requirement backfilled for TODO link TR-GEN-001.
**Status:** pending
Scope: layer-1+

## TR-HTTP-001

**TR-HTTP-001** — Placeholder requirement backfilled for TODO link TR-HTTP-001.
**Status:** pending
Scope: layer-1+

## TR-HVSC-HOST-001

**Host HVSC download and compose mounts** — Provide C# tools/HvscFetch and scripts/Download-Hvsc.ps1 writing by default to runtime/library/hvsc (attached library root). docker-compose mounts ./runtime/library to /romm/library so HVSC is at /romm/library/hvsc. Dockerfile does not bake HVSC. Document in docs/hvsc-in-container.md. NFO SID: paths resolve against HVSC_ROOT only when this tree is on the bind-mounted library storage.
**Covered by:** FR: FR-CSDB-003, FR-GB64-001, FR-HVSC-001, FR-ROMM-004; TEST: TEST-CSDB-001, TEST-CSDB-003, TEST-HVSC-001, TEST-GB64-001, TEST-ROMM-004
**Status:** pending
Scope: layer-1+

## TR-LIB-001

**TR-LIB-001** — Placeholder requirement backfilled for TODO link TR-LIB-001.
**Status:** pending
Scope: layer-1+

## TR-PKG-001

**TR-PKG-001** — Placeholder requirement backfilled for TODO link TR-PKG-001.
**Status:** pending
Scope: layer-1+

## TR-PROC-001

**TR-PROC-001** — Placeholder requirement backfilled for TODO link TR-PROC-001.
**Status:** pending
Scope: layer-1+

## TR-ROMM-CFG-001

**config.yml platform remaps only when needed** — RomM /romm/config/config.yml may define system.platforms remaps. If temporary CSDb staging folders (c64-csdb-demo|crack|sid|misc) remain, each must remap to c64 so the UI shows one C64 platform. Preferred end state: no staging folders; ingest and GB64 organization write under roms/c64/ with (csdb-{id}) or (gb64-{id}) tags. config.yml must not redefine built-in Commodore platform identities.
**Covered by:** FR: FR-CSDB-002, FR-ROMM-001, FR-ROMM-002; TEST: TEST-CSDB-002, TEST-CSDB-003, TEST-ROMM-001, TEST-ROMM-002
**Status:** pending
Scope: layer-1+

## TR-ROMM-DEPLOY-001

**Deploy preserve-and-prepare script behavior** — Deploy process stashes and restores .env, runtime/config, runtime/library, runtime/assets, runtime/csdb, hvsc, gb64 around git checkout/clean. Invokes scripts/Prepare-RomMLibrary.ps1 which creates required platform dirs and runs GB64 library build only when roms/c64 lacks game media and no .gb64-library-built marker (unless Force). Never overwrites non-empty config.yml.
**Covered by:** FR: FR-ROMM-003; TEST: TEST-ROMM-003
**Status:** pending
Scope: layer-1+

## TR-ROMM-MEDIA-001

**Compose and env roots for library media** — docker-compose mounts ./runtime/library to /romm/library (and to csdb-bridge /data/library). Set HVSC_ROOT=/romm/library/hvsc and SCREENSHOTS_ROOT=/romm/library/screenshots on romm; csdb-bridge uses /data/library/hvsc and /data/library/screenshots. Download-Hvsc.ps1 default dest is runtime/library/hvsc. Prepare-RomMLibrary ensures library/hvsc and library/screenshots dirs, syncs gb64/Screenshots to library/screenshots when screenshots library data is missing, and does not place HVSC under roms/.
**Covered by:** FR: FR-ROMM-004; TEST: TEST-HVSC-001, TEST-ROMM-004
**Status:** pending
Scope: layer-1+

## TR-ROMM-PLAT-001

**Ensure required Commodore platform directories on host** — On deploy and local compose prepare, ensure directories exist: runtime/library/roms/c64, runtime/library/roms/c128, runtime/library/roms/c-plus-4, runtime/library/roms/vic-20. Document exact RomM slugs (hyphenated: c-plus-4, vic-20; not plus4 or vic20). Do not rename to non-RomM aliases. Scripts or deploy steps may mkdir -p these paths; content may be empty until filled.
**Covered by:** FR: FR-ROMM-001, FR-ROMM-002, FR-ROMM-003; TEST: TEST-ROMM-001, TEST-ROMM-002, TEST-ROMM-003
**Status:** pending
Scope: layer-1+

## TR-ROMM-STRUCT-001

**Structure A paths and native platform slugs** — Compose and bridge paths must align with Structure A library/roms/{platform}. Required host platform directories (created if missing): c64, c128, c-plus-4, vic-20. Optional: c16, cpet, commodore-cdtv. Mount host runtime/library/roms to /romm/library/roms on RomM so all platform trees persist. CSDb bridge LIBRARY_ROMS_ROOT maps to the same host roms tree; C64 scene ingest defaults to roms/c64/. HVSC is not a platform folder: bind ./hvsc to /romm/library/hvsc (sibling of roms/). Do not use subpath-only mounts that omit the required platform roots.
**Covered by:** FR: FR-CSDB-002, FR-CSDB-003, FR-GB64-001, FR-ROMM-001, FR-ROMM-002, FR-ROMM-003, FR-ROMM-004; TEST: TEST-CSDB-002, TEST-CSDB-003, TEST-ROMM-001, TEST-CSDB-001, TEST-HVSC-001, TEST-GB64-001, TEST-ROMM-002, TEST-ROMM-003, TEST-ROMM-004
**Status:** pending
Scope: layer-1+

## TR-TFM-001

**TR-TFM-001** — Placeholder requirement backfilled for TODO link TR-TFM-001.
**Status:** pending
Scope: layer-1+

## TR-WF-001

**TR-WF-001** — Placeholder requirement backfilled for TODO link TR-WF-001.
**Status:** pending
Scope: layer-1+

