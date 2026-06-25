# HeimdallHash - Paquete de pruebas automatizadas

Este paquete contiene una batería de pruebas automatizadas para validar el comportamiento funcional de HeimdallHash de forma ordenada, repetible y verificable.

La batería prepara un entorno de laboratorio, genera paquetes de prueba, arranca el servicio, ejecuta los casos definidos y deja un resumen CSV con el resultado de cada prueba.

## Estructura del paquete

```text
TestScripts/
 ├── README.md
 ├── GUIA_EJECUCION.md
 ├── PLAN_PRUEBAS.md
 ├── MATRIZ_COBERTURA.md
 ├── Run-AllTests.ps1
 ├── TestConfig.ps1
 ├── scripts/
 │   ├── 00_Preparar-Entorno.ps1
 │   ├── 01_Compilar-Servicio.ps1
 │   ├── 02_Generar-Paquetes-Prueba.ps1
 │   ├── 03_Iniciar-Servicio.ps1
 │   ├── T01_Producto-Valido-Aceptado.ps1
 │   ├── T02_Hash-Incorrecto-Cuarentena.ps1
 │   ├── T03_Sin-XML-SinResolver.ps1
 │   ├── T04_Dos-XML-SinResolver.ps1
 │   ├── T05_PendienteEntrega-Reintento.ps1
 │   ├── T06_Fichero-Declarado-Ausente.ps1
 │   ├── T07_Fichero-Extra-No-Declarado.ps1
 │   ├── T08_Tamano-Incorrecto.ps1
 │   ├── T09_Algoritmo-Hash-Invalido.ps1
 │   ├── T10_XML-Mal-Formado.ps1
 │   ├── T11_Centro-No-Configurado.ps1
 │   ├── 90_Detener-Servicio.ps1
 │   └── 91_Restaurar-Configuracion.ps1
 └── tools/
     └── Scan-HeimdallHashCodeHygiene.ps1
```

## Requisitos

- Windows.
- PowerShell 5.1 o superior.
- .NET SDK instalado.
- Código fuente de HeimdallHash disponible en una carpeta local.

## Instalación

Copiar la carpeta `TestScripts` dentro de la raíz del proyecto, al mismo nivel que la carpeta `Heimdallhash`.

Ejemplo:

```text
HeimdallHash/
 ├── Heimdallhash/
 └── TestScripts/
```

## Ejecución completa

Desde la raíz del proyecto:

```powershell
.\TestScripts\Run-AllTests.ps1
```

La ejecución completa realiza estas acciones:

1. Prepara el entorno de laboratorio.
2. Crea copia de seguridad de `Heimdallhash\appsettings.json`.
3. Aplica una configuración de prueba controlada.
4. Compila el servicio.
5. Genera paquetes ZIP de prueba.
6. Arranca HeimdallHash en segundo plano.
7. Ejecuta los casos `T01` a `T11`.
8. Detiene el servicio.
9. Restaura el `appsettings.json` original.

## Rutas utilizadas

La batería usa estas rutas de laboratorio:

```text
C:\HeimdallHashLab
C:\HeimdallHashData
```

Antes de ejecutar la batería completa, esas rutas se eliminan y se crean de nuevo.

## Resultados

Al finalizar, se genera:

```text
TestScripts\results\test_summary.csv
```

También se guardan los logs capturados del servicio:

```text
TestScripts\results\heimdallhash_stdout.log
TestScripts\results\heimdallhash_stderr.log
```

## Documentación incluida

- `GUIA_EJECUCION.md`: instrucciones de ejecución paso a paso.
- `PLAN_PRUEBAS.md`: detalle de cada caso de prueba.
- `MATRIZ_COBERTURA.md`: tabla de cobertura funcional y validaciones.
