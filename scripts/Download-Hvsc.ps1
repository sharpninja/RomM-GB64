<#
.SYNOPSIS
  Download and normalize HVSC into the attached RomM library storage.

.DESCRIPTION
  Writes the High Voltage SID Collection under runtime/library/hvsc so NFO SID:
  paths resolve at runtime against the same tree mounted at /romm/library/hvsc.

  Layout: DEST\MUSICIANS\..., DEST\GAMES\..., DEST\DEMOS\...

.PARAMETER Dest
  Destination folder (default: .\runtime\library\hvsc)

.PARAMETER HvscUrl
  Optional direct archive URL (skips discovery)

.EXAMPLE
  .\scripts\Download-Hvsc.ps1
#>
[CmdletBinding()]
param(
    [string]$Dest = $null,
    [string]$HvscUrl = $env:HVSC_URL,
    [string]$WorkDir = $(if ($env:HVSC_WORK_DIR) { $env:HVSC_WORK_DIR } else { Join-Path $env:TEMP 'hvsc-work' })
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $Dest) {
    $Dest = Join-Path $repoRoot 'runtime\library\hvsc'
}

$toolProj = Join-Path $repoRoot 'tools\HvscFetch\HvscFetch.csproj'
if (-not (Test-Path $toolProj)) {
    throw "C# tool not found: $toolProj"
}

New-Item -ItemType Directory -Force -Path $Dest, $WorkDir | Out-Null
$env:HVSC_WORK_DIR = $WorkDir
if ($HvscUrl) { $env:HVSC_URL = $HvscUrl }

Write-Host "Running HvscFetch (C#) -> $Dest (library HVSC root for SID: metadata)"
dotnet run --project $toolProj -c Release --no-launch-profile -- $Dest
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Done: $Dest (mounted as /romm/library/hvsc)"
