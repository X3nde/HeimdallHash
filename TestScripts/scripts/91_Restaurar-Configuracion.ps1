param(
    [string]$SolutionRoot = ""
)

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot

Write-HHStep "91 - Restauración de appsettings.json"

if (-not (Test-Path $config.AppSettingsBackupMarker)) {
    Write-HHWarn "No existe marcador de backup. No se restaura appsettings.json."
    exit 0
}

$backupPath = Get-Content $config.AppSettingsBackupMarker -ErrorAction SilentlyContinue | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($backupPath)) {
    Write-HHWarn "El marcador de backup está vacío. No había appsettings.json original que restaurar."
    exit 0
}

if (-not (Test-Path $backupPath)) {
    Write-HHWarn "No se ha encontrado el backup de appsettings: $backupPath"
    exit 0
}

Copy-Item $backupPath $config.AppSettingsPath -Force
Write-HHOk "appsettings.json restaurado desde: $backupPath"
