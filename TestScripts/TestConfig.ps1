function Get-HHTestConfig {
    param(
        [string]$SolutionRoot = ""
    )

    if ([string]::IsNullOrWhiteSpace($SolutionRoot)) {
        if (-not [string]::IsNullOrWhiteSpace($env:HEIMDALLHASH_SOLUTION_ROOT)) {
            $SolutionRoot = $env:HEIMDALLHASH_SOLUTION_ROOT
        }
        else {
            $SolutionRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
        }
    }

    $testRoot = $PSScriptRoot

    [PSCustomObject]@{
        SolutionRoot     = $SolutionRoot
        ProjectRoot      = Join-Path $SolutionRoot "Heimdallhash"
        ProjectFile      = Join-Path $SolutionRoot "Heimdallhash\Heimdallhash.csproj"
        AppSettingsPath  = Join-Path $SolutionRoot "Heimdallhash\appsettings.json"
        TestRoot         = $testRoot
        PackagesRoot     = Join-Path $testRoot "packages"
        ResultsRoot      = Join-Path $testRoot "results"
        ServicePidFile   = Join-Path $testRoot "results\heimdallhash.pid"
        ServiceStdOut    = Join-Path $testRoot "results\heimdallhash_stdout.log"
        ServiceStdErr    = Join-Path $testRoot "results\heimdallhash_stderr.log"
        SummaryCsv       = Join-Path $testRoot "results\test_summary.csv"
        AppSettingsBackupMarker = Join-Path $testRoot "appsettings_backup_path.txt"
        LabRoot          = "C:\HeimdallHashLab"
        DataRoot         = "C:\HeimdallHashData"
        InputDir         = "C:\HeimdallHashLab\Origenes\EntradaProductos"
        DestDownload     = "C:\HeimdallHashLab\Destinos\Centro1234\Download"
        DestUpload       = "C:\HeimdallHashLab\Destinos\Centro1234\Upload"
        CenterId         = "1234"
        Flow             = "Download"
        PollingSeconds   = 5
        DefaultTimeout   = 90
    }
}

function Write-HHStep {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Write-HHOk {
    param([string]$Message)
    Write-Host "OK: $Message" -ForegroundColor Green
}

function Write-HHWarn {
    param([string]$Message)
    Write-Host "AVISO: $Message" -ForegroundColor Yellow
}

function Write-HHFail {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

function Initialize-HHResults {
    param($Config)

    New-Item -ItemType Directory -Force -Path $Config.ResultsRoot | Out-Null

    if (-not (Test-Path $Config.SummaryCsv)) {
        "TestId;Name;Result;TimestampLocal;Detail" | Set-Content -Path $Config.SummaryCsv -Encoding UTF8
    }
}

function Add-HHResult {
    param(
        $Config,
        [string]$TestId,
        [string]$Name,
        [string]$Result,
        [string]$Detail
    )

    Initialize-HHResults -Config $Config

    $line = @(
        $TestId,
        $Name,
        $Result,
        (Get-Date -Format "yyyy-MM-dd HH:mm:ss"),
        ($Detail -replace '"','""')
    ) -join ";"

    Add-Content -Path $Config.SummaryCsv -Value $line -Encoding UTF8
}

function Wait-HHUntil {
    param(
        [scriptblock]$Condition,
        [string]$Description,
        [int]$TimeoutSeconds = 90,
        [int]$DelaySeconds = 1
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if (& $Condition) {
            return $true
        }

        Start-Sleep -Seconds $DelaySeconds
    }

    throw "Timeout esperando condición: $Description"
}

function Assert-HHPathExists {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path $Path)) {
        throw "No existe $Description`: $Path"
    }
}

function Get-HHToday {
    return (Get-Date -Format "yyyyMMdd")
}

function Get-HHNotificationBookPath {
    param($Config)

    return Join-Path $Config.DataRoot "LibrosRegistro\Centros\$($Config.CenterId)\$($Config.Flow)\Notificaciones\Diarios\$(Get-HHToday)_LR_notificaciones.csv"
}

function Assert-HHCsvContains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Description
    )

    if (-not (Test-Path $Path)) {
        throw "No existe el CSV para comprobar $Description`: $Path"
    }

    $content = Get-Content -Path $Path -Raw -Encoding UTF8

    if ($content -notmatch [regex]::Escape($Pattern)) {
        throw "No se encontró '$Pattern' en $Description`: $Path"
    }
}

function Stop-HHServiceProcess {
    param($Config)

    if (Test-Path $Config.ServicePidFile) {
        $pidValue = Get-Content $Config.ServicePidFile -ErrorAction SilentlyContinue | Select-Object -First 1

        if ($pidValue) {
            $process = Get-Process -Id ([int]$pidValue) -ErrorAction SilentlyContinue

            if ($process) {
                Write-HHWarn "Deteniendo proceso HeimdallHash PID=$pidValue"
                Stop-Process -Id ([int]$pidValue) -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 2
            }
        }

        Remove-Item $Config.ServicePidFile -Force -ErrorAction SilentlyContinue
    }
}

function Start-HHServiceProcess {
    param($Config)

    Stop-HHServiceProcess -Config $Config

    New-Item -ItemType Directory -Force -Path $Config.ResultsRoot | Out-Null

    $dllPath = Join-Path $Config.ProjectRoot "bin\Debug\net8.0\Heimdallhash.dll"

    if (-not (Test-Path $dllPath)) {
        throw "No existe Heimdallhash.dll. Compila primero el servicio: $dllPath"
    }

    if (Test-Path $Config.ServiceStdOut) { Remove-Item $Config.ServiceStdOut -Force }
    if (Test-Path $Config.ServiceStdErr) { Remove-Item $Config.ServiceStdErr -Force }

    $args = "`"$dllPath`" --console"

    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $args `
        -WorkingDirectory $Config.ProjectRoot `
        -RedirectStandardOutput $Config.ServiceStdOut `
        -RedirectStandardError $Config.ServiceStdErr `
        -PassThru

    $process.Id | Set-Content -Path $Config.ServicePidFile -Encoding ASCII

    Write-HHOk "Servicio iniciado. PID=$($process.Id)"
    Start-Sleep -Seconds 3
}
