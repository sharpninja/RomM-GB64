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

