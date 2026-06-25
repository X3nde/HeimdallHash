param(
    [string]$ServiceName = "HeimdallHash"
)

$ErrorActionPreference = "Stop"
$svc = Get-Service -Name $ServiceName -ErrorAction Stop

if ($svc.Status -ne "Running") {
    Start-Service -Name $ServiceName
    $svc.WaitForStatus("Running", "00:00:20")
}

Get-Service -Name $ServiceName
