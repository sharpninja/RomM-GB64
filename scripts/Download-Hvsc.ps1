# Moved to vice-sharp-romm. Forwards dest to this repo's runtime/library/hvsc.
[CmdletBinding()]
param(
    [string]$ViceSharpRommRoot = $(
        if ($env:VICESHARP_ROMM_ROOT) { $env:VICESHARP_ROMM_ROOT }
        else { Join-Path (Split-Path $PSScriptRoot -Parent) '..\vice-sharp-romm' }
    ),
    [string]$Dest = '',
    [string]$HvscUrl = $env:HVSC_URL
)
$ErrorActionPreference = 'Stop'
$target = Join-Path $ViceSharpRommRoot 'scripts\Download-Hvsc.ps1'
if (-not (Test-Path -LiteralPath $target)) {
    throw "HVSC fetch moved to vice-sharp-romm. Missing: $target (set VICESHARP_ROMM_ROOT)."
}
if (-not $Dest) {
    $Dest = Join-Path (Split-Path $PSScriptRoot -Parent) 'runtime\library\hvsc'
}
if ($HvscUrl) { $env:HVSC_URL = $HvscUrl }
& $target -Dest $Dest
exit $LASTEXITCODE
