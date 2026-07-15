# Functional Requirements (MCP Server)

## FR-CSDB-001 Search CSDb for SID demos and cracks

Users must be able to search the Commodore Scene Database (CSDb) for SID music, demos, and cracks via a service integrated with the RomM stack (companion/sidecar API and/or UI). Search must cover free-text queries and support filtering by kind (sid, demo, crack). Results must be returned to the caller as a structured list (CSDb id, title, type, kind).
Scope: layer-1+

## FR-CSDB-002 Selective CSDb ingest to library storage

When a user requests ingest of explicitly selected CSDb entries (ids and kinds), each selected item must be downloaded or linked into RomM Structure A storage under the built-in c64 platform path library/roms/c64/ (preferred), using package folders or flat files tagged (csdb-{id}). Maximum 20 items per request. Deduplicate by CSDb id; skip existing packages unless force is set. Archives must be extracted into the package folder; archive files must not be retained. Do not auto-download entire unbounded search result sets. Temporary staging folders c64-csdb-* are allowed only if config.yml remaps them to c64; end state is direct write under roms/c64/.
Scope: layer-1+

## FR-CSDB-003 Link CSDb SID results to HVSC or roms

For CSDb SID results, resolve HVSCPath from the CSDb webservice when present and hardlink or symlink into roms/c64/ (Structure A, built-in c64 slug) from the host HVSC tree bind-mounted at /romm/library/hvsc (host ./hvsc, not image-baked) when the file exists; otherwise download from CSDb when a file URL is available. Name files with (csdb-{id}) tags so GameBase and CSDb SID references remain joinable. Do not treat HVSC as a RomM platform folder under roms/.
Scope: layer-1+

## FR-GB64-001 GameBase64 packages land under roms/c64

GameBase64 content remains host-sourced under ./gb64 (Games, Screenshots, ROMs). When organized for RomM, C64 game media must be written under /romm/library/roms/c64/ using filename tags from docs/gb64-romm-tag-mapping.md including (gb64-{Unique-ID}). Do not use GB64 letter buckets (a1, b2) as RomM platform or parent folders. Screenshots join via NFO Screenshot: paths under ./gb64/Screenshots. SID: paths resolve against host ./hvsc mounted at /romm/library/hvsc, not under roms/{platform}. Non-C64 Commodore collections (VIC-20, C128, Plus/4) are not assumed to live inside GB64 letter buckets; they use their own Structure A platform roots (vic-20, c128, c-plus-4) when organized.
Scope: layer-1+

## FR-HVSC-001 HVSC host tree like GameBase64

The High Voltage SID Collection must be stored on the host as a content tree under ./hvsc (gitignored), prepared with the host download tool, and bind-mounted read-only into RomM and csdb-bridge at /romm/library/hvsc. HVSC must not be baked into the RomM Docker image layers. Operators resolve NFO SID: paths against this host tree the same way GameBase64 content lives under ./gb64.
Scope: layer-1+

## FR-ROMM-001 Use RomM Structure A and built-in Commodore platforms

The stack must treat upstream RomM as the library authority. Storage follows RomM Structure A: library/roms/{platform}/. Commodore 8-bit libraries that operators must support as first-class platform roots use RomM built-in folder slugs only: c64 (Commodore C64/128/MAX), c128 (Commodore 128), c-plus-4 (Commodore Plus/4), vic-20 (Commodore VIC-20). Also recognize other built-in Commodore slugs when present (c16, cpet, commodore-cdtv) without inventing alternate folder names. Empty platform folders created by RomM on first run are expected and must be host-persisted under runtime/library/roms/. Custom side folders (if any) must remap via config.yml system.platforms to one of these built-in slugs or be retired.
Scope: layer-1+

## FR-ROMM-002 Maintain c64, c128, Plus/4, and VIC-20 libraries

The operator library must include distinct Structure A trees for at least four Commodore platforms, using exact RomM slugs: roms/c64/, roms/c128/, roms/c-plus-4/, and roms/vic-20/. Each tree is a separate RomM platform for scan, metadata, and UI. Content must be placed in the platform that matches the software (do not dump VIC-20 or Plus/4 titles into c64). Host paths under runtime/library/roms/{slug}/ must exist and be bind-mounted so libraries survive container recreate. Optional additional platforms (c16, cpet, commodore-cdtv) follow the same pattern when content is added.
Scope: layer-1+

## FR-ROMM-003 Preserve RomM config and conditional GB64 library prepare on redeploy

Redeploy must preserve operator RomM configuration and secrets: existing runtime/config/config.yml (including non-empty content), .env, runtime/assets, runtime/library, runtime/csdb, host hvsc, and gb64 trees must not be wiped or replaced by checkout. .env.example may seed .env only when .env is absent. GB64 library preparation (import/organize into Structure A roms/c64) must run only when prepared GB64 library data is missing under roms/c64 (or no build marker); if library data already exists, skip the library build. If GB64 source is absent, skip library build with a clear log message. Platform directories c64, c128, c-plus-4, vic-20 are still ensured empty if needed.
Scope: layer-1+

