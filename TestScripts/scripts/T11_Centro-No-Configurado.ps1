param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot
$testId = "T11"
$name = "CenterId no configurado en CenterRoutes"

try {
    Write-HHStep "$testId - $name"

    $pkgName = "HH_INVALID_UNKNOWN_CENTER_9999.zip"
    $pkg = Join-Path $config.PackagesRoot $pkgName
    $quarantine = Join-Path $config.DataRoot "Cuarentena\Centros\9999\Download\Productos\$pkgName"
    $lr = Join-Path $config.DataRoot "LibrosRegistro\Centros\9999\Download\Cuarentena\Diarios\$(Get-HHToday)_LR_productos_cuarentena.csv"

    Copy-Item $pkg $config.InputDir -Force

    Wait-HHUntil -Description "producto con CenterId sin ruta configurada enviado a cuarentena" -TimeoutSeconds $config.DefaultTimeout -Condition {
        Test-Path $quarantine
    }

    Assert-HHPathExists -Path $lr -Description "LR cuarentena centro 9999"
    Assert-HHCsvContains -Path $lr -Pattern $pkgName -Description "LR cuarentena centro 9999"

    Add-HHResult -Config $config -TestId $testId -Name $name -Result "PASS" -Detail "Producto con CenterId no configurado enviado a cuarentena del centro 9999."
    Write-HHOk "$testId superado."
}
catch {
    Add-HHResult -Config $config -TestId $testId -Name $name -Result "FAIL" -Detail $_.Exception.Message
    Write-HHFail $_.Exception.Message
    exit 1
}
