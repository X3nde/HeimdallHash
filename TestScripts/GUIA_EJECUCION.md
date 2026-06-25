# Guía de ejecución de pruebas

## 1. Preparación

Ubicar la carpeta `TestScripts` dentro de la raíz del proyecto.

Ejemplo:

```text
HeimdallHash/
 ├── Heimdallhash/
 │   ├── Heimdallhash.csproj
 │   └── appsettings.json
 └── TestScripts/
```

Abrir PowerShell en la raíz del proyecto.

## 2. Ejecución recomendada

Ejecutar:

```powershell
.\TestScripts\Run-AllTests.ps1
```

El script ejecuta la batería completa en orden.

## 3. Ejecución por fases

También se pueden ejecutar los scripts manualmente:

```powershell
.\TestScripts\scripts\00_Preparar-Entorno.ps1 -Force
.\TestScripts\scripts\01_Compilar-Servicio.ps1
.\TestScripts\scripts\02_Generar-Paquetes-Prueba.ps1
.\TestScripts\scripts\03_Iniciar-Servicio.ps1

.\TestScripts\scripts\T01_Producto-Valido-Aceptado.ps1
.\TestScripts\scripts\T02_Hash-Incorrecto-Cuarentena.ps1
.\TestScripts\scripts\T03_Sin-XML-SinResolver.ps1
.\TestScripts\scripts\T04_Dos-XML-SinResolver.ps1
.\TestScripts\scripts\T05_PendienteEntrega-Reintento.ps1
.\TestScripts\scripts\T06_Fichero-Declarado-Ausente.ps1
.\TestScripts\scripts\T07_Fichero-Extra-No-Declarado.ps1
.\TestScripts\scripts\T08_Tamano-Incorrecto.ps1
.\TestScripts\scripts\T09_Algoritmo-Hash-Invalido.ps1
.\TestScripts\scripts\T10_XML-Mal-Formado.ps1
.\TestScripts\scripts\T11_Centro-No-Configurado.ps1

.\TestScripts\scripts\90_Detener-Servicio.ps1
.\TestScripts\scripts\91_Restaurar-Configuracion.ps1
```

## 4. Restauración de configuración

Durante la preparación del entorno, el script crea una copia de seguridad de:

```text
Heimdallhash\appsettings.json
```

La ruta del backup queda registrada en:

```text
TestScripts\results\appsettings_backup_path.txt
```

Al finalizar la batería completa, `91_Restaurar-Configuracion.ps1` restaura automáticamente el fichero original.

Restauración manual:

```powershell
.\TestScripts\scripts\91_Restaurar-Configuracion.ps1
```

## 5. Resultado esperado

El resumen queda en:

```text
TestScripts\results\test_summary.csv
```

Cada línea contiene:

```text
TestId;Name;Result;TimestampLocal;Detail
```

Los valores esperados de `Result` son:

```text
PASS
FAIL
```

## 6. Consideraciones

La batería está diseñada para pruebas locales controladas. No debe ejecutarse directamente sobre rutas productivas, ya que reinicia:

```text
C:\HeimdallHashLab
C:\HeimdallHashData
```
