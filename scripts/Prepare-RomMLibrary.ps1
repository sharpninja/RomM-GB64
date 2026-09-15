# Moved to vice-sharp-romm. Forwards to that repo with this tree as LibraryRoot.
[CmdletBinding()]
param(
    [string]$ViceSharpRommRoot = $(
        if ($env:VICESHARP_ROMM_ROOT) { $env:VICESHARP_ROMM_ROOT }
        else { Join-Path (Split-Path $PSScriptRoot -Parent) '..\vice-sharp-romm' }
    ),
    [switch]$ForceLibraryBuild
)
$ErrorActionPreference = 'Stop'
$target = Join-Path $ViceSharpRommRoot 'scripts\Prepare-RomMLibrary.ps1'
if (-not (Test-Path -LiteralPath $target)) {
    throw "GB64/HVSC importer moved to vice-sharp-romm. Missing: $target (set VICESHARP_ROMM_ROOT)."
}
$library = Split-Path $PSScriptRoot -Parent
& $target -LibraryRoot $library -ForceLibraryBuild:$ForceLibraryBuild
exit $LASTEXITCODE
