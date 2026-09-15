## Hostile validation

**OverallVerdict: AGREE**

Scope: the seven requested claims for `PLAN-ROMMCLIENT-001` / `docs/plans/PLAN-PHASE12-CLOSEOUT.md`. Independently reverified on 2026-09-15 against HEAD `c505e207d2457cb3f248def6d0cb86491a0d6125` **plus the existing uncommitted changes**. Prior receipts were used only to identify reproductions.

| Claim | Result | Fresh evidence |
|---|---|---|
| 1. Prior C4a/C4b fixed | **PASS** | Default `new RomMAuthHandler(auth)`, `HttpClient.BaseAddress=http://romm.test/`, and `RomMTransport.SendAsync` to `https://evil.example/api/roms`: exactly one unauthenticated GET. OAuth sent no foreign token POST, credentials, or body, both before and after obtaining a same-origin token. Independent probes `C1.*` passed. |
| 2. Same default handler authenticates relative requests | **PASS** | On the same handler/transport instance immediately after the foreign GET, `api/platforms` resolved to `http://romm.test/api/platforms` with the exact configured Bearer. Both requests carried the inherited base option. Probe `C1+C2.default-bearer-exact-foreign-then-relative-same-handler` passed. [Transport stamping](F:/GitHub/RomM/src/RomM.Client/Http/RomMTransport.cs:46). |
| 3. Missing allowed/inherited origin fails closed | **PASS** | Six independent probes covered Bearer, Basic, and OAuth, with and without a direct HttpClient BaseAddress but no inherited option. Every case emitted one GET with no credentials and no token POST. [Null-origin guard](F:/GitHub/RomM/src/RomM.Client/Auth/RomMAuthHandler.cs:54). |
| 4. Factory remains origin-bound | **PASS** | `RomMClient.Create` independently passed Bearer, Basic, and OAuth controls: foreign host, changed scheme, and changed port received no credentials; same-origin requests authenticated; cached OAuth stayed isolated. [Factory binding](F:/GitHub/RomM/src/RomM.Client/RomMClient.cs:48). |
| 5. Existing contracts remain valid | **PASS** | All detailed checks below passed against the freshly built DLL. |
| 6. Requested Release rerun | **PASS** | Executed exactly `dotnet test tests/RomM.Client.Tests/RomM.Client.Tests.csproj -c Release` through PowerShell.MCP: **60 passed, 0 failed, 0 skipped, exit 0**, 22:58:15-22:58:20 UTC. |
| 7. No authorization of Iterations 3-9 | **PASS** | This verdict authorizes no work or completion claims for Iterations 3-9. Existing TODO Done flags and its stale summary were not accepted as validation evidence. |

### Claim 5 details

- **Heartbeat PASS:** pinned OpenAPI confirms `HeartbeatResponse.SYSTEM -> SystemDict`. Shipped-client probes preserved VERSION and both true/false SHOW_SETUP_WIZARD; top-level-only fields left both convenience properties null.
- **Unknown coverage PASS:** three unknown `/api/*` paths returned false / `unmapped`; core heartbeat/platform/ROM routes remained explicitly typed.
- **Pagination PASS:** omitted total returned IDs 1,2,3 at offsets 0,2; a full final page returned IDs 1,2,3,4 at offsets 0,2,4. Explicit total=3, limit=1 returned all three items.
- **Cancellation PASS:** cancellation within a page and between pages threw `OperationCanceledException` after one yield and one request. Pre-canceled enumeration yielded zero items and made zero requests.
- **GetAsync PASS:** ROM detail fields populated; missing ROM threw `RomMApiException` with exact status 404, path, and response body.
- **Tasks PASS:** pinned-schema-shaped responses made finished terminal success and failed/stopped/canceled terminal failure. `WaitAsync` returned or threw accordingly without further polling. `task_id` won over a conflicting legacy `id`.

### Validation receipts

- Independent validator-owned probes: **29 passed, 0 failed, 0 skipped, exit 0**. Synthetic HTTP only; no live-server or network behavior is claimed.
- Before/after SHA-256 comparisons matched for all **41 existing source/test/project files**. Product source and existing tests were not modified.
- [Release log](C:/Users/kingd/AppData/Local/Temp/romm-codex-hv-rerun-20260915T225815Z/release-tests.log), [probe log](C:/Users/kingd/AppData/Local/Temp/romm-codex-hv-rerun-20260915T225815Z/independent-probes.log), [probe source](C:/Users/kingd/AppData/Local/Temp/romm-codex-hv-rerun-20260915T225815Z/Program.cs).
- Reproduce probes: `dotnet run --project C:\Users\kingd\AppData\Local\Temp\romm-codex-hv-rerun-20260915T225815Z\HvProbes.csproj -c Release --property:OutputType=Exe`.
- Tested DLL and probe-loaded copy SHA-256: `293EAB4D4F82C56329DA72EFD8BA6A83A71DFC6890882CAE8CEAF6C69B31E726`.
- Release log SHA-256: `C0C793802FC2C032D4D3706B7178012BFB12ED14367D6D912BAFFB4AD9228808`.
- Probe log SHA-256: `91286DBC0BFAE5252DA61ADE818CA57F3BCB8E9FBB3E392F2DD406ABE8822751`.
- Report: [codex-hv-xhigh-20260915-rerun-report.md](F:/GitHub/RomM/docs/receipts/codex-hv-xhigh-20260915-rerun-report.md).

### Explicit FAIL list

None.

=== VERDICT JSON ===
{"verdict":"AGREE","test_passed":60,"test_failed":0,"test_skipped":0,"fail_list":[]}
