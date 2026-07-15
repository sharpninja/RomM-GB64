# Technical Requirements (MCP Server)

## TR-CSDB-ARCH-001

**CSDb bridge sidecar service (C#)** — Implement integration as a companion service csdb-bridge in C# ASP.NET Core (.NET 8), not a RomM core fork and not Python. The service shares library storage with RomM via subpath mounts, exposes REST endpoints for full-catalog search (source=live), RSS recent-window index refresh, and selective ingest by explicit id list. Apply rate limits and search result caps. See docs/csdb-romm-integration.md.
**Covered by:** FR: FR-CSDB-001, FR-CSDB-002; TEST: TEST-CSDB-001, TEST-CSDB-002, TEST-CSDB-003
**Status:** pending
Scope: layer-1+

## TR-CSDB-CLIENT-001

**CSDb search and webservice clients** — Search free-text via CSDb site search HTML (or advanced search) filtered for demos, cracks, and SIDs. Resolve metadata and DownloadLinks via https://csdb.dk/webservice/?type=release|sid&id={id}&depth=2. Do not assume a free-text webservice search exists. Parse DownloadLink Link URLs for file retrieval.
**Covered by:** FR: FR-CSDB-001, FR-CSDB-002, FR-CSDB-003; TEST: TEST-CSDB-001, TEST-CSDB-002, TEST-CSDB-003
**Status:** pending
Scope: layer-1+

## TR-CSDB-INGEST-001

**Selective ingest pipeline with archive extract** — POST /csdb/v1/ingest accepts 1..20 explicit {kind, csdb_id} items. For each item: fetch webservice detail, download Ok links or HVSC-link SIDs, write under roms/c64-csdb-*. If a download is an archive (zip/7z/rar by extension or magic), extract into the package folder and do not keep the archive. Rate-limit CSDb HTTP. Persist job manifest under library csdb-ingest. Optional RomM scan trigger when ROMM_API_TOKEN is configured.
**Covered by:** FR: FR-CSDB-002, FR-CSDB-003; TEST: TEST-CSDB-002, TEST-CSDB-003, TEST-CSDB-001
**Status:** pending
Scope: layer-1+

## TR-CSDB-LAYOUT-001

**CSDb ingest paths under roms** — Write demos to roms/c64-csdb-demo/, cracks to roms/c64-csdb-crack/, SID links/files to roms/c64-csdb-sid/, other types from bulk results to roms/c64-csdb-misc/. Use flat or multi-file game folders with (csdb-{id}) tags. Never create letter-bucket parent folders. Optional RomM config.yml platform remaps to c64.
**Covered by:** FR: FR-CSDB-002, FR-CSDB-003; TEST: TEST-CSDB-002, TEST-CSDB-003, TEST-CSDB-001
**Status:** pending
Scope: layer-1+

