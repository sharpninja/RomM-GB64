# Testing Requirements (MCP Server)

## TEST-CSDB

### TEST-CSDB-001

Unit tests with fixtures: HTML search pages yield release/SID ids and types; release XML yields DownloadLinks; sid XML yields HVSCPath; Type/kind maps to writes under roms/c64/ (or a path remapped to c64), never letter buckets.


### TEST-CSDB-002

Given an ingest request with N explicit items (1<=N<=20), the job attempts download/link for all N and records each outcome. Partial failures do not drop unprocessed ids silently. Archives are extracted into the package folder without retaining the archive file.


### TEST-CSDB-003

Integration test with mocked downloads writes files or hardlinks under roms/c64/ with (csdb-{id}) naming and multi-file folders when multiple DownloadLinks exist. SID hardlinks resolve from a fixture HVSC tree into roms/c64/.



## TEST-GB64

### TEST-GB64-001

Mapping rules and any organizer tooling place extracted GameBase packages under roms/c64 with (gb64-{id}) tags and never under letter-bucket parents. NFO SID: paths resolve under host ./hvsc; Screenshot: under ./gb64/Screenshots.


### TEST-GB64-002

Screenshot sync skip/run via Prepare-RomMLibrary and .screenshots-synced marker.


### TEST-GB64-003

NfoAndTagTests parse NFO and assert RomM tags including (gb64-26153).


### TEST-GB64-004

Conditional import skip/run/resume/force via .gb64-library-built and .gb64-import-state.txt.


### TEST-GB64-005

SID NFO fixer remaps or clears SID: against HVSC; dry-run does not modify ZIPs.


### TEST-GB64-006

--validate-assets counts ShotOk/ShotMissing and SidOk/SidMissing.



## TEST-HVSC

### TEST-HVSC-001

Given ./hvsc contains MUSICIANS (or a fixture SID path), scripts/Resolve-SidPath.ps1 resolves NFO SID: paths under that root. docker-compose.yml declares ./hvsc bind mounts for romm and csdb-bridge. Dockerfile must not COPY or RUN HVSC fetch into the image.


### TEST-HVSC-002

Download-Hvsc.ps1 / HvscFetch produce runtime/library/hvsc/MUSICIANS.


### TEST-HVSC-003

Prepare with empty library/hvsc logs WARN and does not throw solely for missing HVSC.



## TEST-ROMM

### TEST-ROMM-001

On a running RomM stack with host roms mount, /romm/library/roms contains at least c64, c128, c-plus-4, and vic-20 directories (empty allowed). Host runtime/library/roms mirrors those slugs. GB64 C64 media and CSDb C64 ingest resolve under roms/c64/ (or a folder remapped to c64). HVSC is at /romm/library/hvsc, not under roms/. No docs or compose use non-RomM aliases (plus4, vic20) as folder names.


### TEST-ROMM-002

Given compose up with runtime/library/roms mounted, verify host and container both list directories c64, c128, c-plus-4, vic-20 under library/roms. Creating a probe file under host roms/vic-20/ is visible in the container at /romm/library/roms/vic-20/ (bind persistence).


### TEST-ROMM-003

Given existing non-empty runtime/config/config.yml and populated roms/c64, a redeploy leaves config.yml byte-identical and does not re-run GB64 library import (Prepare-RomMLibrary reports SKIP). Given empty roms/c64 and present gb64/Games, prepare reports RUN library build and creates marker or content.


### TEST-ROMM-004

Given HVSC under runtime/library/hvsc and a fixture SID path, Resolve-SidPath finds the file. Given screenshots under runtime/library/screenshots with Letter/file.png layout, Resolve-ScreenshotPath finds NFO Screenshot paths. Compose defines a single library bind including both roots. Paths are not only under unmounted ./gb64 or image layers.


### TEST-ROMM-005

REST contract: heartbeat, platforms, roms list/detail, download, scan. Covered in tests/RomM.Client.Tests.


### TEST-ROMM-006

Collections list/create/add/remove; smart collections read-only.
