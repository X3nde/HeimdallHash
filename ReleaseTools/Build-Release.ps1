param(
    [string]$Version = "1.0.0",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true,
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$Solution = Join-Path $RepoRoot "Heimdallhash.sln"
$ServiceProject = Join-Path $RepoRoot "Heimdallhash\Heimdallhash.csproj"
$ConfiguratorProject = Join-Path $RepoRoot "HeimdallhashConfigurator\HeimdallhashConfigurator.csproj"

$PublishRoot = Join-Path $RepoRoot "publish"
$ReleaseBase = Join-Path $RepoRoot "release"
$ReleaseName = "HeimdallHash_Release_v$Version"
$ReleaseRoot = Join-Path $ReleaseBase $ReleaseName
$ReleaseZip = Join-Path $ReleaseBase "$ReleaseName.zip"

Write-Host "== HeimdallHash Build Release ==" -ForegroundColor Cyan
Write-Host "RepoRoot: $RepoRoot"
Write-Host "Version:  $Version"
Write-Host "Runtime:  $Runtime"
Write-Host "SelfContained: $SelfContained"

if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "No se ha encontrado dotnet en PATH. Instala .NET SDK 8 o superior en la máquina de compilación."
}

Remove-Item $PublishRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $ReleaseRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $ReleaseZip -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $PublishRoot, $ReleaseBase | Out-Null

Write-Host "`n[1/7] Limpiando solución..." -ForegroundColor Yellow
dotnet clean $Solution -c Release

Write-Host "`n[2/7] Restaurando dependencias..." -ForegroundColor Yellow
dotnet restore $Solution

Write-Host "`n[3/7] Compilando solución en Release..." -ForegroundColor Yellow
dotnet build $Solution -c Release --no-restore

if ($RunTests) {
    Write-Host "`n[4/7] Ejecutando pruebas funcionales T01-T11..." -ForegroundColor Yellow
    $tests = Join-Path $RepoRoot "TestScripts\Run-AllTests.ps1"
    if (!(Test-Path $tests)) {
        throw "No se encuentra $tests"
    }
    & $tests
}
else {
    Write-Host "`n[4/7] Pruebas omitidas. Usa -RunTests para ejecutarlas." -ForegroundColor DarkYellow
}

Write-Host "`n[5/7] Publicando servicio..." -ForegroundColor Yellow
dotnet publish $ServiceProject `
    -c Release `
    -r $Runtime `
    --self-contained $SelfContained `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o (Join-Path $PublishRoot "Servicio")

Write-Host "`n[6/7] Publicando configurador..." -ForegroundColor Yellow
dotnet publish $ConfiguratorProject `
    -c Release `
    -r $Runtime `
    --self-contained $SelfContained `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o (Join-Path $PublishRoot "Configurador")

Write-Host "`n[7/7] Generando paquete de entrega..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $ReleaseRoot | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ReleaseRoot "Servicio") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ReleaseRoot "Configurador") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ReleaseRoot "Scripts") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ReleaseRoot "Plantillas") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ReleaseRoot "Documentacion") | Out-Null

Copy-Item (Join-Path $PublishRoot "Servicio\*") (Join-Path $ReleaseRoot "Servicio") -Recurse -Force
Copy-Item (Join-Path $PublishRoot "Configurador\*") (Join-Path $ReleaseRoot "Configurador") -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot "Scripts\*") (Join-Path $ReleaseRoot "Scripts") -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot "Documentacion\*") (Join-Path $ReleaseRoot "Documentacion") -Recurse -Force

$appsettings = Join-Path $RepoRoot "Heimdallhash\appsettings.json"
if (Test-Path $appsettings) {
    Copy-Item $appsettings (Join-Path $ReleaseRoot "Servicio\appsettings.json") -Force
    Copy-Item $appsettings (Join-Path $ReleaseRoot "Plantillas\appsettings.template.json") -Force
}

@"
HeimdallHash Release v$Version
Generado: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Runtime: $Runtime
SelfContained: $SelfContained

Contenido:
- Servicio: ejecutable del servicio Windows.
- Configurador: herramienta gráfica de configuración.
- Scripts: instalación, desinstalación, arranque, parada y prueba T12.
- Plantillas: configuración base.
- Documentacion: guías de instalación y operación.
"@ | Set-Content -Path (Join-Path $ReleaseRoot "README.txt") -Encoding UTF8

Compress-Archive -Path (Join-Path $ReleaseRoot "*") -DestinationPath $ReleaseZip -Force

Write-Host "`nRelease generado correctamente:" -ForegroundColor Green
Write-Host $ReleaseZip -ForegroundColor Green
Write-Host "`nEntrega al cliente/tutor el ZIP generado en la carpeta release." -ForegroundColor Cyan
