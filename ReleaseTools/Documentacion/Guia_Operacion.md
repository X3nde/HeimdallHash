# HeimdallHash - Guía rápida de operación

## Flujo básico

1. Abrir el configurador.
2. Cargar `Servicio\appsettings.json`.
3. Revisar rutas de entrada.
4. Revisar centros y destinos.
5. Validar configuración.
6. Crear directorios internos si procede.
7. Guardar configuración.
8. Iniciar o reiniciar el servicio.

## Rutas funcionales

- `WatchRoutes`: rutas externas monitorizadas.
- `CenterRoutes`: destinos por centro y flujo.
- `Storage`: temporal interno.
- `Quarantine`: productos inválidos o no resolubles.
- `PendingDelivery`: productos válidos pendientes de entrega.
- `RecordBooks`: libros de registro funcionales.
- `ApplicationLogs`: logs técnicos de aplicación.

## Trazabilidad

La trazabilidad funcional se registra en `LibrosRegistro` mediante CSV diarios/mensuales:

- Aceptados.
- Cuarentena.
- PendienteEntrega.
- Notificaciones.
- Acciones manuales.
