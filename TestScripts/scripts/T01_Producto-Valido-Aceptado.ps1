param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot
$testId = "T01"
$name = "Producto válido aceptado"

try {
    Write-HHStep "$testId - $name"

    $pkg = Join-Path $config.PackagesRoot "HH_VALID_DN_1234_Download.zip"
    $dest = Join-Path $config.DestDownload "HH_VALID_DN_1234_Download.zip"
    $lr = Join-Path $config.DataRoot "LibrosRegistro\Centros\1234\Download\Aceptados\Diarios\$(Get-HHToday)_LR_productos_aceptados.csv"

    Copy-Item $pkg $config.InputDir -Force

    Wait-HHUntil -Description "producto válido en destino" -TimeoutSeconds $config.DefaultTimeout -Condition {
        Test-Path $dest
    }

    Assert-HHPathExists -Path $lr -Description "LR de aceptados"
    Assert-HHCsvContains -Path $lr -Pattern "HH_VALID_DN_1234_Download.zip" -Description "LR aceptados"

    Add-HHResult -Config $config -TestId $testId -Name $name -Result "PASS" -Detail "Producto aceptado y registrado."
    Write-HHOk "$testId superado."
}
catch {
    Add-HHResult -Config $config -TestId $testId -Name $name -Result "FAIL" -Detail $_.Exception.Message
    Write-HHFail $_.Exception.Message
    exit 1
}
