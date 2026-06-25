param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot

Write-HHStep "01 - Compilación del servicio HeimdallHash"

dotnet build $config.ProjectFile -c Debug

if ($LASTEXITCODE -ne 0) {
    throw "La compilación del servicio ha fallado."
}

Write-HHOk "Compilación correcta."
