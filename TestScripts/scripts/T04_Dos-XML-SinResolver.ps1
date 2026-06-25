param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot
$testId = "T04"
$name = "Producto con dos XML a cuarentena SinResolver"

try {
    Write-HHStep "$testId - $name"

    $pkgName = "HH_INVALID_TWO_XML.zip"
    $pkg = Join-Path $config.PackagesRoot $pkgName
    $quarantine = Join-Path $config.DataRoot "Cuarentena\SinResolver\Productos\$pkgName"
    $lr = Join-Path $config.DataRoot "LibrosRegistro\SinResolver\Cuarentena\Diarios\$(Get-HHToday)_LR_productos_cuarentena_sinresolver.csv"

    Copy-Item $pkg $config.InputDir -Force

    Wait-HHUntil -Description "producto con dos XML en cuarentena SinResolver" -TimeoutSeconds $config.DefaultTimeout -Condition {
        Test-Path $quarantine
    }

    Assert-HHPathExists -Path $lr -Description "LR cuarentena SinResolver"
    Assert-HHCsvContains -Path $lr -Pattern $pkgName -Description "LR cuarentena SinResolver"

    Add-HHResult -Config $config -TestId $testId -Name $name -Result "PASS" -Detail "Producto con múltiples XML en SinResolver."
    Write-HHOk "$testId superado."
}
catch {
    Add-HHResult -Config $config -TestId $testId -Name $name -Result "FAIL" -Detail $_.Exception.Message
    Write-HHFail $_.Exception.Message
    exit 1
}
