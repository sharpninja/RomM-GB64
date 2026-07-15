<#
.SYNOPSIS
  Create an Octopus Deploy release (and deploy to Production) from a GitHub tag/release.

.DESCRIPTION
  Uses the installed Octopus CLI (octopus.exe). For GitHub Actions, prefer
  .github/workflows/octopus-on-release.yml. This script is for local/manual use.

.PARAMETER Tag
  Git tag name (e.g. v1.0.0). Version for Octopus is Tag without leading v.

.PARAMETER Environment
  Octopus environment to deploy (default Production).

.PARAMETER SkipDeploy
  Only create the release (lifecycle may still auto-deploy).

.EXAMPLE
  .\scripts\New-OctopusReleaseFromGitHubTag.ps1 -Tag v1.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [string]$Environment = "Production",

    [string]$Project = "RomM",

    [string]$Space = $(if ($env:OCTOPUS_SPACE) { $env:OCTOPUS_SPACE } else { "Default" }),

    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command octopus -ErrorAction SilentlyContinue)) {
    throw "Octopus CLI (octopus) not found on PATH."
}
if ([string]::IsNullOrWhiteSpace($env:OCTOPUS_URL) -and [string]::IsNullOrWhiteSpace($env:OCTOPUS_SERVER_URL)) {
    throw "Set OCTOPUS_URL (or OCTOPUS_SERVER_URL), e.g. http://payton-desktop:8065"
}
if ([string]::IsNullOrWhiteSpace($env:OCTOPUS_API_KEY)) {
    throw "Set OCTOPUS_API_KEY"
}
if ([string]::IsNullOrWhiteSpace($env:OCTOPUS_URL) -and $env:OCTOPUS_SERVER_URL) {
    $env:OCTOPUS_URL = $env:OCTOPUS_SERVER_URL
}

$ver = $Tag.TrimStart("v", "V")
Write-Host "octopus version: $(octopus version)"
Write-Host "Creating release $ver for project $Project (tag $Tag) space $Space"

$notesFile = Join-Path ([IO.Path]::GetTempPath()) ("octopus-notes-{0}.txt" -f [guid]::NewGuid().ToString("N"))
@"
GitHub tag: $Tag
Created: $([DateTime]::UtcNow.ToString("o"))
"@ | Set-Content -Path $notesFile -Encoding utf8

try {
    octopus release create `
        --project $Project `
        --version $ver `
        --channel Default `
        --release-notes-file $notesFile `
        --git-resource "redeploy-romm-docker-stack:romm:$Tag" `
        --ignore-existing `
        --no-prompt `
        --space $Space

    if (-not $SkipDeploy) {
        Write-Host "Deploying $ver to $Environment..."
        octopus release deploy `
            --project $Project `
            --version $ver `
            --environment $Environment `
            --space $Space `
            --no-prompt `
            -f basic
    }
}
finally {
    Remove-Item -LiteralPath $notesFile -Force -ErrorAction SilentlyContinue
}

Write-Host "Done. UI: $($env:OCTOPUS_URL)/app#/Spaces-1/projects/Projects-7"
