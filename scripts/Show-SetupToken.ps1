#Requires -Version 7.4
[CmdletBinding()]
param()

$projectRoot = Split-Path -Parent $PSScriptRoot
$configurationFile = Join-Path $projectRoot '.local/development.json'
if (-not (Test-Path -LiteralPath $configurationFile)) {
    throw 'Configuração local ainda não existe. Execute scripts/Start-Dev.ps1 primeiro.'
}

$configuration = Get-Content -LiteralPath $configurationFile -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($configuration.SetupToken)) {
    throw 'A configuração local não contém uma chave de instalação.'
}

Write-Host 'Chave de instalação local (não compartilhe):'
Write-Output $configuration.SetupToken
