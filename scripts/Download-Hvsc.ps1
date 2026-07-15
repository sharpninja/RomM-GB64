<#
.SYNOPSIS
  Download and normalize the High Voltage SID Collection (HVSC) for RomM.

.DESCRIPTION
  Host-side PowerShell helper. Docker image builds use tools/HvscFetch (C#) instead.
  Layout: DEST\MUSICIANS\..., DEST\GAMES\..., DEST\DEMOS\...

.PARAMETER Dest
  Destination folder (default: .\runtime\hvsc)

.PARAMETER HvscUrl
  Optional direct archive URL (skips discovery)

.EXAMPLE
  .\scripts\Download-Hvsc.ps1 -Dest .\runtime\hvsc
#>
[CmdletBinding()]
param(
    [string]$Dest = (Join-Path $PSScriptRoot '..\runtime\hvsc' | Resolve-Path -ErrorAction SilentlyContinue | ForEach-Object { $_.Path }),
    [string]$HvscUrl = $env:HVSC_URL,
    [string]$WorkDir = $(if ($env:HVSC_WORK_DIR) { $env:HVSC_WORK_DIR } else { Join-Path $env:TEMP 'hvsc-work' })
)

$ErrorActionPreference = 'Stop'
if (-not $Dest) {
    $Dest = Join-Path (Split-Path $PSScriptRoot -Parent) 'runtime\hvsc'
}

$toolProj = Join-Path (Split-Path $PSScriptRoot -Parent) 'tools\HvscFetch\HvscFetch.csproj'
if (-not (Test-Path $toolProj)) {
    throw "C# tool not found: $toolProj"
}

New-Item -ItemType Directory -Force -Path $Dest, $WorkDir | Out-Null
$env:HVSC_WORK_DIR = $WorkDir
if ($HvscUrl) { $env:HVSC_URL = $HvscUrl }

Write-Host "Running HvscFetch (C#) -> $Dest"
dotnet run --project $toolProj -c Release --no-launch-profile -- $Dest
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Done: $Dest"
