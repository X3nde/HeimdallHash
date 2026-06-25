# HeimdallHash - Matriz de pruebas

| Test | Nombre | Objetivo |
|---|---|---|
| T01 | Producto válido aceptado | Verifica entrega correcta y LR de aceptados. |
| T02 | Hash incorrecto a cuarentena | Verifica rechazo por integridad y notificación. |
| T03 | Producto sin XML a SinResolver | Verifica clasificación de producto no resoluble. |
| T04 | Producto con dos XML a SinResolver | Verifica rechazo por ambigüedad de Delivery Note. |
| T05 | PendienteEntrega y reintento automático | Verifica retención y reintento cuando destino no disponible. |
| T06 | Fichero declarado ausente | Verifica rechazo por falta de fichero declarado. |
| T07 | Fichero extra no declarado | Verifica rechazo por contenido no declarado. |
| T08 | Tamaño incorrecto | Verifica rechazo por tamaño declarado distinto. |
| T09 | Algoritmo hash inválido | Verifica rechazo por algoritmo no permitido. |
| T10 | XML mal formado | Verifica rechazo por XML no parseable. |
| T11 | Centro no configurado | Verifica tratamiento de DestinationCenterId desconocido. |
| T12 | Servicio Windows | Verifica instalación, arranque, parada y desinstalación del servicio. |
