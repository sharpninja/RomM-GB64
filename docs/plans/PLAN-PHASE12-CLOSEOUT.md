# Plan: close PLAN-ROMMCLIENT-001 phases 1 and 2

**Date:** 2026-09-15
**Source:** Codex `gpt-6-astra` review + HV (`docs/receipts/codex-phase12-20260915.request-response.jsonl`)
**Verdict:** DISAGREE. Do not mark Iteration 1 or 2 complete.

Phases:
- Phase 1 / Iteration 1: RomM auth handlers (FR-AUTH-001/002, IMPL-ROMMCLIENT-001)
- Phase 2 / Iteration 2: system, platforms, ROM read, pagination

Tests Codex re-ran: `dotnet test tests/RomM.Client.Tests/RomM.Client.Tests.csproj -c Release` = 46 passed, 0 failed, 0 skipped. Green suite is **not** DoD.

## Blocking remediations (Byrd order)

### R1. Replace placeholder client FRs with testable AC

**FAIL:** wiki/MCP exports still have "Placeholder requirement backfilled" for FR-AUTH-001, FR-AUTH-002, FR-SYS-001, FR-PLT-001, FR-ROM-001..004, FR-TASK-001/002. Mappings omit those IDs. No TEST-AUTH record.

**Work:**
1. Update each FR in MCP (`workspacePath` `F:\GitHub\RomM`) with real body + numbered AC (Bearer `rmm_`, Basic, OAuth refresh skew, 401/403 types, heartbeat schema, platforms list/get, roms page fields, pagination).
2. Create TEST-AUTH, TEST-SYS, TEST-PLT, TEST-ROM covering those AC by test class/method.
3. Create mappings FR -> TR-AUTH-001 / TR-HTTP-001 / TR-ROMM-API-001 -> TEST-*.
4. Export wiki. Placeholder text must be gone.

**Exit:** `requirements_list` shows non-placeholder bodies; TR-per-FR-Mapping lists FR-AUTH-001/002 and FR-ROM-00x; TEST-AUTH exists.

### R2. Heartbeat contract vs OpenAPI 5.0.0

**FAIL:** pinned `openapi/romm-5.0.0.json` nests heartbeat fields under `SYSTEM`. Client `HeartbeatResponse` and test fixtures use top-level `Version` / `ShowSetupWizard`. A conforming server response deserializes those as null.

**Work:**
1. Write a failing test that deserializes a `SYSTEM`-wrapped fixture and asserts Version/ShowSetupWizard (or documents the mapped property).
2. Change the model/JSON mapping to match OpenAPI (nested SYSTEM or explicit JsonPropertyName path).
3. Keep a negative test that a top-level-only fixture is not silently treated as the live contract.

**Files:** `src/RomM.Client/Models/CoreModels.cs` (HeartbeatResponse), `tests/RomM.Client.Tests/RomMCoreClientTests.cs`, `openapi/romm-5.0.0.json`.

### R3. OpenAPI coverage must not auto-pass `/api/*`

**FAIL:** coverage test accepts every `/api/*` path, so PASS does not prove the operation is implemented.

**Work:**
1. Change `OpenApiCoverageTests` / `OpenApiPathCoverage` so unmapped `/api/*` paths FAIL or are an explicit allow-list with a comment and TEST-SPEC-001 AC.
2. Phase 2 scope: heartbeat, platforms, roms list/get, pagination query params must be mapped to real client methods.

**Files:** `src/RomM.Client/OpenApiPathCoverage.cs`, `tests/RomM.Client.Tests/OpenApiCoverageTests.cs`.

### R4. Credential leakage on absolute URLs

**FAIL (security):** public transport attaches the configured Bearer token to an absolute URL on another host. OAuth sends username/password to that host's `/api/token`.

**Work:**
1. Red tests: `SendAsync` to `https://evil.example/api/roms` must not include `Authorization` from the RomM client; OAuth must not POST secrets to a non-base host.
2. Restrict auth injection to the configured `BaseAddress` host (and relative URIs). Refuse or strip auth on cross-host absolute URIs unless explicitly opted in.
3. Same-origin check for OAuth token endpoint (already `/api/token` on request URI; pin to BaseAddress).

**Files:** `src/RomM.Client/Http/RomMTransport.cs`, `src/RomM.Client/Auth/RomMAuthHandler.cs`, `tests/RomM.Client.Tests/RomMAuthHandlerTests.cs`, `tests/RomM.Client.Tests/RomMHttpExceptionMappingTests.cs`.

### R5. Pagination early termination

**FAIL:** `EnumerateAsync` (or equivalent) stops paging too early vs `total`/`offset`/`limit`.

**Work:**
1. Red test: server total=3, limit=1, three pages; enumerator must yield 3 items.
2. Fix loop to continue until offset+count >= total or a short page with no next.

**Files:** `src/RomM.Client/Clients/ResourceClients.cs`, `tests/RomM.Client.Tests/RomListQueryTests.cs`, `RomMCoreClientTests.cs`.

### R6. Cancellation: no yield after cancel

**FAIL:** enumerator yielded an item after cancellation.

**Work:**
1. Red test: cancel after first page; no further `MoveNext` success.
2. Check `cancellationToken` before each yield and before the next HTTP call.

**Files:** `ResourceClients.cs` `EnumerateAsync`.

### R7. Task/scan payloads vs pinned schema

**FAIL:** task responses fail against the pinned OpenAPI schema (phase 2 adjacent if scan is in iteration 2; if scan is iteration 3, still fix before claiming ROM I/O complete).

**Work:**
1. Compare `TaskInfo` / `TaskExecutionResponse` / `TaskStatusResponse` to `openapi/romm-5.0.0.json`.
2. Red tests with fixtures from the spec; then fix DTOs.

**Files:** `CoreModels.cs`, `ResourceClients.cs` tasks client, `RomMCoreClientTests.cs`.

## Non-blocking but recorded

- Concurrent OAuth shares one grant: PASS (keep a regression test).
- Rejected refresh clears token store: PASS (keep a regression test).
- Native Codex sandbox exec 206 on this host: use PowerShell.MCP for agent test runs; do not treat as a product defect.
- Live MCP reads failed under `approval_policy=never` in the first Codex exec. Re-verify R1 against MCP `requirements_list` after R1, not only wiki files.

## Exit for phases 1 and 2

1. R1 wiki/MCP placeholders gone; TEST-AUTH mapped.
2. R2-R6 red-then-green; focused `RomM.Client.Tests` 0 fail 0 skip.
3. Hostile AGREE on Iteration 1 and Iteration 2 claims (not Codex babysit unless you ask).
4. Only then keep IMPL-ROMMCLIENT-001 / Iteration 2 `Done=true` with a new DoneSummary citing the new tests.

Do not treat the existing 46/46 green run as closing these phases.
