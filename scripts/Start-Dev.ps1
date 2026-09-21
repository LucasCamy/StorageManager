#Requires -Version 7.4
[CmdletBinding()]
param([switch]$SkipInstall)

. (Join-Path $PSScriptRoot 'DevTools.ps1')
$projectRoot = Split-Path -Parent $PSScriptRoot
$configuration = Get-StorageDevConfiguration -ProjectRoot $projectRoot
$apiProject = Join-Path $projectRoot 'src/backend/StorageManager.Api/StorageManager.Api.csproj'
$apiDll = Join-Path $projectRoot 'src/backend/StorageManager.Api/bin/Debug/net10.0/StorageManager.Api.dll'
$frontendDirectory = Join-Path $projectRoot 'src/frontend'
$vitePath = Join-Path $frontendDirectory 'node_modules/vite/bin/vite.js'
$processFile = Join-Path $projectRoot '.local/processes.json'
$logDirectory = Join-Path $projectRoot '.local/logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

foreach ($port in @(5080, 5178)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "A porta $port já está em uso. Se esta instalação estiver aberta, use scripts/Stop-Dev.ps1 primeiro."
    }
}

$env:STORAGE_DB_PASSWORD = $configuration.DatabasePassword
$env:ConnectionStrings__Default = "Host=127.0.0.1;Port=55432;Database=storagemanager;Username=storagemanager;Password=$($configuration.DatabasePassword)"
$env:Setup__Token = $configuration.SetupToken
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DataProtection__Path = Join-Path $projectRoot '.local/keys'
$env:API_PROXY_TARGET = 'http://127.0.0.1:5080'

Push-Location $projectRoot
try {
    docker compose up -d --wait db
    Assert-StorageCommand $LASTEXITCODE 'inicializar PostgreSQL'
    dotnet build $apiProject --nologo
    Assert-StorageCommand $LASTEXITCODE 'compilar backend'
    dotnet $apiDll --migrate
    Assert-StorageCommand $LASTEXITCODE 'aplicar migrations'
    if (-not $SkipInstall -or -not (Test-Path -LiteralPath $vitePath)) {
        Push-Location $frontendDirectory
        try {
            npm ci
            Assert-StorageCommand $LASTEXITCODE 'instalar frontend'
        } finally { Pop-Location }
    }

    $apiProcess = $null
    $webProcess = $null
    try {
        $apiProcess = Start-Process -FilePath (Get-Command dotnet.exe).Source -ArgumentList @(
            ('"' + $apiDll + '"'), '--urls', 'http://127.0.0.1:5080'
        ) -WorkingDirectory (Split-Path -Parent $apiProject) -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $logDirectory 'api.log') `
            -RedirectStandardError (Join-Path $logDirectory 'api.error.log')
        Wait-StorageHttp 'http://127.0.0.1:5080/health'
        $webProcess = Start-Process -FilePath (Get-Command node.exe).Source -ArgumentList @(
            ('"' + $vitePath + '"'), '--host', '127.0.0.1', '--port', '5178', '--strictPort'
        ) -WorkingDirectory $frontendDirectory -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $logDirectory 'web.log') `
            -RedirectStandardError (Join-Path $logDirectory 'web.error.log')
        Wait-StorageHttp 'http://127.0.0.1:5178'
        @{ Api = $apiProcess.Id; Web = $webProcess.Id } | ConvertTo-Json |
            Set-Content -LiteralPath $processFile -Encoding utf8
    } catch {
        foreach ($startedProcess in @($apiProcess, $webProcess)) {
            if ($null -ne $startedProcess -and -not $startedProcess.HasExited) {
                Stop-Process -Id $startedProcess.Id -ErrorAction SilentlyContinue
            }
        }
        throw
    }
    Write-Host "`nStorageManager: http://localhost:5178"
    $setupStatus = Invoke-RestMethod 'http://127.0.0.1:5080/api/v1/setup/status'
    if ($setupStatus.required) {
        Write-Host "Chave de instalação (apenas para o primeiro cadastro): $($configuration.SetupToken)"
    }
    Write-Host 'Logs e configuração de desenvolvimento em .local/. Não compartilhe development.json.'
} finally { Pop-Location }
