#Requires -Version 7.4
[CmdletBinding()]
param([switch]$KeepRunning, [int]$DatabasePort = 55433, [int]$ApiPort = 5081)

. (Join-Path $PSScriptRoot 'DevTools.ps1')
$projectRoot = Split-Path -Parent $PSScriptRoot
$runId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$containerName = "storagemanager-test-$runId"
$logDirectory = Join-Path $projectRoot '.local/logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$apiProject = Join-Path $projectRoot 'src/backend/StorageManager.Api/StorageManager.Api.csproj'
$buildRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.local/integration-bin'))
$buildDirectory = [IO.Path]::GetFullPath((Join-Path $buildRoot $runId))
if (-not $buildDirectory.StartsWith($buildRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Diretório temporário de compilação inválido.'
}
$apiDll = Join-Path $buildDirectory 'StorageManager.Api.dll'
$databasePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$setupToken = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$adminPassword = 'Test!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16)) + 'a'
$adminEmail = 'admin.integration@example.test'
$apiProcess = $null
$containerCreated = $false
$testExitCode = 1

foreach ($port in @($DatabasePort, $ApiPort)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "Porta $port ocupada. Escolha portas livres; nenhum processo existente será encerrado."
    }
}

Push-Location $projectRoot
try {
    $env:POSTGRES_PASSWORD = $databasePassword
    docker run -d --name $containerName --label storagemanager.purpose=integration-test `
        -p "127.0.0.1:${DatabasePort}:5432" -e POSTGRES_DB=storagemanager_test `
        -e POSTGRES_USER=storage_test -e POSTGRES_PASSWORD --tmpfs /var/lib/postgresql postgres:18 | Out-Null
    Assert-StorageCommand $LASTEXITCODE 'criar PostgreSQL de teste'
    $containerCreated = $true
    $ready = $false
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        docker exec $containerName pg_isready -U storage_test -d storagemanager_test *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'PostgreSQL de teste não ficou pronto.' }
    $env:ConnectionStrings__Default = "Host=127.0.0.1;Port=$DatabasePort;Database=storagemanager_test;Username=storage_test;Password=$databasePassword"
    $env:Setup__Token = $setupToken
    $env:TEST_SETUP_TOKEN = $setupToken
    $env:TEST_ADMIN_PASSWORD = $adminPassword
    $env:TEST_ADMIN_EMAIL = $adminEmail
    $env:API_BASE_URL = "http://127.0.0.1:$ApiPort"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DataProtection__Path = Join-Path $projectRoot ".local/integration-keys/$runId"
    dotnet build $apiProject --output $buildDirectory --nologo
    Assert-StorageCommand $LASTEXITCODE 'compilar API para teste'
    dotnet $apiDll --migrate
    Assert-StorageCommand $LASTEXITCODE 'migrar banco de teste'
    $apiProcess = Start-Process -FilePath (Get-Command dotnet.exe).Source -ArgumentList @(
        ('"' + $apiDll + '"'), '--urls', $env:API_BASE_URL
    ) -WorkingDirectory (Split-Path -Parent $apiProject) -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $logDirectory 'integration-api.log') `
        -RedirectStandardError (Join-Path $logDirectory 'integration-api.error.log')
    Wait-StorageHttp "$($env:API_BASE_URL)/health"
    node (Join-Path $PSScriptRoot 'test-integration.mjs')
    $testExitCode = $LASTEXITCODE
    if ($KeepRunning) {
        @{
            Container = $containerName; ApiProcess = $apiProcess.Id; ApiUrl = $env:API_BASE_URL
            AdminEmail = $adminEmail; AdminPassword = $adminPassword; DatabasePort = $DatabasePort
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot '.local/integration.json') -Encoding utf8
        Write-Host 'Ambiente de teste mantido para QA; detalhes privados em .local/integration.json.'
    }
} finally {
    if (-not $KeepRunning -or $null -eq $apiProcess) {
        if ($null -ne $apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
        if ($containerCreated) { docker rm -f $containerName | Out-Null }
        if (Test-Path -LiteralPath $buildDirectory) { Remove-Item -LiteralPath $buildDirectory -Recurse -Force }
    }
    Pop-Location
}
exit $testExitCode
