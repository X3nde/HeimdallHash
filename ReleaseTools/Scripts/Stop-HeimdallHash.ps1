param(
    [string]$ServiceName = "HeimdallHash"
)

$ErrorActionPreference = "Stop"
$svc = Get-Service -Name $ServiceName -ErrorAction Stop

if ($svc.Status -ne "Stopped") {
    Stop-Service -Name $ServiceName -Force
    $svc.WaitForStatus("Stopped", "00:00:20")
}

Get-Service -Name $ServiceName
