#Requires -Version 7.0
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$credPath = Join-Path $env:USERPROFILE '.creds\paytondesktop.cred.xml'
$cred = Import-Clixml -LiteralPath $credPath
$taskId = '108d4f7f-66eb-4c22-8c37-90877eed995e'
$deadline = [DateTime]::UtcNow.AddHours(10)

while ([DateTime]::UtcNow -lt $deadline) {
    $r = $null
    $attempt = 0
    while ($attempt -lt 5 -and $null -eq $r) {
        $attempt++
        try {
            $r = Invoke-Command -ComputerName 'PAYTON-DESKTOP' -Credential $cred -ScriptBlock {
                param([string]$WantedTaskId)
                $token = $null
                foreach ($line in [System.IO.File]::ReadAllLines('C:\deploy\RomM-clean\.env')) {
                    if ($line -match '^ROMM_API_TOKEN=(.*)$') {
                        $token = $Matches[1].Trim().Trim([char]39)
                    }
                }
                $h = @{ Authorization = ('Bearer ' + $token) }
                $status = Invoke-RestMethod -Uri 'http://127.0.0.1:8080/api/tasks/status' -Headers $h -TimeoutSec 30
                $stats = Invoke-RestMethod -Uri 'http://127.0.0.1:8080/api/stats' -Headers $h -TimeoutSec 30
                $scan = @($status | Where-Object { $_.task_id -eq $WantedTaskId } | Select-Object -First 1)
                [pscustomobject]@{
                    TaskStatus = [string]$scan.status
                    Roms       = [int]$stats.ROMS
                }
            } -ArgumentList $taskId -ErrorAction Stop
        } catch {
            if ($attempt -ge 5) { throw }
            Start-Sleep -Seconds (15 * $attempt)
        }
    }

    $st = [string]$r.TaskStatus
    if ($st -eq 'finished' -or $st -eq 'completed') {
        if ([int]$r.Roms -ge 28000) {
            Write-Output 'DONE'
            exit 0
        }
        Write-Output 'FAILED'
        exit 1
    }
    if ($st -eq 'failed' -or $st -eq 'stopped' -or $st -eq 'canceled') {
        Write-Output 'FAILED'
        exit 1
    }

    Start-Sleep -Seconds 120
}

Write-Output 'FAILED'
exit 1
