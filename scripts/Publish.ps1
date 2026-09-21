#Requires -Version 7.4
[CmdletBinding()]
param([string]$Output = 'artifacts/publish', [switch]$SkipInstall)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$frontendDirectory = Join-Path $projectRoot 'src/frontend'
$apiProject = Join-Path $projectRoot 'src/backend/StorageManager.Api/StorageManager.Api.csproj'
$webRoot = Join-Path $projectRoot 'src/backend/StorageManager.Api/wwwroot'
$outputDirectory = Join-Path $projectRoot $Output

Push-Location $frontendDirectory
try {
    if (-not $SkipInstall) {
        npm ci
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao instalar dependências do frontend.' }
    }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o frontend.' }
} finally { Pop-Location }

if (Test-Path -LiteralPath $webRoot) {
    $resolvedWebRoot = [IO.Path]::GetFullPath($webRoot)
    $resolvedApiRoot = [IO.Path]::GetFullPath((Split-Path -Parent $apiProject))
    if (-not $resolvedWebRoot.StartsWith($resolvedApiRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Destino wwwroot inválido.'
    }
    Remove-Item -LiteralPath $resolvedWebRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $webRoot -Force | Out-Null
Copy-Item -Path (Join-Path $frontendDirectory 'dist/*') -Destination $webRoot -Recurse -Force

dotnet publish $apiProject --configuration Release --output $outputDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar a aplicação.' }
Write-Host "Aplicação publicada em $outputDirectory"
Write-Host 'Configure ConnectionStrings__Default, Setup__Token, DataProtection__Path e HTTPS no ambiente de destino.'
