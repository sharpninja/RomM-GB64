## Hostile validation

**OverallVerdict: DISAGREE**

Scope: Iterations 1–2 after R1–R7, `PLAN-ROMMCLIENT-001`, [closeout plan](F:/GitHub/RomM/docs/plans/PLAN-PHASE12-CLOSEOUT.md). Independently checked on 2026-09-15 at commit `c505e207d2457cb3f248def6d0cb86491a0d6125`.

| Claim | Result | Independently verified evidence |
|---|---|---|
| 1. MCP requirements and wiki mappings | **PASS** | Live `requirements_list` with `workspacePath=F:\GitHub\RomM` returned non-placeholder bodies for all ten specified FRs, all five TEST records, and FR-to-TR/TEST mappings. Effective requirements confirm these FRs in current layer 1. Both wiki exports list FR-ROM-003, FR-ROM-004 and FR-TASK-001 at lines 22, 23 and 37: [GitHub](F:/GitHub/RomM/docs/Project/wiki/github/TR-per-FR-Mapping.md:22), [Azure](F:/GitHub/RomM/docs/Project/wiki/azure/TR-per-FR-Mapping.md:22). |
| 2. Heartbeat contract | **PASS** | Validator-owned probes through the shipped client populated `SYSTEM.VERSION` and both true/false values of `SYSTEM.SHOW_SETUP_WIZARD`. Top-level-only fields left both properties null. Pinned OpenAPI `HeartbeatResponse → SystemDict` confirms the nesting. [Model](F:/GitHub/RomM/src/RomM.Client/Models/CoreModels.cs:6). |
| 3. Path coverage | **PASS** | Independent call returned false for `/api/does-not-exist-in-5.0.0`. Nine core resource paths map to existing typed interface methods and are absent from the deferred set; token retains its typed entry. [Coverage](F:/GitHub/RomM/src/RomM.Client/OpenApiPathCoverage.cs:183). |
| 4. Foreign-host credentials | **FAIL** | Factory-created clients passed Bearer/Basic origin checks and cold/cached OAuth checks. However, the public default `RomMAuthHandler(auth)` plus public `RomMTransport` still leaks both Bearer and OAuth credentials despite `HttpClient.BaseAddress=https://romm.test/`. Two independent probes reproduced this; details below. |
| 5. Pagination and cancellation | **PASS** | With total omitted, independent enumeration returned IDs 1,2,3 in exactly two requests, offsets 0 and 2. Cancellation after the first yield threw `OperationCanceledException`, count=1, requests=1, for page sizes 1 and 2. [Enumerator](F:/GitHub/RomM/src/RomM.Client/Clients/ResourceClients.cs:65). |
| 6. ROM detail and task semantics | **PASS** | Independent detail probe checked id/name/summary/fs_name/slug and exact `RomMApiException.StatusCode=404`, path and body. `ResolveTaskId` preferred `task_id` over a conflicting legacy `id`. Schema-shaped task responses made finished terminal success and failed/stopped/canceled terminal failure; `WaitAsync` returned or threw accordingly. [Models](F:/GitHub/RomM/src/RomM.Client/Models/CoreModels.cs:100). |
| 7. Requested Release rerun | **PASS** | Executed exactly `dotnet test tests/RomM.Client.Tests/RomM.Client.Tests.csproj -c Release` through PowerShell.MCP: **57 passed, 0 failed, 0 skipped, exit 0**, 22:35:31–22:35:38 UTC. |
| 8. No authorization of Iterations 3–9 | **PASS** | The live TODO Note explicitly says its prior authorization covers R1–R7 / Iterations 1–2 only and “Does NOT re-authorize Iterations 3-9”. Existing Done=true rows and stale “Unit 46/46” DoneSummary were not accepted as evidence. This validation authorizes no later iterations. |

### Explicit FAIL list

- **C4a: Bearer leakage through the default public handler.** With `new RomMAuthHandler(RomMAuth.ClientApiToken("rmm_validator_dummy"))`, an HttpClient base address of `https://romm.test/`, and `new RomMTransport(http)`, sending GET `https://evil.example/api/roms` produced an outgoing Bearer header.
- **C4b: OAuth password leakage through the same construction path.** Using `RomMAuth.OAuthPassword("validator", "dummy-password")` produced POST `https://evil.example/api/token` containing username/password, followed by an authenticated foreign GET.

Root cause: [the constructor defaults allowedBaseAddress to null](F:/GitHub/RomM/src/RomM.Client/Auth/RomMAuthHandler.cs:23); [null permits every origin](F:/GitHub/RomM/src/RomM.Client/Auth/RomMAuthHandler.cs:48); [OAuth then derives the token endpoint from the foreign request](F:/GitHub/RomM/src/RomM.Client/Auth/RomMAuthHandler.cs:246). Existing isolation tests explicitly supply an allowed origin at [line 361](F:/GitHub/RomM/tests/RomM.Client.Tests/RomMAuthHandlerTests.cs:361), leaving this public default path untested.

### Validation receipts

- Requested suite: **57/0/0**, exit **0**. Additional validator-owned probes: **13 passed, 2 failed, 0 skipped**, exit **1**. JSON test counts below refer to the requested suite.
- Probe HTTP requests were captured by an in-memory handler using dummy credentials; no requests were sent to evil.example.
- [Release output](F:/GitHub/RomM/.mcpServer/tmp/romm-codex-hv-20260915T223531Z/release-tests.log), [independent probe output](F:/GitHub/RomM/.mcpServer/tmp/romm-codex-hv-20260915T223531Z/independent-probes.log), [probe source](F:/GitHub/RomM/.mcpServer/tmp/romm-codex-hv-20260915T223531Z/Program.cs), [live MCP snapshot](F:/GitHub/RomM/.mcpServer/tmp/romm-codex-hv-20260915T223531Z/mcp-scoped-records.json).
- Reproduce probes: `dotnet run --project .mcpServer/tmp/romm-codex-hv-20260915T223531Z/HvProbes.csproj -c Release --property:OutputType=Exe`.
- Tested client DLL SHA-256: `63F85EC8D7BDA7762A62BBDA2BFEF07DCFCFB4D152765E637366DC535431E97F`.
- Independent probe output SHA-256: `52BDD933F0C54D257CB02BA7CB803AB9B7389B32486DB8567748D675C55093FA`.
- Before/after hashes matched for all **41 existing source/test/project files** checked. Product source and existing tests were not modified.

=== VERDICT JSON ===
{"verdict":"DISAGREE","test_passed":57,"test_failed":0,"test_skipped":0,"fail_list":["C4a: Default public RomMAuthHandler plus RomMTransport sends Bearer to evil.example.","C4b: Default public OAuth handler posts username/password to evil.example/api/token."]}

<oai-mem-citation>
<citation_entries>
MEMORY.md:84-88|note=[PowerShell MCP routing and complete evidence guidance]
</citation_entries>
<rollout_ids>
019f4851-b1fe-77d2-bd05-369231fdde4e
</rollout_ids>
</oai-mem-citation>
