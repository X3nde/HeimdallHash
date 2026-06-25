# Plan de pruebas funcionales

## Objetivo

Validar que HeimdallHash procesa correctamente paquetes comprimidos con Delivery Note, aplicando las reglas de integridad, clasificación, cuarentena, entrega diferida, reintento y trazabilidad funcional.

## Configuración de referencia

La batería usa:

```text
Centro: 1234
Flujo: Download
Origen: C:\HeimdallHashLab\Origenes\EntradaProductos
Destino: C:\HeimdallHashLab\Destinos\Centro1234\Download
Datos internos: C:\HeimdallHashData
```

## T01 - Producto válido aceptado

### Objetivo

Comprobar que un paquete válido se entrega en destino y queda registrado como aceptado.

### Entrada

```text
HH_VALID_DN_1234_Download.zip
```

### Condiciones

- Contiene una Delivery Note XML válida.
- Contiene el fichero declarado.
- El hash SHA256 coincide.
- El tamaño declarado coincide.
- El centro `1234` está configurado.
- El destino está disponible.

### Resultado esperado

- El ZIP se mueve a:

```text
C:\HeimdallHashLab\Destinos\Centro1234\Download
```

- Se registra en:

```text
C:\HeimdallHashData\LibrosRegistro\Centros\1234\Download\Aceptados\Diarios
```

---

## T02 - Hash incorrecto a cuarentena

### Objetivo

Comprobar que un fichero con hash real distinto al declarado se rechaza.

### Entrada

```text
HH_INVALID_HASH_1234_Download.zip
```

### Condiciones

- Delivery Note válida.
- Fichero declarado presente.
- Hash declarado no coincide con el hash real.

### Resultado esperado

- El ZIP se mueve a:

```text
C:\HeimdallHashData\Cuarentena\Centros\1234\Download\Productos
```

- Se registra en:

```text
C:\HeimdallHashData\LibrosRegistro\Centros\1234\Download\Cuarentena\Diarios
```

- Se registra notificación funcional:

```text
QUARANTINE_CREATED
```

---

## T03 - Producto sin XML

### Objetivo

Comprobar que un paquete sin Delivery Note no se procesa como válido.

### Entrada

```text
HH_INVALID_NO_XML.zip
```

### Condiciones

- No contiene ningún fichero XML.

### Resultado esperado

- El ZIP se mueve a:

```text
C:\HeimdallHashData\Cuarentena\SinResolver\Productos
```

- Se registra en:

```text
C:\HeimdallHashData\LibrosRegistro\SinResolver\Cuarentena\Diarios
```

---

## T04 - Producto con dos XML

### Objetivo

Comprobar que un paquete con más de una Delivery Note no se procesa como válido.

### Entrada

```text
HH_INVALID_TWO_XML.zip
```

### Condiciones

- Contiene más de un XML.

### Resultado esperado

- El ZIP se mueve a:

```text
C:\HeimdallHashData\Cuarentena\SinResolver\Productos
```

- Se registra en:

```text
C:\HeimdallHashData\LibrosRegistro\SinResolver\Cuarentena\Diarios
```

---

## T05 - PendienteEntrega y reintento automático

### Objetivo

Comprobar que un producto válido queda pendiente si el destino no está disponible, y que se entrega automáticamente cuando el destino vuelve a estar disponible.

### Entrada

```text
HH_VALID_PENDING_1234_Download.zip
```

### Condiciones

- Producto válido.
- Destino temporalmente no disponible.
- Posteriormente, destino restaurado.

### Resultado esperado

Fase 1:

- El ZIP se mueve a:

```text
C:\HeimdallHashData\PendienteEntrega\Centros\1234\Download\Productos
```

- Se registra estado:

```text
PENDING
```

- Se registra notificación:

```text
PENDING_CREATED
```

Fase 2:

- El ZIP se mueve al destino final.
- Se registra estado:

```text
DELIVERED
```

- Se registra notificación:

```text
PENDING_DELIVERED
```

---

## T06 - Fichero declarado ausente

### Objetivo

Comprobar que si la Delivery Note declara un fichero que no está dentro del paquete, el producto se rechaza.

### Entrada

```text
HH_INVALID_DECLARED_FILE_MISSING.zip
```

### Resultado esperado

- Cuarentena por centro/flujo.
- Registro en LR de cuarentena.

---

## T07 - Fichero extra no declarado

### Objetivo

Comprobar que si el paquete contiene un fichero adicional no declarado en la Delivery Note, el producto se rechaza.

### Entrada

```text
HH_INVALID_EXTRA_FILE.zip
```

### Resultado esperado

- Cuarentena por centro/flujo.
- Registro en LR de cuarentena.

---

## T08 - Tamaño incorrecto

### Objetivo

Comprobar que si el tamaño declarado no coincide con el tamaño real del fichero, el producto se rechaza.

### Entrada

```text
HH_INVALID_SIZE_MISMATCH.zip
```

### Resultado esperado

- Cuarentena por centro/flujo.
- Registro en LR de cuarentena.

---

## T09 - Algoritmo hash inválido

### Objetivo

Comprobar que un algoritmo de hash no permitido provoca rechazo del producto.

### Entrada

```text
HH_INVALID_HASH_ALGORITHM.zip
```

### Resultado esperado

- Cuarentena por centro/flujo.
- Registro en LR de cuarentena.

---

## T10 - XML mal formado

### Objetivo

Comprobar que una Delivery Note XML no parseable provoca rechazo del paquete.

### Entrada

```text
HH_INVALID_MALFORMED_XML.zip
```

### Resultado esperado

- Cuarentena sin resolver.
- Registro en LR de cuarentena sin resolver.

---

## T11 - CenterId no configurado

### Objetivo

Comprobar que un centro formalmente válido pero no configurado en rutas destino provoca rechazo del producto.

### Entrada

```text
HH_INVALID_UNKNOWN_CENTER_9999.zip
```

### Condiciones

- Delivery Note con:

```text
DestinationCenterId = 9999
```

- No existe ruta `CenterRoutes` para `9999`.

### Resultado esperado

- Cuarentena por centro detectado:

```text
C:\HeimdallHashData\Cuarentena\Centros\9999\Download\Productos
```

- Registro en LR de cuarentena del centro `9999`.
