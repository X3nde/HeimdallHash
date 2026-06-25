param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot
$testId = "T02"
$name = "Hash incorrecto a cuarentena"

try {
    Write-HHStep "$testId - $name"

    $pkgName = "HH_INVALID_HASH_1234_Download.zip"
    $pkg = Join-Path $config.PackagesRoot $pkgName
    $quarantine = Join-Path $config.DataRoot "Cuarentena\Centros\1234\Download\Productos\$pkgName"
    $lr = Join-Path $config.DataRoot "LibrosRegistro\Centros\1234\Download\Cuarentena\Diarios\$(Get-HHToday)_LR_productos_cuarentena.csv"
    $notif = Get-HHNotificationBookPath -Config $config

    Copy-Item $pkg $config.InputDir -Force

    Wait-HHUntil -Description "producto con hash incorrecto en cuarentena" -TimeoutSeconds $config.DefaultTimeout -Condition {
        Test-Path $quarantine
    }

    Assert-HHPathExists -Path $lr -Description "LR de cuarentena"
    Assert-HHCsvContains -Path $lr -Pattern $pkgName -Description "LR cuarentena"

    Wait-HHUntil -Description "notificación QUARANTINE_CREATED registrada" -TimeoutSeconds $config.DefaultTimeout -Condition {
        (Test-Path $notif) -and ((Get-Content $notif -Raw -Encoding UTF8) -match "QUARANTINE_CREATED")
    }

    Add-HHResult -Config $config -TestId $testId -Name $name -Result "PASS" -Detail "Producto en cuarentena y notificación registrada."
    Write-HHOk "$testId superado."
}
catch {
    Add-HHResult -Config $config -TestId $testId -Name $name -Result "FAIL" -Detail $_.Exception.Message
    Write-HHFail $_.Exception.Message
    exit 1
}
