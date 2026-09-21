#Requires -Version 7.4
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$statePath = [IO.Path]::GetFullPath((Join-Path $projectRoot '.local/integration.json'))
if (-not $statePath.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Arquivo de estado fora do workspace.'
}
if (-not (Test-Path -LiteralPath $statePath)) {
    Write-Host 'Nenhum ambiente temporário de integração encontrado.'
    exit 0
}

$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if ($state.Container -notmatch '^storagemanager-test-[a-f0-9]{12}$') {
    throw 'Nome de contêiner temporário inválido.'
}

$apiProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$($state.ApiProcess)" -ErrorAction SilentlyContinue
if ($null -ne $apiProcess) {
    if (-not $apiProcess.CommandLine.Contains($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'O processo registrado não pertence a este workspace.'
    }
    Stop-Process -Id $state.ApiProcess
}

$purpose = docker inspect --format '{{ index .Config.Labels "storagemanager.purpose" }}' $state.Container 2>$null
if ($LASTEXITCODE -eq 0) {
    if ($purpose -ne 'integration-test') { throw 'O contêiner não possui o rótulo de teste esperado.' }
    docker rm -f $state.Container | Out-Null
}

$runId = $state.Container -replace '^storagemanager-test-', ''
$keysRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.local/integration-keys'))
$keysPath = [IO.Path]::GetFullPath((Join-Path $keysRoot $runId))
if (-not $keysPath.StartsWith($keysRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Diretório de chaves temporárias inválido.'
}
if (Test-Path -LiteralPath $keysPath) {
    Remove-Item -LiteralPath $keysPath -Recurse -Force
}
$buildRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.local/integration-bin'))
$buildPath = [IO.Path]::GetFullPath((Join-Path $buildRoot $runId))
if (-not $buildPath.StartsWith($buildRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Diretório de compilação temporária inválido.'
}
if (Test-Path -LiteralPath $buildPath) {
    Remove-Item -LiteralPath $buildPath -Recurse -Force
}
Remove-Item -LiteralPath $statePath -Force
Write-Host 'API, banco efêmero e chaves do teste de integração foram removidos.'
