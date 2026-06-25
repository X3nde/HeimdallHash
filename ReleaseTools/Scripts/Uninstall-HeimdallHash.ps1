param(
    [string]$ServiceName = "HeimdallHash",
    [string]$InstallRoot = "C:\Program Files\HeimdallHash",
    [switch]$RemoveFiles
)

$ErrorActionPreference = "Stop"

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    throw "Debes ejecutar PowerShell como Administrador para desinstalar el servicio."
}

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne "Stopped") {
        Write-Host "Deteniendo servicio $ServiceName..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    Write-Host "Eliminando servicio $ServiceName..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Host
    Start-Sleep -Seconds 2
}
else {
    Write-Host "El servicio $ServiceName no existe." -ForegroundColor DarkYellow
}

if ($RemoveFiles) {
    if (Test-Path $InstallRoot) {
        Write-Host "Eliminando archivos de instalación: $InstallRoot" -ForegroundColor Yellow
        Remove-Item $InstallRoot -Recurse -Force
    }
}

Write-Host "Desinstalación completada." -ForegroundColor Green
