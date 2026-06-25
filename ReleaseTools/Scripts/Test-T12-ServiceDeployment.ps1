param(
    [string]$InstallRoot = "C:\HeimdallHashDeploymentTest",
    [string]$ServiceName = "HeimdallHash_T12"
)

$ErrorActionPreference = "Stop"

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    throw "T12 requiere PowerShell como Administrador."
}

$PackageRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$install = Join-Path $PSScriptRoot "Install-HeimdallHash.ps1"
$uninstall = Join-Path $PSScriptRoot "Uninstall-HeimdallHash.ps1"

Write-Host "== T12 - Instalación y ejecución como servicio Windows ==" -ForegroundColor Cyan

# Limpieza previa
& $uninstall -ServiceName $ServiceName -InstallRoot $InstallRoot -RemoveFiles -ErrorAction SilentlyContinue

# Instalación en ruta de prueba
& $install -InstallRoot $InstallRoot -ServiceName $ServiceName -DisplayName "HeimdallHash T12 Test Service" -StartType demand -Force

# Crear directorios base habituales usados por appsettings de laboratorio
$paths = @(
    "C:\HeimdallHashData\Temp",
    "C:\HeimdallHashData\Cuarentena",
    "C:\HeimdallHashData\PendienteEntrega",
    "C:\HeimdallHashData\LibrosRegistro",
    "C:\HeimdallHashData\LogsAplicacion",
    "C:\HeimdallHashLab\Origenes\EntradaProductos",
    "C:\HeimdallHashLab\Destinos\Centro1234\Download",
    "C:\HeimdallHashLab\Destinos\Centro1234\Upload"
)

foreach ($p in $paths) {
    New-Item -ItemType Directory -Path $p -Force | Out-Null
}

Write-Host "Iniciando servicio..." -ForegroundColor Yellow
Start-Service -Name $ServiceName
(Get-Service -Name $ServiceName).WaitForStatus("Running", "00:00:30")

$svc = Get-Service -Name $ServiceName
if ($svc.Status -ne "Running") {
    throw "El servicio no está en estado Running."
}

Write-Host "Servicio iniciado correctamente." -ForegroundColor Green

Start-Sleep -Seconds 5

Write-Host "Deteniendo servicio..." -ForegroundColor Yellow
Stop-Service -Name $ServiceName -Force
(Get-Service -Name $ServiceName).WaitForStatus("Stopped", "00:00:30")

$svc = Get-Service -Name $ServiceName
if ($svc.Status -ne "Stopped") {
    throw "El servicio no está en estado Stopped."
}

Write-Host "Servicio detenido correctamente." -ForegroundColor Green

& $uninstall -ServiceName $ServiceName -InstallRoot $InstallRoot -RemoveFiles

Write-Host "T12 PASS - Instalación, arranque, parada y desinstalación correctas." -ForegroundColor Green
