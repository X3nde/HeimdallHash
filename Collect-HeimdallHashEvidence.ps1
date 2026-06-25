param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [string]$ReleaseVersion = "1.0.0",
    [string]$EvidenceRoot = "",
    [switch]$RunFunctionalTests,
    [switch]$RunT12,
    [switch]$BuildRelease
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Add-IfExists {
    param(
        [string]$Path,
        [string]$Destination
    )

    if (Test-Path $Path) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        Copy-Item $Path $Destination -Recurse -Force
        return $true
    }

    return $false
}

function Save-CommandOutput {
    param(
        [string]$Path,
        [scriptblock]$Command
    )

    try {
        & $Command *>&1 | Tee-Object -FilePath $Path
    }
    catch {
        $_ | Out-File -FilePath $Path -Append -Encoding UTF8
        throw
    }
}

$ProjectRoot = (Resolve-Path $ProjectRoot).Path

if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Join-Path $ProjectRoot ("Evidencias_HeimdallHash_" + (Get-Date -Format "yyyyMMdd_HHmmss"))
}

New-Item -ItemType Directory -Path $EvidenceRoot -Force | Out-Null

$Dirs = @{
    Logs = Join-Path $EvidenceRoot "01_Logs_Ejecucion"
    Config = Join-Path $EvidenceRoot "02_Configuracion"
    Release = Join-Path $EvidenceRoot "03_Release"
    Tests = Join-Path $EvidenceRoot "04_Pruebas"
    Service = Join-Path $EvidenceRoot "05_Servicio_Windows"
    Docs = Join-Path $EvidenceRoot "06_Documentacion"
}

foreach ($d in $Dirs.Values) {
    New-Item -ItemType Directory -Path $d -Force | Out-Null
}

$Manifest = [ordered]@{
    FechaGeneracion = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    ProjectRoot = $ProjectRoot
    ReleaseVersion = $ReleaseVersion
    Equipo = $env:COMPUTERNAME
    Usuario = $env:USERNAME
    Evidencias = @()
}

Write-Step "Información de entorno"
Save-CommandOutput -Path (Join-Path $Dirs.Logs "00_entorno_dotnet_info.txt") -Command {
    dotnet --info
}

Save-CommandOutput -Path (Join-Path $Dirs.Logs "00_entorno_powershell.txt") -Command {
    $PSVersionTable
}

Write-Step "Compilación Debug"
Save-CommandOutput -Path (Join-Path $Dirs.Logs "01_dotnet_build_debug.txt") -Command {
    dotnet build (Join-Path $ProjectRoot "Heimdallhash.sln") -c Debug
}

Write-Step "Compilación Release"
Save-CommandOutput -Path (Join-Path $Dirs.Logs "02_dotnet_build_release.txt") -Command {
    dotnet build (Join-Path $ProjectRoot "Heimdallhash.sln") -c Release
}

if ($BuildRelease) {
    Write-Step "Generación de release"
    $buildReleaseScript = Join-Path $ProjectRoot "ReleaseTools\Build-Release.ps1"
    if (!(Test-Path $buildReleaseScript)) {
        throw "No se encuentra $buildReleaseScript"
    }

    Save-CommandOutput -Path (Join-Path $Dirs.Logs "03_build_release.txt") -Command {
        & $buildReleaseScript -Version $ReleaseVersion
    }
}
else {
    Write-Host "Generación de release omitida. Usa -BuildRelease para generarla." -ForegroundColor DarkYellow
}

Write-Step "Copia de configuración"
Add-IfExists -Path (Join-Path $ProjectRoot "Heimdallhash\appsettings.json") -Destination $Dirs.Config | Out-Null
Add-IfExists -Path (Join-Path $ProjectRoot "Heimdallhash\appsettings.backup.json") -Destination $Dirs.Config | Out-Null

Write-Step "Copia de release"
$ReleaseZip = Join-Path $ProjectRoot "release\HeimdallHash_Release_v$ReleaseVersion.zip"
$ReleaseFolder = Join-Path $ProjectRoot "release\HeimdallHash_Release_v$ReleaseVersion"

Add-IfExists -Path $ReleaseZip -Destination $Dirs.Release | Out-Null
Add-IfExists -Path $ReleaseFolder -Destination $Dirs.Release | Out-Null

if ($RunFunctionalTests) {
    Write-Step "Ejecución pruebas funcionales T01-T11"
    $runAll = Join-Path $ProjectRoot "TestScripts\Run-AllTests.ps1"
    if (!(Test-Path $runAll)) {
        throw "No se encuentra $runAll"
    }

    Save-CommandOutput -Path (Join-Path $Dirs.Logs "04_run_all_tests_T01_T11.txt") -Command {
        & $runAll
    }
}
else {
    Write-Host "Pruebas T01-T11 omitidas. Usa -RunFunctionalTests para ejecutarlas." -ForegroundColor DarkYellow
}

Write-Step "Copia de resultados de pruebas"
$PossibleTestFiles = @(
    (Join-Path $ProjectRoot "test_summary.csv"),
    (Join-Path $ProjectRoot "TestScripts\test_summary.csv"),
    (Join-Path $ProjectRoot "TestScripts\output\test_summary.csv"),
    (Join-Path $ProjectRoot "TestScripts\results\test_summary.csv"),
    (Join-Path $ProjectRoot "heimdallhash_stdout.log"),
    (Join-Path $ProjectRoot "TestScripts\heimdallhash_stdout.log"),
    (Join-Path $ProjectRoot "TestScripts\output\heimdallhash_stdout.log"),
    (Join-Path $ProjectRoot "TestScripts\results\heimdallhash_stdout.log")
)

foreach ($file in $PossibleTestFiles) {
    Add-IfExists -Path $file -Destination $Dirs.Tests | Out-Null
}

# Buscar CSV/logs relevantes generados en árbol de proyecto, sin arrastrar bin/obj.
Get-ChildItem $ProjectRoot -Recurse -Force -File -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch "\\bin\\" -and
        $_.FullName -notmatch "\\obj\\" -and
        $_.Name -match "test_summary|heimdallhash_stdout|LR_productos|LR_notificaciones|log_errores"
    } |
    ForEach-Object {
        Copy-Item $_.FullName $Dirs.Tests -Force -ErrorAction SilentlyContinue
    }

if ($RunT12) {
    Write-Step "Ejecución prueba T12 de servicio Windows"
    $T12Candidates = @(
        (Join-Path $ProjectRoot "release\HeimdallHash_Release_v$ReleaseVersion\Scripts\Test-T12-ServiceDeployment.ps1"),
        (Join-Path $ProjectRoot "release\T12_Run\Scripts\Test-T12-ServiceDeployment.ps1"),
        (Join-Path $ProjectRoot "ReleaseTools\Scripts\Test-T12-ServiceDeployment.ps1")
    )

    $T12 = $T12Candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (!$T12) {
        throw "No se encuentra Test-T12-ServiceDeployment.ps1. Genera o descomprime primero la release."
    }

    Save-CommandOutput -Path (Join-Path $Dirs.Logs "05_T12_ServiceDeployment.txt") -Command {
        & $T12
    }
}
else {
    Write-Host "T12 omitida. Usa -RunT12 para ejecutarla." -ForegroundColor DarkYellow
}

Write-Step "Estado de servicios HeimdallHash"
Save-CommandOutput -Path (Join-Path $Dirs.Service "servicios_heimdallhash.txt") -Command {
    Get-Service | Where-Object { $_.Name -like "*Heimdall*" -or $_.DisplayName -like "*Heimdall*" } | Format-List *
}

Write-Step "Inventario de release"
Save-CommandOutput -Path (Join-Path $Dirs.Release "inventario_release.txt") -Command {
    if (Test-Path (Join-Path $ProjectRoot "release")) {
        Get-ChildItem (Join-Path $ProjectRoot "release") -Recurse -Force | Select-Object FullName, Length, LastWriteTime
    }
    else {
        "No existe carpeta release."
    }
}

Write-Step "Documentación"
Add-IfExists -Path (Join-Path $ProjectRoot "ReleaseTools\Documentacion") -Destination $Dirs.Docs | Out-Null
Add-IfExists -Path (Join-Path $ProjectRoot "README_RELEASE_WORKSPACE.md") -Destination $Dirs.Docs | Out-Null

# Resumen textual
$summaryPath = Join-Path $EvidenceRoot "RESUMEN_EVIDENCIAS.md"

@"
# Evidencias HeimdallHash

Fecha de generación: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Evidencias incluidas

| Carpeta | Contenido |
|---|---|
| 01_Logs_Ejecucion | Salidas de dotnet, build, release, T01-T11 y T12 si se ejecutan |
| 02_Configuracion | appsettings.json y configuración usada |
| 03_Release | ZIP y/o carpeta release generada |
| 04_Pruebas | CSV/logs de pruebas funcionales |
| 05_Servicio_Windows | Estado de servicios HeimdallHash |
| 06_Documentacion | Guías y matriz de pruebas |

## Comandos recomendados para evidencia completa

```powershell
.\\Collect-HeimdallHashEvidence.ps1 -BuildRelease -RunFunctionalTests -RunT12
```

## Resultado esperado

- Compilación Debug: OK
- Compilación Release: OK
- T01-T11: PASS
- T12: PASS
- Release generada: OK
"@ | Set-Content -Path $summaryPath -Encoding UTF8

$Manifest.Evidencias += Get-ChildItem $EvidenceRoot -Recurse -File | ForEach-Object {
    [ordered]@{
        RelativePath = $_.FullName.Replace($EvidenceRoot, "").TrimStart("\")
        SizeBytes = $_.Length
        LastWriteTime = $_.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
    }
}

$Manifest | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $EvidenceRoot "manifest.json") -Encoding UTF8

$ZipPath = "$EvidenceRoot.zip"
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Compress-Archive -Path (Join-Path $EvidenceRoot "*") -DestinationPath $ZipPath -Force

Write-Host ""
Write-Host "Evidencias generadas correctamente:" -ForegroundColor Green
Write-Host $EvidenceRoot -ForegroundColor Green
Write-Host $ZipPath -ForegroundColor Green
