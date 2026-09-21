#Requires -Version 7.4
[CmdletBinding()]
param([switch]$StopDatabase)

. (Join-Path $PSScriptRoot 'DevTools.ps1')
$projectRoot = Split-Path -Parent $PSScriptRoot
$processFile = Join-Path $projectRoot '.local/processes.json'
if (Test-Path -LiteralPath $processFile) {
    $processes = Get-Content -LiteralPath $processFile -Raw | ConvertFrom-Json
    foreach ($processId in @($processes.Web, $processes.Api)) {
        if (Test-StorageOwnedProcess -ProcessId $processId -ProjectRoot $projectRoot) {
            Stop-Process -Id $processId
            Write-Host "Processo StorageManager $processId encerrado."
        }
    }
}
if ($StopDatabase) {
    $configuration = Get-StorageDevConfiguration -ProjectRoot $projectRoot
    $env:STORAGE_DB_PASSWORD = $configuration.DatabasePassword
    Push-Location $projectRoot
    try {
        docker compose stop db
        Assert-StorageCommand $LASTEXITCODE 'parar PostgreSQL'
    } finally { Pop-Location }
}
Write-Host 'Dados e volume do PostgreSQL foram preservados.'
