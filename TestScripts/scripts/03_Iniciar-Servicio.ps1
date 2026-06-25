param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot

Write-HHStep "03 - Arranque de HeimdallHash en segundo plano"

Start-HHServiceProcess -Config $config
