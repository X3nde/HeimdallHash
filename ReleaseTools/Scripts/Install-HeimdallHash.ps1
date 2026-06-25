param(
    [string]$InstallRoot = "C:\Program Files\HeimdallHash",
    [string]$ServiceName = "HeimdallHash",
    [string]$DisplayName = "HeimdallHash Service",
    [ValidateSet("auto", "demand", "disabled")]
    [string]$StartType = "auto",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    throw "Debes ejecutar PowerShell como Administrador para instalar el servicio."
}

$PackageRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$SourceService = Join-Path $PackageRoot "Servicio"
$SourceConfigurator = Join-Path $PackageRoot "Configurador"
$ServiceExeSource = Join-Path $SourceService "Heimdallhash.exe"

if (!(Test-Path $ServiceExeSource)) {
    throw "No se encuentra el ejecutable del servicio: $ServiceExeSource"
}

$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($ExistingService -and -not $Force) {
    throw "El servicio '$ServiceName' ya existe. Usa -Force o desinstálalo primero."
}

if ($ExistingService -and $Force) {
    Write-Host "Servicio existente detectado. Deteniendo y eliminando..." -ForegroundColor Yellow
    if ($ExistingService.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $InstallRoot "Servicio") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $InstallRoot "Configurador") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $InstallRoot "Documentacion") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $InstallRoot "Plantillas") -Force | Out-Null

$TargetService = Join-Path $InstallRoot "Servicio"
$TargetConfigurator = Join-Path $InstallRoot "Configurador"

$existingConfig = Join-Path $TargetService "appsettings.json"
if (Test-Path $existingConfig) {
    $backup = Join-Path $TargetService ("appsettings.backup." + (Get-Date -Format "yyyyMMdd_HHmmss") + ".json")
    Copy-Item $existingConfig $backup -Force
    Write-Host "Se ha creado copia de seguridad de appsettings.json: $backup" -ForegroundColor Yellow
}

Copy-Item (Join-Path $SourceService "*") $TargetService -Recurse -Force
Copy-Item (Join-Path $SourceConfigurator "*") $TargetConfigurator -Recurse -Force

if (Test-Path (Join-Path $PackageRoot "Documentacion")) {
    Copy-Item (Join-Path $PackageRoot "Documentacion\*") (Join-Path $InstallRoot "Documentacion") -Recurse -Force
}
if (Test-Path (Join-Path $PackageRoot "Plantillas")) {
    Copy-Item (Join-Path $PackageRoot "Plantillas\*") (Join-Path $InstallRoot "Plantillas") -Recurse -Force
}

$ServiceExe = Join-Path $TargetService "Heimdallhash.exe"
if (!(Test-Path $ServiceExe)) {
    throw "No se encuentra el ejecutable instalado: $ServiceExe"
}

Write-Host "Instalando servicio '$ServiceName'..." -ForegroundColor Cyan
$binPath = '"' + $ServiceExe + '"'
sc.exe create $ServiceName binPath= $binPath start= $StartType DisplayName= $DisplayName | Out-Host
sc.exe description $ServiceName "Servicio HeimdallHash para validación, trazabilidad y entrega controlada de paquetes." | Out-Null

Write-Host "`nInstalación completada." -ForegroundColor Green
Write-Host "Servicio:      $ServiceName"
Write-Host "Ejecutable:    $ServiceExe"
Write-Host "Configurador:  $(Join-Path $TargetConfigurator 'HeimdallhashConfigurator.exe')"
Write-Host "`nPara iniciar: .\Start-HeimdallHash.ps1"
