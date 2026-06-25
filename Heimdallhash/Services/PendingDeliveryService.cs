using Heimdallhash.Config;
using Heimdallhash.Models;
using Heimdallhash.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Gestiona productos válidos pendientes de entrega.
     *
     * Se usa cuando el producto ha superado la validación DN/XML, pero no puede
     * entregarse porque la ruta destino no está disponible o ha fallado el movimiento.
     *
     * Los productos pendientes se almacenan por centro y flujo:
     *
     * PendienteEntrega\Centros\<CenterId>\<Flow>\Productos
     *
     * La trazabilidad funcional se registra en los libros de registro:
     *
     * LibrosRegistro\Centros\<CenterId>\<Flow>\PendienteEntrega\Diarios
     */
    public class PendingDeliveryService
    {
        private readonly AppSettings _settings;
        private readonly ILogger<PendingDeliveryService> _logger;
        private readonly MailNotifier _mailNotifier;

        private int _contadorCiclosReintento = 0;

        public PendingDeliveryService(
            IOptions<AppSettings> settings,
            ILogger<PendingDeliveryService> logger,
            MailNotifier mailNotifier)
        {
            _settings = settings.Value;
            _logger = logger;
            _mailNotifier = mailNotifier;
        }

        /*
         * Registra un producto como pendiente de entrega.
         *
         * El producto se mueve desde su ruta actual a la estructura
         * PendienteEntrega correspondiente al CenterId y Flow.
         *
         * Un producto pendiente debe tener centro y flujo resueltos. Si no se
         * dispone de centro, no se registra como pendiente y debe tratarse como
         * un caso de cuarentena sin resolver desde el flujo principal.
         */
        public async Task<PendingDeliveryRecord?> RegistrarPendienteAsync(
            PackageValidationResult result,
            string productPath,
            string lastError,
            CancellationToken cancellationToken = default)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!_settings.PendingDelivery.Enabled)
            {
                _logger.LogError(
                    "No se puede registrar el producto {ProductName} como pendiente porque la gestión de pendientes está desactivada.",
                    result.PackageName);

                return null;
            }

            if (string.IsNullOrWhiteSpace(result.DestinationCenterId))
            {
                _logger.LogError(
                    "No se puede registrar el producto {ProductName} como pendiente porque no tiene centro resuelto.",
                    result.PackageName);

                return null;
            }

            if (string.IsNullOrWhiteSpace(result.Flow))
            {
                _logger.LogError(
                    "No se puede registrar el producto {ProductName} como pendiente porque no tiene flujo resuelto.",
                    result.PackageName);

                return null;
            }

            if (string.IsNullOrWhiteSpace(productPath) || !File.Exists(productPath))
            {
                _logger.LogError(
                    "No se puede registrar pendiente porque el producto no existe: {ProductPath}",
                    productPath);

                return null;
            }

            try
            {
                string productsDirectory = ObtenerDirectorioProductosPendientes(
                    result.DestinationCenterId,
                    result.Flow);

                string recordBookPath = ObtenerRutaLibroPendientesDiario(
                    result.DestinationCenterId,
                    result.Flow);

                Directory.CreateDirectory(productsDirectory);

                string pendingFileName = CrearNombrePendiente(result);
                string pendingProductPath = Path.Combine(productsDirectory, pendingFileName);

                if (File.Exists(pendingProductPath))
                {
                    _logger.LogError(
                        "No se puede registrar el producto como pendiente porque ya existe un producto con el mismo nombre en PendienteEntrega: {PendingProductPath}",
                        pendingProductPath);

                    return null;
                }

                await MoverProductoAPendientesAsync(
                    productPath,
                    pendingProductPath,
                    cancellationToken);

                var record = new PendingDeliveryRecord
                {
                    OriginalEventId = result.EventId,
                    ProductName = result.PackageName,
                    OriginalProductPath = result.PackagePath,
                    PendingProductPath = pendingProductPath,
                    Flow = result.Flow,
                    CenterId = result.DestinationCenterId,
                    DestinationPath = result.DestinationPath,
                    Status = "PENDING",
                    Attempts = 0,
                    FailedCycles = 1,
                    LastError = lastError
                };

                await EscribirRegistroPendienteAsync(
                    record,
                    recordBookPath,
                    cancellationToken);

                await _mailNotifier.EnviarNotificacionPendienteCreadoAsync(
                    record.OriginalEventId,
                    record.ProductName,
                    record.Flow,
                    record.CenterId,
                    record.DestinationPath,
                    record.PendingProductPath,
                    lastError,
                    "Producto válido registrado como pendiente de entrega porque no se pudo entregar en la ruta destino.",
                    DateTime.Now,
                    cancellationToken);

                _logger.LogWarning(
                    "Producto válido registrado como pendiente de entrega: {ProductName}. Ruta pendiente: {PendingProductPath}. Libro: {RecordBookPath}",
                    result.PackageName,
                    pendingProductPath,
                    recordBookPath);

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo registrar el producto {ProductName} como pendiente de entrega.",
                    result.PackageName);

                return null;
            }
        }

        /*
         * Reintenta entregar productos válidos que quedaron en PendienteEntrega.
         *
         * Cada ciclo revisa la estructura:
         *
         * PendienteEntrega\Centros\<CenterId>\<Flow>\Productos
         *
         * Si el destino vuelve a estar disponible, mueve el producto a destino
         * y registra una nueva línea con estado DELIVERED en el libro de pendientes.
         *
         * Si el destino sigue sin estar disponible, mantiene el producto en
         * PendienteEntrega y añade una línea de seguimiento con estado PENDING.
         */
        public async Task ReintentarPendientesAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.PendingDelivery.Enabled)
            {
                _logger.LogDebug(
                    "No se revisa PendienteEntrega porque PendingDelivery.Enabled está desactivado.");

                return;
            }

            if (!DebeEjecutarReintentoEnEsteCiclo())
            {
                _logger.LogDebug(
                    "Se omite la revisión de PendienteEntrega en este ciclo por la configuración RetryEveryCycles.");

                return;
            }

            string raizPendientes = ObtenerRaizPendienteEntrega();

            _logger.LogInformation(
                "Revisando productos pendientes de entrega en: {RaizPendientes}",
                raizPendientes);

            if (!Directory.Exists(raizPendientes))
            {
                _logger.LogInformation(
                    "No existe la raíz de PendienteEntrega. No hay productos pendientes que reintentar.");

                return;
            }

            var productosPendientes = ObtenerProductosPendientes()
                .ToList();

            if (productosPendientes.Count == 0)
            {
                _logger.LogInformation(
                    "No se han encontrado productos físicos en PendienteEntrega.");

                return;
            }

            _logger.LogInformation(
                "Se han detectado {Cantidad} producto(s) pendiente(s) de entrega para reintento.",
                productosPendientes.Count);

            foreach (var producto in productosPendientes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await ReintentarProductoPendienteAsync(
                    producto,
                    cancellationToken);
            }
        }

        /*
         * Determina si debe ejecutarse el reintento en el ciclo actual.
         *
         * El primer ciclo tras arrancar el servicio siempre revisa PendienteEntrega.
         * Así se evita que, al reiniciar el servicio después de recuperar una ruta
         * destino, el producto pendiente quede sin reintento hasta varios ciclos más.
         */
        private bool DebeEjecutarReintentoEnEsteCiclo()
        {
            _contadorCiclosReintento++;

            int retryEveryCycles = _settings.PendingDelivery.RetryEveryCycles > 0
                ? _settings.PendingDelivery.RetryEveryCycles
                : 1;

            if (_contadorCiclosReintento == 1)
            {
                return true;
            }

            return (_contadorCiclosReintento - 1) % retryEveryCycles == 0;
        }

        /*
         * Reintenta entregar un único producto pendiente.
         */
        private async Task ReintentarProductoPendienteAsync(
            PendingProductCandidate producto,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Reintentando entrega pendiente. Producto: {ProductName}. Centro: {CenterId}. Flujo: {Flow}. Ruta pendiente: {PendingProductPath}",
                producto.ProductName,
                producto.CenterId,
                producto.Flow,
                producto.PendingProductPath);

            PendingDeliveryRecord ultimoRegistro = ObtenerUltimoRegistroProductoPendiente(producto)
                ?? CrearRegistroBaseDesdeProductoPendiente(producto);

            int nuevoNumeroIntentos = ultimoRegistro.Attempts + 1;
            int nuevosCiclosFallidos = ultimoRegistro.FailedCycles;

            if (HaSuperadoMaximoDeIntentos(ultimoRegistro))
            {
                await RegistrarSeguimientoPendienteAsync(
                    ultimoRegistro,
                    producto,
                    "MAX_RETRY_ATTEMPTS_REACHED",
                    ultimoRegistro.Attempts,
                    ultimoRegistro.FailedCycles,
                    "PENDING",
                    cancellationToken);

                return;
            }

            if (HaSuperadoMaximaAntiguedad(ultimoRegistro))
            {
                await RegistrarSeguimientoPendienteAsync(
                    ultimoRegistro,
                    producto,
                    "MAX_PENDING_DAYS_REACHED",
                    nuevoNumeroIntentos,
                    ultimoRegistro.FailedCycles + 1,
                    "PENDING",
                    cancellationToken);

                return;
            }

            string? destinationPath = ResolverDestinoProductoPendiente(producto);

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                await RegistrarSeguimientoPendienteAsync(
                    ultimoRegistro,
                    producto,
                    "DESTINATION_ROUTE_NOT_CONFIGURED",
                    nuevoNumeroIntentos,
                    ultimoRegistro.FailedCycles + 1,
                    "PENDING",
                    cancellationToken);

                return;
            }

            _logger.LogInformation(
                "Destino resuelto para producto pendiente {ProductName}: {DestinationPath}",
                producto.ProductName,
                destinationPath);

            if (!Directory.Exists(destinationPath))
            {
                await RegistrarSeguimientoPendienteAsync(
                    ultimoRegistro,
                    producto,
                    "DESTINATION_PATH_NOT_AVAILABLE",
                    nuevoNumeroIntentos,
                    ultimoRegistro.FailedCycles + 1,
                    "PENDING",
                    cancellationToken);

                return;
            }

            if (_settings.PendingDelivery.RequireDestinationWriteProbe)
            {
                string? errorPruebaEscritura = await ProbarEscrituraDestinoAsync(
                    destinationPath,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(errorPruebaEscritura))
                {
                    await RegistrarSeguimientoPendienteAsync(
                        ultimoRegistro,
                        producto,
                        $"DESTINATION_WRITE_PROBE_FAILED - {errorPruebaEscritura}",
                        nuevoNumeroIntentos,
                        ultimoRegistro.FailedCycles + 1,
                        "PENDING",
                        cancellationToken);

                    return;
                }
            }

            string destinationFilePath = Path.Combine(
                destinationPath,
                producto.ProductName);

            try
            {
                await RetryHelper.EjecutarConReintentosAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    File.Move(
                        producto.PendingProductPath,
                        destinationFilePath,
                        overwrite: true);

                    await Task.CompletedTask;

                    return true;
                },
                _settings.RetryPolicy.MaxAttempts,
                _settings.RetryPolicy.DelayMilliseconds);

                var deliveredRecord = CopiarRegistroParaSeguimiento(
                    ultimoRegistro,
                    producto,
                    "DELIVERED",
                    nuevoNumeroIntentos,
                    nuevosCiclosFallidos,
                    string.Empty);

                deliveredRecord.DestinationPath = destinationPath;

                await EscribirRegistroPendienteAsync(
                    deliveredRecord,
                    ObtenerRutaLibroPendientesDiario(producto.CenterId, producto.Flow),
                    cancellationToken);

                await _mailNotifier.EnviarNotificacionPendienteEntregadoAsync(
                    deliveredRecord.OriginalEventId,
                    deliveredRecord.ProductName,
                    deliveredRecord.Flow,
                    deliveredRecord.CenterId,
                    deliveredRecord.DestinationPath,
                    destinationFilePath,
                    deliveredRecord.Attempts,
                    DateTime.Now,
                    cancellationToken);

                _logger.LogInformation(
                    "Producto pendiente entregado correctamente: {ProductName}. Destino: {DestinationFilePath}",
                    producto.ProductName,
                    destinationFilePath);
            }
            catch (Exception ex)
            {
                await RegistrarSeguimientoPendienteAsync(
                    ultimoRegistro,
                    producto,
                    $"DESTINATION_MOVE_ERROR - {ex.Message}",
                    nuevoNumeroIntentos,
                    ultimoRegistro.FailedCycles + 1,
                    "PENDING",
                    cancellationToken);

                _logger.LogWarning(
                    ex,
                    "No se pudo entregar el producto pendiente {ProductName}. Se mantendrá en PendienteEntrega.",
                    producto.ProductName);
            }
        }

        /*
         * Registra una línea adicional de seguimiento para un producto que sigue pendiente.
         */
        private async Task RegistrarSeguimientoPendienteAsync(
            PendingDeliveryRecord ultimoRegistro,
            PendingProductCandidate producto,
            string lastError,
            int attempts,
            int failedCycles,
            string status,
            CancellationToken cancellationToken)
        {
            var record = CopiarRegistroParaSeguimiento(
                ultimoRegistro,
                producto,
                status,
                attempts,
                failedCycles,
                lastError);

            await EscribirRegistroPendienteAsync(
                record,
                ObtenerRutaLibroPendientesDiario(producto.CenterId, producto.Flow),
                cancellationToken);

            _logger.LogWarning(
                "Producto pendiente no entregado: {ProductName}. Centro: {CenterId}. Flujo: {Flow}. Motivo: {LastError}",
                producto.ProductName,
                producto.CenterId,
                producto.Flow,
                lastError);

            if (DebeNotificarPendienteSigueFallando(status, failedCycles))
            {
                await _mailNotifier.EnviarNotificacionPendienteSigueFallandoAsync(
                    record.OriginalEventId,
                    record.ProductName,
                    record.Flow,
                    record.CenterId,
                    record.DestinationPath,
                    record.PendingProductPath,
                    record.FailedCycles,
                    lastError,
                    "El producto pendiente continúa sin poder entregarse tras varios ciclos de reintento.",
                    DateTime.Now,
                    cancellationToken);
            }
        }

        /*
         * Determina si debe emitirse una notificación de fallo persistente
         * para un producto que continúa pendiente.
         *
         * Se notifica solo cuando se alcanza exactamente el umbral configurado.
         * Así se evita generar una notificación repetida en cada ciclo posterior.
         */
        private bool DebeNotificarPendienteSigueFallando(
            string status,
            int failedCycles)
        {
            if (!status.Equals("PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!_settings.Notifications.Enabled ||
                !_settings.Notifications.NotifyOnPendingStillFailed)
            {
                return false;
            }

            int threshold = _settings.Notifications.NotifyPendingStillFailedAfterCycles > 0
                ? _settings.Notifications.NotifyPendingStillFailedAfterCycles
                : _settings.PendingDelivery.NotifyAfterFailedCycles;

            if (threshold <= 0)
            {
                return false;
            }

            return failedCycles == threshold;
        }

        /*
         * Crea una copia del último registro conservando identidad y trazabilidad.
         */
        private PendingDeliveryRecord CopiarRegistroParaSeguimiento(
            PendingDeliveryRecord ultimoRegistro,
            PendingProductCandidate producto,
            string status,
            int attempts,
            int failedCycles,
            string lastError)
        {
            return new PendingDeliveryRecord
            {
                PendingId = ObtenerValorODefecto(
                    ultimoRegistro.PendingId,
                    Guid.NewGuid().ToString()),

                OriginalEventId = ultimoRegistro.OriginalEventId,
                ProductName = producto.ProductName,
                OriginalProductPath = ultimoRegistro.OriginalProductPath,
                PendingProductPath = producto.PendingProductPath,
                Flow = producto.Flow,
                CenterId = producto.CenterId,
                DestinationPath = ResolverDestinoProductoPendiente(producto)
                    ?? ultimoRegistro.DestinationPath,
                Status = status,
                Attempts = attempts,
                FailedCycles = failedCycles,
                CreatedAtLocal = ultimoRegistro.CreatedAtLocal,
                CreatedAtUtc = ultimoRegistro.CreatedAtUtc,
                LastError = lastError
            };
        }

        /*
         * Comprueba si se ha superado el número máximo de intentos.
         * Si MaxRetryAttempts vale 0, no hay límite automático.
         */
        private bool HaSuperadoMaximoDeIntentos(PendingDeliveryRecord record)
        {
            return _settings.PendingDelivery.MaxRetryAttempts > 0 &&
                   record.Attempts >= _settings.PendingDelivery.MaxRetryAttempts;
        }

        /*
         * Comprueba si el producto pendiente ha superado la antigüedad máxima.
         * Si MaxPendingDays vale 0, no hay caducidad automática.
         */
        private bool HaSuperadoMaximaAntiguedad(PendingDeliveryRecord record)
        {
            if (_settings.PendingDelivery.MaxPendingDays <= 0)
            {
                return false;
            }

            if (!DateTime.TryParse(record.CreatedAtLocal, out DateTime createdAt))
            {
                return false;
            }

            return DateTime.Now.Subtract(createdAt).TotalDays >
                   _settings.PendingDelivery.MaxPendingDays;
        }

        /*
         * Comprueba capacidad de escritura en destino creando y borrando un fichero temporal.
         */
        private async Task<string?> ProbarEscrituraDestinoAsync(
            string destinationPath,
            CancellationToken cancellationToken)
        {
            string probeFile = Path.Combine(
                destinationPath,
                $".heimdallhash_probe_{Guid.NewGuid():N}.tmp");

            try
            {
                await File.WriteAllTextAsync(
                    probeFile,
                    "probe",
                    cancellationToken);

                File.Delete(probeFile);

                return null;
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(probeFile))
                    {
                        File.Delete(probeFile);
                    }
                }
                catch
                {
                    // Si no se puede limpiar el fichero de prueba, se informa del error original.
                }

                return ex.Message;
            }
        }

        /*
         * Resuelve el destino funcional del producto pendiente usando CenterId + Flow.
         */
        private string? ResolverDestinoProductoPendiente(PendingProductCandidate producto)
        {
            var rutaDestino = _settings.CenterRoutes.FirstOrDefault(ruta =>
                ruta.Enabled &&
                ruta.CenterId.Equals(producto.CenterId, StringComparison.OrdinalIgnoreCase) &&
                ruta.Flow.Equals(producto.Flow, StringComparison.OrdinalIgnoreCase));

            if (rutaDestino is null ||
                string.IsNullOrWhiteSpace(rutaDestino.DestinationPath))
            {
                return null;
            }

            return rutaDestino.DestinationPath;
        }

        /*
         * Enumera productos físicos pendientes de entrega.
         */
        private IEnumerable<PendingProductCandidate> ObtenerProductosPendientes()
        {
            string raizPendientes = ObtenerRaizPendienteEntrega();

            string centersDirectory = Path.Combine(
                raizPendientes,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.PendingDelivery.CentersDirectoryName,
                    "Centros")));

            if (!Directory.Exists(centersDirectory))
            {
                yield break;
            }

            foreach (string centerDirectory in Directory.EnumerateDirectories(centersDirectory))
            {
                string centerId = Path.GetFileName(centerDirectory);

                foreach (string flowDirectory in Directory.EnumerateDirectories(centerDirectory))
                {
                    string flow = Path.GetFileName(flowDirectory);

                    string productsDirectory = Path.Combine(
                        flowDirectory,
                        SanitizarNombreDirectorio(ObtenerValorODefecto(
                            _settings.PendingDelivery.ProductsDirectoryName,
                            "Productos")));

                    if (!Directory.Exists(productsDirectory))
                    {
                        continue;
                    }

                    foreach (string productPath in Directory.EnumerateFiles(productsDirectory))
                    {
                        yield return new PendingProductCandidate
                        {
                            CenterId = centerId,
                            Flow = flow,
                            ProductName = Path.GetFileName(productPath),
                            PendingProductPath = productPath
                        };
                    }
                }
            }
        }

        /*
         * Obtiene el último registro conocido para un producto pendiente.
         */
        private PendingDeliveryRecord? ObtenerUltimoRegistroProductoPendiente(
            PendingProductCandidate producto)
        {
            string dailyDirectory = ObtenerDirectorioDiarioPendientes(
                producto.CenterId,
                producto.Flow);

            if (!Directory.Exists(dailyDirectory))
            {
                return null;
            }

            PendingDeliveryRecord? ultimoRegistro = null;

            foreach (string file in Directory.EnumerateFiles(dailyDirectory, "*.csv"))
            {
                foreach (string line in File.ReadLines(file).Skip(1))
                {
                    PendingDeliveryRecord? record = ConvertirLineaARegistroPendiente(line);

                    if (record is null)
                    {
                        continue;
                    }

                    bool coincideRuta = record.PendingProductPath.Equals(
                        producto.PendingProductPath,
                        StringComparison.OrdinalIgnoreCase);

                    bool coincideNombre = record.ProductName.Equals(
                        producto.ProductName,
                        StringComparison.OrdinalIgnoreCase);

                    if (coincideRuta || coincideNombre)
                    {
                        ultimoRegistro = record;
                    }
                }
            }

            return ultimoRegistro;
        }

        /*
         * Crea un registro base si no existe línea previa en libros de registro.
         */
        private PendingDeliveryRecord CrearRegistroBaseDesdeProductoPendiente(
            PendingProductCandidate producto)
        {
            return new PendingDeliveryRecord
            {
                ProductName = producto.ProductName,
                PendingProductPath = producto.PendingProductPath,
                Flow = producto.Flow,
                CenterId = producto.CenterId,
                DestinationPath = ResolverDestinoProductoPendiente(producto) ?? string.Empty,
                Status = "PENDING",
                Attempts = 0,
                FailedCycles = 0,
                LastError = "PENDING_RECORD_NOT_FOUND"
            };
        }

        /*
         * Convierte una línea CSV de libro de pendientes en un registro.
         */
        private PendingDeliveryRecord? ConvertirLineaARegistroPendiente(string line)
        {
            var campos = SepararCsv(line);

            if (campos.Count < 14)
            {
                return null;
            }

            return new PendingDeliveryRecord
            {
                PendingId = campos[0],
                OriginalEventId = campos[1],
                ProductName = campos[2],
                OriginalProductPath = campos[3],
                PendingProductPath = campos[4],
                Flow = campos[5],
                CenterId = campos[6],
                DestinationPath = campos[7],
                Status = campos[8],
                Attempts = int.TryParse(campos[9], out int attempts) ? attempts : 0,
                FailedCycles = int.TryParse(campos[10], out int failedCycles) ? failedCycles : 0,
                CreatedAtLocal = campos[11],
                CreatedAtUtc = campos[12],
                LastError = campos[13]
            };
        }

        /*
         * Mueve el producto validado a la carpeta PendienteEntrega.
         *
         * No se permite sobrescritura para conservar la integridad operativa.
         */
        private async Task MoverProductoAPendientesAsync(
            string origen,
            string destino,
            CancellationToken cancellationToken)
        {
            await RetryHelper.EjecutarConReintentosAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                File.Move(origen, destino, overwrite: false);

                await Task.CompletedTask;

                return true;
            },
            _settings.RetryPolicy.MaxAttempts,
            _settings.RetryPolicy.DelayMilliseconds);
        }

        /*
         * Añade una línea al libro de registro de productos pendientes de entrega.
         */
        private async Task EscribirRegistroPendienteAsync(
            PendingDeliveryRecord record,
            string rutaRegistro,
            CancellationToken cancellationToken)
        {
            string separador = ObtenerSeparador();

            string cabecera = string.Join(separador, new[]
            {
                "PendingId",
                "OriginalEventId",
                "ProductName",
                "OriginalProductPath",
                "PendingProductPath",
                "Flow",
                "CenterId",
                "DestinationPath",
                "Status",
                "Attempts",
                "FailedCycles",
                "CreatedAtLocal",
                "CreatedAtUtc",
                "LastError"
            });

            string linea = string.Join(separador, new[]
            {
                EscaparCsv(record.PendingId),
                EscaparCsv(record.OriginalEventId),
                EscaparCsv(record.ProductName),
                EscaparCsv(record.OriginalProductPath),
                EscaparCsv(record.PendingProductPath),
                EscaparCsv(record.Flow),
                EscaparCsv(record.CenterId),
                EscaparCsv(record.DestinationPath),
                EscaparCsv(record.Status),
                record.Attempts.ToString(),
                record.FailedCycles.ToString(),
                EscaparCsv(record.CreatedAtLocal),
                EscaparCsv(record.CreatedAtUtc),
                EscaparCsv(record.LastError)
            });

            await RetryHelper.EjecutarConReintentosAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? directorioRegistro = Path.GetDirectoryName(rutaRegistro);

                if (!string.IsNullOrWhiteSpace(directorioRegistro))
                {
                    Directory.CreateDirectory(directorioRegistro);
                }

                bool existe = File.Exists(rutaRegistro);

                await using var stream = new FileStream(
                    rutaRegistro,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read);

                await using var writer = new StreamWriter(stream);

                if (!existe)
                {
                    await writer.WriteLineAsync(cabecera);
                }

                await writer.WriteLineAsync(linea);

                return true;
            },
            _settings.RecordBooks.MaxWriteAttempts,
            _settings.RecordBooks.RetryDelayMilliseconds);
        }

        /*
         * Devuelve el nombre original del producto.
         *
         * HeimdallHash no modifica el nombre del producto tratado. La
         * trazabilidad se conserva mediante PendingId y OriginalEventId.
         */
        private static string CrearNombrePendiente(PackageValidationResult result)
        {
            return result.PackageName;
        }

        /*
         * Obtiene la carpeta donde se guardará físicamente el producto pendiente:
         *
         * PendienteEntrega\Centros\<CenterId>\<Flow>\Productos
         */
        private string ObtenerDirectorioProductosPendientes(
            string centerId,
            string flow)
        {
            string raizPendientes = ObtenerRaizPendienteEntrega();

            return Path.Combine(
                raizPendientes,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.PendingDelivery.CentersDirectoryName,
                    "Centros")),
                SanitizarNombreDirectorio(centerId),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    flow,
                    "SinFlujo")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.PendingDelivery.ProductsDirectoryName,
                    "Productos")));
        }

        /*
         * Obtiene la ruta del libro diario de productos pendientes:
         *
         * LibrosRegistro\Centros\<CenterId>\<Flow>\PendienteEntrega\Diarios\yyyyMMdd_LR_productos_pendiente_entrega.csv
         */
        private string ObtenerRutaLibroPendientesDiario(
            string centerId,
            string flow)
        {
            string dailyDirectory = ObtenerDirectorioDiarioPendientes(
                centerId,
                flow);

            string nombreLibro = ObtenerValorODefecto(
                _settings.RecordBooks.PendingDeliveryDailyPattern,
                "yyyyMMdd_LR_productos_pendiente_entrega.csv")
                .Replace("yyyyMMdd", DateTime.Now.ToString("yyyyMMdd"));

            return Path.Combine(
                dailyDirectory,
                SanitizarNombreFichero(nombreLibro));
        }

        /*
         * Obtiene el directorio diario del libro de pendientes.
         */
        private string ObtenerDirectorioDiarioPendientes(
            string centerId,
            string flow)
        {
            string raizLibros = ObtenerRaizLibrosRegistro();

            return Path.Combine(
                raizLibros,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.CentersDirectoryName,
                    "Centros")),
                SanitizarNombreDirectorio(centerId),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    flow,
                    "SinFlujo")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.PendingDeliveryDirectoryName,
                    "PendienteEntrega")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.DailyDirectoryName,
                    "Diarios")));
        }

        /*
         * Obtiene la raíz de PendienteEntrega.
         */
        private string ObtenerRaizPendienteEntrega()
        {
            if (!string.IsNullOrWhiteSpace(_settings.PendingDelivery.RootDirectory))
            {
                return _settings.PendingDelivery.RootDirectory;
            }

            _logger.LogWarning(
                "PendingDelivery.RootDirectory no está configurado. Se usará la ruta por defecto dentro del directorio base de la aplicación.");

            return Path.Combine(AppContext.BaseDirectory, "PendienteEntrega");
        }

        /*
         * Obtiene la raíz de LibrosRegistro.
         */
        private string ObtenerRaizLibrosRegistro()
        {
            if (!string.IsNullOrWhiteSpace(_settings.RecordBooks.RootDirectory))
            {
                return _settings.RecordBooks.RootDirectory;
            }

            _logger.LogWarning(
                "RecordBooks.RootDirectory no está configurado. Se usará la ruta por defecto dentro del directorio base de la aplicación.");

            return Path.Combine(AppContext.BaseDirectory, "LibrosRegistro");
        }

        /*
         * Obtiene el separador CSV configurado.
         */
        private string ObtenerSeparador()
        {
            if (!string.IsNullOrWhiteSpace(_settings.RecordBooks.Delimiter))
            {
                return _settings.RecordBooks.Delimiter;
            }

            return ";";
        }

        /*
         * Devuelve un valor configurado o un valor por defecto.
         */
        private string ObtenerValorODefecto(string? value, string valorPorDefecto)
        {
            return string.IsNullOrWhiteSpace(value)
                ? valorPorDefecto
                : value;
        }

        /*
         * Sanitiza nombres de carpetas derivados de configuración o datos del producto.
         *
         * Evita caracteres inválidos para nombres de directorio.
         */
        private string SanitizarNombreDirectorio(string? value)
        {
            value ??= string.Empty;
            value = value.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                return "SinResolver";
            }

            foreach (char caracterInvalido in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(caracterInvalido, '_');
            }

            return value;
        }

        /*
         * Sanitiza nombres de fichero derivados de configuración.
         */
        private string SanitizarNombreFichero(string? value)
        {
            value ??= string.Empty;
            value = value.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                return "yyyyMMdd_LR_productos_pendiente_entrega.csv"
                    .Replace("yyyyMMdd", DateTime.Now.ToString("yyyyMMdd"));
            }

            foreach (char caracterInvalido in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(caracterInvalido, '_');
            }

            return value;
        }

        /*
         * Escapa un campo CSV para evitar que saltos de línea, comillas o separadores
         * rompan la estructura del fichero.
         */
        private string EscaparCsv(string? value)
        {
            value ??= string.Empty;

            string separador = ObtenerSeparador();

            bool necesitaComillas =
                value.Contains(separador) ||
                value.Contains('"') ||
                value.Contains('\n') ||
                value.Contains('\r');

            value = value.Replace("\"", "\"\"");

            return necesitaComillas
                ? $"\"{value}\""
                : value;
        }

        /*
         * Separa una línea CSV teniendo en cuenta comillas.
         */
        private List<string> SepararCsv(string linea)
        {
            var campos = new List<string>();
            string separador = ObtenerSeparador();

            bool dentroDeComillas = false;
            var campoActual = new System.Text.StringBuilder();

            for (int i = 0; i < linea.Length; i++)
            {
                char caracter = linea[i];

                if (caracter == '"')
                {
                    if (dentroDeComillas &&
                        i + 1 < linea.Length &&
                        linea[i + 1] == '"')
                    {
                        campoActual.Append('"');
                        i++;
                    }
                    else
                    {
                        dentroDeComillas = !dentroDeComillas;
                    }

                    continue;
                }

                if (!dentroDeComillas &&
                    i <= linea.Length - separador.Length &&
                    linea.Substring(i, separador.Length) == separador)
                {
                    campos.Add(campoActual.ToString());
                    campoActual.Clear();
                    i += separador.Length - 1;
                    continue;
                }

                campoActual.Append(caracter);
            }

            campos.Add(campoActual.ToString());

            return campos;
        }

        private sealed class PendingProductCandidate
        {
            public string ProductName { get; set; } = string.Empty;
            public string PendingProductPath { get; set; } = string.Empty;
            public string CenterId { get; set; } = string.Empty;
            public string Flow { get; set; } = string.Empty;
        }
    }
}
