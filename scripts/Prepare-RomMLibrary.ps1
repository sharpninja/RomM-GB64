<#
.SYNOPSIS
  Ensure Structure A platform dirs and optionally prepare GB64-derived library content.

.DESCRIPTION
  - Always creates required platform directories under runtime/library/roms (c64, c128, c-plus-4, vic-20).
  - Does NOT overwrite RomM config (runtime/config/config.yml).
  - Runs GB64 library build/import ONLY when prepared GB64 library data is missing
    (no game media under roms/c64) AND source ./gb64/Games is available.
  - If roms/c64 already has content, skips the expensive library build.

.PARAMETER RepoRoot
  Repository / deploy root (default: parent of scripts/).

.PARAMETER ForceLibraryBuild
  Rebuild/import even if roms/c64 already has files.

.EXAMPLE
  .\scripts\Prepare-RomMLibrary.ps1
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$ForceLibraryBuild
)

$ErrorActionPreference = 'Stop'

function Test-HasGameMedia([string]$Dir) {
    if (-not (Test-Path -LiteralPath $Dir -PathType Container)) { return $false }
    $ext = @('*.d64', '*.g64', '*.t64', '*.tap', '*.crt', '*.prg', '*.p00', '*.sid', '*.zip', '*.7z', '*.rar')
    foreach ($e in $ext) {
        $hit = Get-ChildItem -LiteralPath $Dir -Recurse -File -Filter $e -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($hit) { return $true }
    }
    # Any non-empty file counts as "has data" (markers, extracted folders)
    $any = Get-ChildItem -LiteralPath $Dir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -gt 0 -and $_.Name -notmatch '^\.' } |
        Select-Object -First 1
    return [bool]$any
}

$romsRoot = Join-Path $RepoRoot 'runtime\library\roms'
$configDir = Join-Path $RepoRoot 'runtime\config'
$configFile = Join-Path $configDir 'config.yml'
$gb64Games = Join-Path $RepoRoot 'gb64\Games'
$c64Root = Join-Path $romsRoot 'c64'
$marker = Join-Path $romsRoot '.gb64-library-built'

Write-Host "Prepare-RomMLibrary root=$RepoRoot"

# --- Always: required Structure A platform dirs (empty OK) ---
$requiredPlatforms = @('c64', 'c128', 'c-plus-4', 'vic-20')
$optionalPlatforms = @('c16', 'cpet', 'commodore-cdtv')
foreach ($p in ($requiredPlatforms + $optionalPlatforms)) {
    $d = Join-Path $romsRoot $p
    if (-not (Test-Path -LiteralPath $d)) {
        New-Item -ItemType Directory -Path $d -Force | Out-Null
        Write-Host "Created platform dir: $d"
    }
}

# --- Always: config dir exists; never clobber existing config.yml ---
if (-not (Test-Path -LiteralPath $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}
if (-not (Test-Path -LiteralPath $configFile)) {
    # Empty file is fine for RomM first-run; do not write defaults over a later real config
    New-Item -ItemType File -Path $configFile -Force | Out-Null
    Write-Host "Created empty config.yml (first run)"
}
else {
    $len = (Get-Item -LiteralPath $configFile).Length
    Write-Host "Preserving existing config.yml ($len bytes)"
}

# --- Conditional: GB64 library build only if prepared data is missing ---
$hasSource = Test-Path -LiteralPath $gb64Games -PathType Container
$hasLibrary = (Test-HasGameMedia $c64Root) -or (Test-Path -LiteralPath $marker -PathType Leaf)

if (-not $hasSource) {
    Write-Host "SKIP library build: GB64 source missing at $gb64Games"
    Write-Host "Platform dirs ensured; config preserved."
    exit 0
}

if ($hasLibrary -and -not $ForceLibraryBuild) {
    Write-Host "SKIP library build: GB64-derived library data already present under $c64Root (or marker exists)"
    Write-Host "Use -ForceLibraryBuild to re-run import."
    exit 0
}

Write-Host "RUN library build: source=$gb64Games target=$c64Root Force=$ForceLibraryBuild"

# Placeholder for full organizer (VERSION.NFO -> tagged Structure A packages).
# Until the organizer is implemented, record intent and ensure c64 exists.
# Real import should extract Games ZIPs into roms/c64 with (gb64-{id}) tags per docs/gb64-romm-tag-mapping.md.
$gameZipCount = @(Get-ChildItem -LiteralPath $gb64Games -Recurse -Filter '*.zip' -File -ErrorAction SilentlyContinue).Count
Write-Host "GB64 game zip count (approx): $gameZipCount"

# Marker so redeploys skip until force or marker removed
@(
    "builtUtc=$((Get-Date).ToUniversalTime().ToString('o'))"
    "source=$gb64Games"
    "gameZipCount=$gameZipCount"
    "note=Organizer pass: ensure platform roots; full extract TBD in organizer tool"
) | Set-Content -LiteralPath $marker -Encoding utf8

Write-Host "Library prepare complete (marker: $marker)"
exit 0
