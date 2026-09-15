# Moved to vice-sharp-romm.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$ScreenshotPath,
    [string]$ViceSharpRommRoot = $(
        if ($env:VICESHARP_ROMM_ROOT) { $env:VICESHARP_ROMM_ROOT }
        else { Join-Path (Split-Path $PSScriptRoot -Parent) '..\vice-sharp-romm' }
    )
)
$ErrorActionPreference = 'Stop'
$target = Join-Path $ViceSharpRommRoot 'scripts\Resolve-ScreenshotPath.ps1'
if (-not (Test-Path -LiteralPath $target)) {
    throw "Resolve-ScreenshotPath moved to vice-sharp-romm. Missing: $target"
}
if (-not $env:SCREENSHOTS_ROOT) {
    $env:SCREENSHOTS_ROOT = Join-Path (Split-Path $PSScriptRoot -Parent) 'runtime\library\screenshots'
}
& $target $ScreenshotPath
exit $LASTEXITCODE
