# GB64 and HVSC importer

This repo owns the importer. Host data is `./gb64` and `./runtime/library`.

## Tools

- `tools/Gb64Import` - extract GameBase64 ZIPs into Structure A `roms/c64/`
- `tools/HvscFetch` - download and normalize HVSC
- `scripts/Prepare-RomMLibrary.ps1` - platform dirs, screenshot sync, gated game import
- `scripts/Download-Hvsc.ps1` - runs HvscFetch
- `scripts/Resolve-SidPath.ps1` / `Resolve-ScreenshotPath.ps1`

```pwsh
.\scripts\Download-Hvsc.ps1
.\scripts\Prepare-RomMLibrary.ps1
dotnet test .\tools\Gb64Import.Tests\Gb64Import.Tests.csproj -c Release
```

Prepare skips game import when `roms/c64` already has media or `.gb64-library-built` exists. Resume uses `roms/c64/.gb64-import-state.txt` (do not pass `--force` unless you intend a full rewrite).

Files on disk are not in the RomM UI until a library scan. Compose sets `SCAN_TIMEOUT` default 86400 seconds and `SCAN_WORKERS` default 4 so a full GB64 Quick Scan is not killed at RomM's 4 hour default. HVSC stays under `runtime/library/hvsc` and is not a `roms/` platform.

