# HeimdallHash - Kit de publicación Release

Este kit permite generar una entrega limpia para cliente/tutor sin incluir artefactos de desarrollo como `.vs`, `bin` u `obj`.

## Requisitos para generar la release

La generación debe realizarse en Windows con:

- PowerShell 5.1 o superior.
- .NET SDK 8 o superior.
- Permisos normales para compilar.
- Permisos de administrador solo para probar instalación del servicio.

## Generar paquete entregable

Desde la raíz del código:

```powershell
.\ReleaseTools\Build-Release.ps1 -Version 1.0.0
```

El resultado se generará en:

```text
release\HeimdallHash_Release_v1.0.0.zip
```

Ese ZIP es el paquete que se entrega al cliente/tutor.

## Contenido del paquete generado

```text
Servicio\
Configurador\
Scripts\
Plantillas\
Documentacion\
README.txt
```

## Prueba de servicio Windows

Tras generar y descomprimir la release, ejecutar PowerShell como Administrador dentro de la carpeta `Scripts`:

```powershell
.\Test-T12-ServiceDeployment.ps1
```

Resultado esperado:

```text
T12 PASS - Instalación, arranque, parada y desinstalación correctas.
```
