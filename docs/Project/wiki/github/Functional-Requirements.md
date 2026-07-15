# Functional Requirements (MCP Server)

## FR-CSDB-001 Search CSDb for SID demos and cracks

Users must be able to search the Commodore Scene Database (CSDb) for SID music, demos, and cracks via a service integrated with the RomM stack (companion/sidecar API and/or UI). Search must cover free-text queries and support filtering by kind (sid, demo, crack). Results must be returned to the caller as a structured list (CSDb id, title, type, kind).
Scope: layer-1+

## FR-CSDB-002 Selective CSDb ingest to library storage

When a user requests ingest of explicitly selected CSDb entries (ids and kinds), each selected item must be downloaded or linked into attached storage under the RomM library roms/ tree (c64-csdb-demo|crack|sid|misc) so RomM can scan and catalog the files. Maximum 20 items per request. Deduplicate by CSDb id; skip existing packages unless force is set. Archives must be extracted into the package folder; archive files must not be retained. Do not auto-download entire unbounded search result sets.
Scope: layer-1+

## FR-CSDB-003 Link CSDb SID results to HVSC or roms

For CSDb SID results, resolve HVSCPath from the CSDb webservice when present and hardlink or symlink into roms/ from the host HVSC tree bind-mounted at /romm/library/hvsc (host path ./hvsc, same content model as ./gb64; not baked into the RomM image) when the file exists; otherwise download from CSDb when a file URL is available. GameBase and CSDb SID references must remain joinable via path or (csdb-{id}) tags.
Scope: layer-1+

## FR-HVSC-001 HVSC host tree like GameBase64

The High Voltage SID Collection must be stored on the host as a content tree under ./hvsc (gitignored), prepared with the host download tool, and bind-mounted read-only into RomM and csdb-bridge at /romm/library/hvsc. HVSC must not be baked into the RomM Docker image layers. Operators resolve NFO SID: paths against this host tree the same way GameBase64 content lives under ./gb64.
Scope: layer-1+

