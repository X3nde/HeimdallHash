param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot

Write-HHStep "90 - Parada de HeimdallHash"

Stop-HHServiceProcess -Config $config
Write-HHOk "Servicio detenido."
