# HeimdallHash

**HeimdallHash** es un prototipo desarrollado como Trabajo Fin de Grado para validar, clasificar y controlar productos documentales en entornos de alta seguridad.

El sistema permite procesar paquetes documentales, validar su contenido mediante una **Delivery Note XML**, comprobar la integridad de los ficheros mediante funciones hash, resolver destinos por centro y flujo, gestionar cuarentena, tratar productos no resolubles, conservar productos válidos en **PendienteEntrega** y generar evidencias funcionales del proceso.

## Aviso de licencia

Este repositorio es público exclusivamente por exigencia académica para la revisión del Trabajo Fin de Grado. **HeimdallHash no se distribuye bajo una licencia open source**.

Todos los derechos están reservados. No se autoriza el uso, copia, modificación, redistribución, sublicenciamiento, explotación comercial, integración en otros productos ni despliegue en producción sin autorización expresa del autor.

Para más información, consultar el archivo [`LICENSE.md`](LICENSE.md).

## Contexto académico

* Autor: Xende Rodríguez López
* Titulación: Grado en Ingeniería Informática
* Universidad: Universidad Alfonso X El Sabio
* Trabajo: Trabajo Fin de Grado
* Curso: 2025/2026
* Proyecto: HeimdallHash

## Descripción funcional

HeimdallHash actúa como una capa adicional de validación documental antes de aceptar productos transferidos hacia una red de destino.

El flujo general de funcionamiento es el siguiente:

1. Monitoriza una o varias rutas de entrada configuradas.
2. Detecta paquetes documentales candidatos.
3. Comprueba que el archivo se encuentra estable antes de procesarlo.
4. Extrae el contenido del paquete en una zona temporal.
5. Localiza una única Delivery Note XML.
6. Lee el centro destino y los ficheros declarados.
7. Valida presencia, tamaño, formato, algoritmo de hash y huella de cada fichero.
8. Detecta ficheros ausentes, adicionales o no conformes.
9. Resuelve el destino mediante configuración de centro y flujo.
10. Clasifica el producto como aceptado, cuarentena, SinResolver o PendienteEntrega.
11. Genera libros funcionales, logs técnicos y notificaciones configurables.

## Funcionalidades principales

* Ejecución como servicio Windows.
* Procesamiento automático de rutas configuradas.
* Validación de paquetes documentales.
* Lectura y validación de Delivery Note XML.
* Comprobación de integridad mediante hash.
* Validación de algoritmos permitidos.
* Detección de ficheros ausentes.
* Detección de ficheros adicionales no declarados.
* Detección de tamaños incoherentes.
* Resolución de destinos mediante centro y flujo.
* Gestión de productos aceptados.
* Gestión de cuarentena.
* Gestión de productos no resolubles mediante SinResolver.
* Gestión de PendienteEntrega y reintentos automáticos.
* Generación de libros funcionales en CSV.
* Generación de logs técnicos separados.
* Notificaciones configurables mediante SMTP.
* Configurador gráfico WPF.
* Scripts de generación de release.
* Scripts de instalación, arranque, parada, desinstalación y prueba del servicio Windows.
* Batería de pruebas funcionales T01-T12.

## Estructura del repositorio

La estructura del repositorio se ha mantenido alineada con la solución real y con los scripts de prueba incluidos.

```text
HEIMDALLHASH/
│
├── Heimdallhash.sln
├── Heimdallhash/
├── HeimdallhashConfigurator/
├── ReleaseTools/
├── TestScripts/
├── Collect-HeimdallHashEvidence.ps1
├── README_RELEASE_WORKSPACE.md
├── README.md
├── LICENSE.md
├── NOTICE.md
└── .gitignore
```

### `Heimdallhash/`

Contiene el código fuente del servicio principal de HeimdallHash.

Incluye el Worker Service, la lógica de procesamiento, modelos, validadores y servicios auxiliares:

* Procesamiento de productos.
* Extracción de paquetes.
* Localización de Delivery Note XML.
* Lectura de metadatos declarados.
* Validación de contenido.
* Gestión de cuarentena.
* Gestión de PendienteEntrega.
* Generación de libros funcionales.
* Generación de logs técnicos.
* Notificaciones SMTP.
* Validación de configuración.

### `HeimdallhashConfigurator/`

Contiene el código fuente del configurador gráfico desarrollado en WPF.

El configurador permite:

* Cargar y revisar la configuración.
* Editar rutas, centros, flujos y destinos.
* Configurar almacenamiento interno.
* Revisar cuarentena y PendienteEntrega.
* Configurar libros funcionales y logs.
* Configurar notificaciones SMTP.
* Proteger determinados parámetros sensibles.
* Crear directorios internos.
* Controlar el servicio Windows cuando se ejecuta con permisos suficientes.

### `ReleaseTools/`

Contiene las herramientas necesarias para generar una release entregable de HeimdallHash.

Incluye:

```text
ReleaseTools/
├── Build-Release.ps1
├── Documentacion/
└── Scripts/
```

La carpeta `Documentacion/` incluye guías de generación, instalación, operación y matriz de pruebas.

La carpeta `Scripts/` incluye scripts para:

* Instalar el servicio Windows.
* Iniciar el servicio.
* Detener el servicio.
* Desinstalar el servicio.
* Ejecutar la prueba T12 de despliegue como servicio Windows.

### `TestScripts/`

Contiene la batería de pruebas automatizadas utilizada para validar funcionalmente HeimdallHash.

Incluye:

```text
TestScripts/
├── README.md
├── GUIA_EJECUCION.md
├── PLAN_PRUEBAS.md
├── MATRIZ_COBERTURA.md
├── Run-AllTests.ps1
├── TestConfig.ps1
├── packages/
├── results/
└── scripts/
```

La carpeta `packages/` contiene paquetes ZIP sintéticos utilizados por las pruebas.

La carpeta `scripts/` contiene los scripts de validación T01-T11 y scripts auxiliares de preparación, compilación, generación de paquetes, arranque, parada y restauración.

La carpeta `results/` contiene el resumen de resultados de prueba incluido como evidencia académica.

## Requisitos

Para compilar y probar HeimdallHash se requiere:

* Windows.
* PowerShell 5.1 o superior.
* .NET SDK 8 o superior.
* Visual Studio 2022 o entorno compatible con proyectos .NET.
* Permisos de administrador únicamente para instalar o probar el servicio Windows.

## Configuración

El repositorio no incluye configuraciones reales del entorno operativo.

El archivo `Heimdallhash/appsettings.json` incluido en el repositorio corresponde a una configuración de laboratorio utilizada para validación académica. Utiliza rutas sintéticas como:

```text
C:\HeimdallHashLab
C:\HeimdallHashData
```

y no contiene credenciales reales.

Para una prueba local, pueden utilizarse estas rutas de laboratorio o adaptar la configuración a otro entorno controlado. No debe utilizarse la configuración del repositorio directamente sobre rutas productivas.

## Compilación

La solución puede abrirse desde Visual Studio mediante el archivo:

```text
Heimdallhash.sln
```

También puede compilarse desde PowerShell en la raíz del repositorio:

```powershell
dotnet restore .\Heimdallhash.sln
dotnet build .\Heimdallhash.sln -c Release
```

## Generación de release

Para generar un paquete release limpio, ejecutar desde la raíz del repositorio:

```powershell
.\ReleaseTools\Build-Release.ps1 -Version 1.0.0
```

El proceso genera un paquete entregable con estructura separada para servicio, configurador, scripts, plantillas y documentación.

La guía detallada se encuentra en:

```text
ReleaseTools\Documentacion\Guia_Generacion_Release.md
```

## Pruebas funcionales

El repositorio incluye una batería de pruebas automatizadas para validar el comportamiento funcional del prototipo.

Las pruebas T01-T11 cubren los siguientes escenarios:

| Prueba | Escenario                                              |
| ------ | ------------------------------------------------------ |
| T01    | Producto válido aceptado                               |
| T02    | Hash incorrecto enviado a cuarentena                   |
| T03    | Paquete sin XML clasificado como SinResolver           |
| T04    | Paquete con múltiples XML clasificado como SinResolver |
| T05    | PendienteEntrega y reintento automático                |
| T06    | Fichero declarado ausente                              |
| T07    | Fichero adicional no declarado                         |
| T08    | Tamaño declarado incorrecto                            |
| T09    | Algoritmo de hash no permitido                         |
| T10    | Delivery Note XML mal formada                          |
| T11    | Centro destino no configurado                          |

La prueba T12 valida la instalación, arranque, parada y desinstalación como servicio Windows.

## Ejecución de pruebas T01-T11

Desde la raíz del repositorio:

```powershell
.\TestScripts\Run-AllTests.ps1
```

La batería realiza las siguientes acciones:

1. Prepara el entorno de laboratorio.
2. Crea copia de seguridad de `Heimdallhash\appsettings.json`.
3. Aplica una configuración de prueba controlada.
4. Compila el servicio.
5. Genera paquetes ZIP sintéticos.
6. Arranca HeimdallHash en segundo plano.
7. Ejecuta los casos T01-T11.
8. Detiene el servicio.
9. Restaura el `appsettings.json` original.
10. Genera un resumen de resultados.

El resumen de resultados se genera en:

```text
TestScripts\results\test_summary.csv
```

La guía detallada de ejecución se encuentra en:

```text
TestScripts\GUIA_EJECUCION.md
```

## Prueba T12 de servicio Windows

La prueba T12 se encuentra en:

```text
ReleaseTools\Scripts\Test-T12-ServiceDeployment.ps1
```

Esta prueba debe ejecutarse desde una release generada y con PowerShell abierto como Administrador.

Permite verificar:

* Instalación del servicio Windows.
* Arranque del servicio.
* Parada del servicio.
* Desinstalación del servicio.

La guía relacionada se encuentra en:

```text
ReleaseTools\Documentacion\Guia_Instalacion.md
```

## Evidencias académicas

El repositorio incluye elementos de validación académica:

* Matriz de pruebas T01-T12.
* Plan de pruebas.
* Guía de ejecución.
* Paquetes sintéticos.
* Resultado resumido de pruebas.
* Scripts de validación.
* Herramientas de generación de release.

Las evidencias incluidas han sido generadas con datos sintéticos y rutas de laboratorio.

## Advertencia de confidencialidad

Este repositorio no contiene nombres reales de sistemas, servidores, rutas internas, credenciales, certificados, claves privadas, datos de cliente ni información clasificada.

Los nombres de centros, rutas, paquetes, configuraciones y evidencias se han preparado para un entorno de laboratorio y revisión académica.

No debe incorporarse al repositorio ningún dato procedente de entornos reales o productivos.

## Archivos excluidos

El repositorio no debe incluir artefactos generados por compilación o ejecución, tales como:

```text
.vs/
bin/
obj/
publish/
release/
Debug/
Release/
*.log
credenciales
certificados
claves privadas
configuraciones reales
datos reales
```

Estas exclusiones se gestionan mediante el archivo `.gitignore`.

## Estado del proyecto

Prototipo funcional validado académicamente mediante pruebas controladas T01-T12.

El proyecto se entrega como código fuente, documentación de apoyo, scripts de release y batería de pruebas sintéticas para revisión académica del Trabajo Fin de Grado.

## Autor

Xende Rodríguez López
Trabajo Fin de Grado
Grado en Ingeniería Informática
Universidad Alfonso X El Sabio
Curso 2025/2026
