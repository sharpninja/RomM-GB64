# Moved to vice-sharp-romm. Forwards HVSC root from this repo unless HVSC_ROOT is set.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$SidPath,
    [string]$ViceSharpRommRoot = $(
        if ($env:VICESHARP_ROMM_ROOT) { $env:VICESHARP_ROMM_ROOT }
        else { Join-Path (Split-Path $PSScriptRoot -Parent) '..\vice-sharp-romm' }
    )
)
$ErrorActionPreference = 'Stop'
$target = Join-Path $ViceSharpRommRoot 'scripts\Resolve-SidPath.ps1'
if (-not (Test-Path -LiteralPath $target)) {
    throw "Resolve-SidPath moved to vice-sharp-romm. Missing: $target"
}
if (-not $env:HVSC_ROOT) {
    $env:HVSC_ROOT = Join-Path (Split-Path $PSScriptRoot -Parent) 'runtime\library\hvsc'
}
& $target $SidPath
exit $LASTEXITCODE
