# Matriz de cobertura de pruebas

| ID | Prueba | Categoría | Validación principal | Resultado esperado |
|---|---|---|---|---|
| T01 | Producto válido aceptado | Flujo nominal | Delivery Note válida, hash correcto, tamaño correcto, centro configurado | Producto entregado y LR Aceptados |
| T02 | Hash incorrecto | Integridad | Hash declarado distinto del real | Cuarentena centro/flujo y LR Cuarentena |
| T03 | Sin XML | Estructura | Ausencia de Delivery Note | Cuarentena SinResolver |
| T04 | Dos XML | Estructura | Más de una Delivery Note | Cuarentena SinResolver |
| T05 | PendienteEntrega y reintento | Disponibilidad destino | Destino caído y posterior recuperación | PENDING, DELIVERED y destino final |
| T06 | Fichero declarado ausente | Delivery Note | Fichero declarado no existe en ZIP | Cuarentena centro/flujo |
| T07 | Fichero extra no declarado | Delivery Note | ZIP contiene fichero no declarado | Cuarentena centro/flujo |
| T08 | Tamaño incorrecto | Delivery Note | Tamaño declarado distinto del real | Cuarentena centro/flujo |
| T09 | Algoritmo hash inválido | Delivery Note | Algoritmo no permitido | Cuarentena centro/flujo |
| T10 | XML mal formado | Delivery Note | XML no parseable | Cuarentena SinResolver |
| T11 | CenterId no configurado | Enrutamiento | Centro no existente en CenterRoutes | Cuarentena centro detectado |

## Cobertura de trazabilidad

| Elemento | Cubierto |
|---|---|
| LR Aceptados | Sí |
| LR Cuarentena por centro/flujo | Sí |
| LR Cuarentena SinResolver | Sí |
| LR PendienteEntrega | Sí |
| LR Notificaciones | Sí |
| Logs de consola del servicio | Sí |
| Resumen CSV de pruebas | Sí |

## Notificaciones funcionales cubiertas

| Evento | Cubierto |
|---|---|
| QUARANTINE_CREATED | Sí |
| PENDING_CREATED | Sí |
| PENDING_DELIVERED | Sí |
| PENDING_STILL_FAILED | No en esta batería |
