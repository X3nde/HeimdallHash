param(
    [switch]$Force,
    [string]$SolutionRoot = ""
)

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot

Write-HHStep "00 - Reset del laboratorio HeimdallHash"

if (-not $Force) {
    Write-HHWarn "Esta operación elimina $($config.LabRoot), $($config.DataRoot), paquetes y resultados de prueba."
    $answer = Read-Host "Escribe SI para continuar"
    if ($answer -ne "SI") {
        throw "Operación cancelada por el usuario."
    }
}

Stop-HHServiceProcess -Config $config

Remove-Item $config.LabRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $config.DataRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $config.PackagesRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $config.ResultsRoot -Recurse -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $config.InputDir | Out-Null
New-Item -ItemType Directory -Force -Path $config.DestDownload | Out-Null
New-Item -ItemType Directory -Force -Path $config.DestUpload | Out-Null
New-Item -ItemType Directory -Force -Path $config.PackagesRoot | Out-Null
New-Item -ItemType Directory -Force -Path $config.ResultsRoot | Out-Null

$appSettingsBackup = "$($config.AppSettingsPath).backup.testkit.$(Get-Date -Format 'yyyyMMddHHmmss')"

if (Test-Path $config.AppSettingsPath) {
    Copy-Item $config.AppSettingsPath $appSettingsBackup -Force
    $appSettingsBackup | Set-Content -Path $config.AppSettingsBackupMarker -Encoding UTF8
    Write-HHWarn "Backup de appsettings creado: $appSettingsBackup"
}
else {
    "" | Set-Content -Path $config.AppSettingsBackupMarker -Encoding UTF8
}

$appsettings = @"
{
  "PollingIntervalSeconds": 5,
  "ConcurrencyLevel": 2,
  "Storage": {
    "TempDirectory": "C:\\HeimdallHashData\\Temp",
    "CleanTemporaryFilesAfterProcessing": true
  },
  "Hash": {
    "DefaultAlgorithm": "SHA256",
    "AllowedAlgorithms": [ "MD5", "SHA1", "SHA256", "SHA384", "SHA512" ]
  },
  "RetryPolicy": {
    "MaxAttempts": 5,
    "DelayMilliseconds": 1000
  },
  "StabilityCheck": {
    "MinFileAgeSeconds": 2
  },
  "Email": {
    "SmtpServer": "",
    "Sender": "",
    "Recipients": [],
    "Subject": "HeimdallHash - Notificación",
    "Port": 25,
    "Password": "",
    "EnableSsl": false,
    "UseCredentials": false
  },
  "WatchRoutes": [
    {
      "Name": "RutaLaboratorioDeliveryNote",
      "Flow": "Download",
      "InputDirectory": "C:\\HeimdallHashLab\\Origenes\\EntradaProductos",
      "ValidationMode": "DeliveryNote",
      "OutputDirectory": "",
      "Enabled": true,
      "QuarantineDirectory": "",
      "TempDirectory": ""
    }
  ],
  "CenterRoutes": [
    {
      "CenterId": "1234",
      "Flow": "Download",
      "DestinationPath": "C:\\HeimdallHashLab\\Destinos\\Centro1234\\Download",
      "Enabled": true,
      "Description": "Centro de laboratorio 1234 - recepción"
    },
    {
      "CenterId": "1234",
      "Flow": "Upload",
      "DestinationPath": "C:\\HeimdallHashLab\\Destinos\\Centro1234\\Upload",
      "Enabled": true,
      "Description": "Centro de laboratorio 1234 - envío"
    }
  ],
  "ArchiveProcessing": {
    "ExtractorMode": "SharpCompress",
    "EnableSevenZipFallback": true,
    "SevenZipExecutablePath": "C:\\Program Files\\7-Zip\\7z.exe",
    "SupportedExtensions": [ ".zip", ".7z", ".rar" ],
    "TemporaryDirectory": "",
    "RejectPasswordProtectedArchives": true,
    "CleanTemporaryFilesAfterProcessing": true
  },
  "Quarantine": {
    "RootDirectory": "C:\\HeimdallHashData\\Cuarentena",
    "CentersDirectoryName": "Centros",
    "UnresolvedDirectoryName": "SinResolver",
    "ProductsDirectoryName": "Productos",
    "AllowManualRetryFromGui": true,
    "AllowManualReleaseWithoutValidation": false
  },
  "PendingDelivery": {
    "Enabled": true,
    "RootDirectory": "C:\\HeimdallHashData\\PendienteEntrega",
    "CentersDirectoryName": "Centros",
    "ProductsDirectoryName": "Productos",
    "RetryEveryCycles": 1,
    "MaxRetryAttempts": 0,
    "NotifyAfterFailedCycles": 5,
    "MaxPendingDays": 0,
    "MoveToQuarantineAfterMaxPendingDays": false,
    "RevalidateBeforeFinalDelivery": true,
    "RequireDestinationWriteProbe": true
  },
  "RecordBooks": {
    "RootDirectory": "C:\\HeimdallHashData\\LibrosRegistro",
    "CentersDirectoryName": "Centros",
    "UnresolvedDirectoryName": "SinResolver",
    "DailyDirectoryName": "Diarios",
    "MonthlyDirectoryName": "Mensuales",
    "ArchiveDirectoryName": "Archivo",
    "AcceptedDirectoryName": "Aceptados",
    "QuarantineDirectoryName": "Cuarentena",
    "PendingDeliveryDirectoryName": "PendienteEntrega",
    "NotificationsDirectoryName": "Notificaciones",
    "ManualActionsDirectoryName": "AccionesManuales",
    "AcceptedDailyPattern": "yyyyMMdd_LR_productos_aceptados.csv",
    "QuarantineDailyPattern": "yyyyMMdd_LR_productos_cuarentena.csv",
    "PendingDeliveryDailyPattern": "yyyyMMdd_LR_productos_pendiente_entrega.csv",
    "NotificationsDailyPattern": "yyyyMMdd_LR_notificaciones.csv",
    "ManualActionsDailyPattern": "yyyyMMdd_LR_acciones_manuales.csv",
    "AcceptedMonthlyPattern": "yyyyMM_LR_productos_aceptados.csv",
    "QuarantineMonthlyPattern": "yyyyMM_LR_productos_cuarentena.csv",
    "PendingDeliveryMonthlyPattern": "yyyyMM_LR_productos_pendiente_entrega.csv",
    "NotificationsMonthlyPattern": "yyyyMM_LR_notificaciones.csv",
    "UnresolvedQuarantineDailyPattern": "yyyyMMdd_LR_productos_cuarentena_sinresolver.csv",
    "UnresolvedQuarantineMonthlyPattern": "yyyyMM_LR_productos_cuarentena_sinresolver.csv",
    "UnresolvedNotificationsDailyPattern": "yyyyMMdd_LR_notificaciones_sinresolver.csv",
    "UnresolvedNotificationsMonthlyPattern": "yyyyMM_LR_notificaciones_sinresolver.csv",
    "Delimiter": ";",
    "MaxWriteAttempts": 3,
    "RetryDelayMilliseconds": 500,
    "MergePreviousDaysOnCycleStart": true,
    "MoveDailyToArchiveAfterMerge": true,
    "DeleteDailyAfterMonthlyMerge": false
  },
  "ApplicationLogs": {
    "RootDirectory": "C:\\HeimdallHashData\\LogsAplicacion",
    "ServiceDirectoryName": "Servicio",
    "ErrorsDirectoryName": "Errores",
    "DailyDirectoryName": "Diarios",
    "MonthlyDirectoryName": "Mensuales",
    "ArchiveDirectoryName": "Archivo",
    "ServiceDailyPattern": "yyyyMMdd_log_servicio.csv",
    "ErrorsDailyPattern": "yyyyMMdd_log_errores.csv",
    "ServiceMonthlyPattern": "yyyyMM_log_servicio.csv",
    "ErrorsMonthlyPattern": "yyyyMM_log_errores.csv",
    "Delimiter": ";",
    "MaxWriteAttempts": 3,
    "RetryDelayMilliseconds": 500
  },
  "Notifications": {
    "Enabled": true,
    "NotifyOnQuarantine": true,
    "NotifyOnPendingCreated": true,
    "NotifyOnPendingDelivered": true,
    "NotifyOnPendingStillFailed": false,
    "NotifyPendingStillFailedAfterCycles": 5,
    "PreventDuplicateNotifications": true,
    "RetryPendingNotifications": true,
    "MaxNotificationAttempts": 3,
    "QuarantineSubjectPrefix": "[HeimdallHash] Producto enviado a cuarentena",
    "PendingCreatedSubjectPrefix": "[HeimdallHash] Producto pendiente de entrega",
    "PendingDeliveredSubjectPrefix": "[HeimdallHash] Producto pendiente entregado",
    "PendingStillFailedSubjectPrefix": "[HeimdallHash] Producto pendiente aún no entregado"
  },
  "DirectoryManagement": {
    "ValidateDirectoriesOnStartup": true,
    "AskUserBeforeCreatingDirectoriesFromConfigurator": true,
    "AllowConfiguratorToCreateMissingDirectories": true,
    "AllowServiceToCreateInternalDirectories": true,
    "AllowServiceToCreateFlowDirectories": false,
    "DisableRouteWhenInputDirectoryIsMissing": true,
    "UsePendingDeliveryWhenDestinationIsMissing": true
  }
}
"@

$appsettings | Set-Content -Path $config.AppSettingsPath -Encoding UTF8

Initialize-HHResults -Config $config
Write-HHOk "Laboratorio reiniciado y appsettings de prueba aplicado."
