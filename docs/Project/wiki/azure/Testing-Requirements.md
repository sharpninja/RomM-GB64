# Testing Requirements (MCP Server)

## TEST-CSDB

### TEST-CSDB-001

Unit tests with fixtures: HTML search pages yield release/SID ids and types; release XML yields DownloadLinks; sid XML yields HVSCPath; Type maps to correct roms/ subfolder.


### TEST-CSDB-002

Given an ingest request with N explicit items (1<=N<=20), the job attempts download/link for all N and records each outcome. Partial failures do not drop unprocessed ids silently. Archives are extracted into the package folder without retaining the archive file.


### TEST-CSDB-003

Integration test with mocked downloads writes files or hardlinks under the expected roms/c64-csdb-* paths with (csdb-{id}) naming and multi-file folders when multiple DownloadLinks exist.
