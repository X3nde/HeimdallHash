param([string]$SolutionRoot = "")

. "$PSScriptRoot\..\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot
$testId = "T05"
$name = "PendienteEntrega y reintento automático"

try {
    Write-HHStep "$testId - $name"

    $pkgName = "HH_VALID_PENDING_1234_Download.zip"
    $pkg = Join-Path $config.PackagesRoot $pkgName
    $pending = Join-Path $config.DataRoot "PendienteEntrega\Centros\1234\Download\Productos\$pkgName"
    $dest = Join-Path $config.DestDownload $pkgName
    $lr = Join-Path $config.DataRoot "LibrosRegistro\Centros\1234\Download\PendienteEntrega\Diarios\$(Get-HHToday)_LR_productos_pendiente_entrega.csv"
    $notif = Get-HHNotificationBookPath -Config $config

    Remove-Item $config.DestDownload -Recurse -Force -ErrorAction SilentlyContinue

    Copy-Item $pkg $config.InputDir -Force

    Wait-HHUntil -Description "producto válido en PendienteEntrega" -TimeoutSeconds $config.DefaultTimeout -Condition {
        Test-Path $pending
    }

    Wait-HHUntil -Description "LR PendienteEntrega generado con estado PENDING" -TimeoutSeconds $config.DefaultTimeout -Condition {
        (Test-Path $lr) -and ((Get-Content $lr -Raw -Encoding UTF8) -match "PENDING")
    }

    Wait-HHUntil -Description "notificación PENDING_CREATED registrada" -TimeoutSeconds $config.DefaultTimeout -Condition {
        (Test-Path $notif) -and ((Get-Content $notif -Raw -Encoding UTF8) -match "PENDING_CREATED")
    }

    New-Item -ItemType Directory -Force -Path $config.DestDownload | Out-Null

    Wait-HHUntil -Description "producto pendiente entregado en destino recuperado" -TimeoutSeconds $config.DefaultTimeout -Condition {
        (Test-Path $dest) -and (-not (Test-Path $pending))
    }

    Wait-HHUntil -Description "LR PendienteEntrega actualizado con estado DELIVERED" -TimeoutSeconds $config.DefaultTimeout -Condition {
        (Test-Path $lr) -and ((Get-Content $lr -Raw -Encoding UTF8) -match "DELIVERED")
    }

    Wait-HHUntil -Description "notificación PENDING_DELIVERED registrada" -TimeoutSeconds $config.DefaultTimeout -Condition {
        (Test-Path $notif) -and ((Get-Content $notif -Raw -Encoding UTF8) -match "PENDING_DELIVERED")
    }

    Add-HHResult -Config $config -TestId $testId -Name $name -Result "PASS" -Detail "Pendiente creado, reintentado y entregado."
    Write-HHOk "$testId superado."
}
catch {
    Add-HHResult -Config $config -TestId $testId -Name $name -Result "FAIL" -Detail $_.Exception.Message
    Write-HHFail $_.Exception.Message
    exit 1
}
