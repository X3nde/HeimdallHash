# HeimdallHash v1.0 - Fuente limpia con herramientas de release

Este paquete contiene el código fuente limpio de HeimdallHash y herramientas para generar una release entregable.

## Limpieza aplicada

Se han eliminado artefactos de desarrollo:

```text
.vs
bin
obj
publish
release
TestResults
```

## Generar entrega

Ejecutar en Windows:

```powershell
.\ReleaseTools\Build-Release.ps1 -Version 1.0.0
```

El ZIP entregable se generará en:

```text
release\HeimdallHash_Release_v1.0.0.zip
```

## Prueba de despliegue como servicio

Después de generar la release y descomprimirla, ejecutar como Administrador:

```powershell
.\Scripts\Test-T12-ServiceDeployment.ps1
```
