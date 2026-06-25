param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot
$testId = "T06"
$name = "Fichero declarado en DN pero ausente en ZIP"

try {
    Write-HHStep "$testId - $name"

    $pkgName = "HH_INVALID_DECLARED_FILE_MISSING.zip"
    $pkg = Join-Path $config.PackagesRoot $pkgName
    $quarantine = Join-Path $config.DataRoot "Cuarentena\Centros\1234\Download\Productos\$pkgName"
    $lr = Join-Path $config.DataRoot "LibrosRegistro\Centros\1234\Download\Cuarentena\Diarios\$(Get-HHToday)_LR_productos_cuarentena.csv"

    Copy-Item $pkg $config.InputDir -Force

    Wait-HHUntil -Description "Producto con fichero declarado ausente enviado a cuarentena." -TimeoutSeconds $config.DefaultTimeout -Condition {
        Test-Path $quarantine
    }

    Assert-HHPathExists -Path $lr -Description "LR de cuarentena"
    Assert-HHCsvContains -Path $lr -Pattern $pkgName -Description "LR cuarentena"

    Add-HHResult -Config $config -TestId $testId -Name $name -Result "PASS" -Detail "Producto con fichero declarado ausente enviado a cuarentena."
    Write-HHOk "$testId superado."
}
catch {
    Add-HHResult -Config $config -TestId $testId -Name $name -Result "FAIL" -Detail $_.Exception.Message
    Write-HHFail $_.Exception.Message
    exit 1
}
