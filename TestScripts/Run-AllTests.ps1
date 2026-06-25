param(
    [switch]$NoReset,
    [string]$SolutionRoot = ""
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\TestConfig.ps1"
$config = Get-HHTestConfig -SolutionRoot $SolutionRoot

try {
    if (-not $NoReset) {
        & "$PSScriptRoot\scripts\00_Preparar-Entorno.ps1" -Force -SolutionRoot $config.SolutionRoot
    }

    & "$PSScriptRoot\scripts\01_Compilar-Servicio.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\02_Generar-Paquetes-Prueba.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\03_Iniciar-Servicio.ps1" -SolutionRoot $config.SolutionRoot

    & "$PSScriptRoot\scripts\T01_Producto-Valido-Aceptado.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T02_Hash-Incorrecto-Cuarentena.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T03_Sin-XML-SinResolver.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T04_Dos-XML-SinResolver.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T05_PendienteEntrega-Reintento.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T06_Fichero-Declarado-Ausente.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T07_Fichero-Extra-No-Declarado.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T08_Tamano-Incorrecto.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T09_Algoritmo-Hash-Invalido.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T10_XML-Mal-Formado.ps1" -SolutionRoot $config.SolutionRoot
    & "$PSScriptRoot\scripts\T11_Centro-No-Configurado.ps1" -SolutionRoot $config.SolutionRoot

    Write-HHStep "Resumen de pruebas"
    Get-Content $config.SummaryCsv | ForEach-Object { Write-Host $_ }

    Write-HHOk "Batería completa finalizada."
}
finally {
    & "$PSScriptRoot\scripts\90_Detener-Servicio.ps1" -SolutionRoot $config.SolutionRoot

    if (-not $NoReset) {
        & "$PSScriptRoot\scripts\91_Restaurar-Configuracion.ps1" -SolutionRoot $config.SolutionRoot
    }
}
