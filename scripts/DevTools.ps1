#Requires -Version 7.4
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-StorageDevConfiguration {
    param([Parameter(Mandatory)][string]$ProjectRoot)
    $configDirectory = Join-Path $ProjectRoot '.local'
    $configPath = Join-Path $configDirectory 'development.json'
    if (-not (Test-Path -LiteralPath $configDirectory)) {
        New-Item -ItemType Directory -Path $configDirectory | Out-Null
    }
    if (-not (Test-Path -LiteralPath $configPath)) {
        $configuration = [ordered]@{
            DatabasePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
            SetupToken = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
        }
        $configuration | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding utf8
    }
    Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
}

function Wait-StorageHttp {
    param([Parameter(Mandatory)][string]$Url, [int]$Seconds = 45)
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        try {
            $result = Invoke-WebRequest -Uri $Url -TimeoutSec 2 -SkipHttpErrorCheck
            if ($result.StatusCode -ge 200 -and $result.StatusCode -lt 400) { return }
        } catch { }
        Start-Sleep -Milliseconds 400
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "O serviço não respondeu: $Url. Consulte .local/logs."
}

function Assert-StorageCommand {
    param([int]$ExitCode, [string]$Operation)
    if ($ExitCode -ne 0) { throw "Falha em $Operation (exit $ExitCode)." }
}

function Test-StorageOwnedProcess {
    param([int]$ProcessId, [string]$ProjectRoot)
    $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
    if ($null -eq $processInfo) { return $false }
    return $processInfo.Name -in @('dotnet.exe', 'node.exe') -and
        $processInfo.CommandLine -like "*$ProjectRoot*"
}
