using System.Security.Cryptography;
using Heimdallhash.Config;
using Heimdallhash.Models;
using Heimdallhash.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    public class FileProcessor
    {
        private readonly AppSettings _settings;
        private readonly ILogger<FileProcessor> _logger;
        private readonly ErrorLogger _errorLogger;
        private readonly PackageProcessor _packageProcessor;
        private readonly PackageAuditLogger _packageAuditLogger;
        private readonly PendingDeliveryService _pendingDeliveryService;
        private readonly MailNotifier _mailNotifier;

        public FileProcessor(
            IOptions<AppSettings> settings,
            ILogger<FileProcessor> logger,
            ErrorLogger errorLogger,
            PackageProcessor packageProcessor,
            PackageAuditLogger packageAuditLogger,
            PendingDeliveryService pendingDeliveryService,
            MailNotifier mailNotifier)
        {
            _settings = settings.Value;
            _logger = logger;
            _errorLogger = errorLogger;
            _packageProcessor = packageProcessor;
            _packageAuditLogger = packageAuditLogger;
            _pendingDeliveryService = pendingDeliveryService;
            _mailNotifier = mailNotifier;
        }

        public async Task ProcesarDirectorioAsync(CancellationToken cancellationToken)
        {
            _errorLogger.IniciarNuevoCiclo();

            try
            {
                var rutasActivas = _settings.WatchRoutes
                    .Where(ruta => ruta.Enabled)
                    .ToList();

                if (rutasActivas.Count == 0)
                {
                    _logger.LogWarning("No hay rutas activas configuradas en WatchRoutes.");
                    return;
                }

                var nivelConcurrencia = _settings.ConcurrencyLevel > 0
                    ? _settings.ConcurrencyLevel
                    : 1;

                using var semaphore = new SemaphoreSlim(nivelConcurrencia);

                var tareas = new List<Task>();

                foreach (var ruta in rutasActivas)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    tareas.AddRange(await CrearTareasDeProcesamientoAsync(
                        ruta,
                        semaphore,
                        cancellationToken));
                }

                await Task.WhenAll(tareas);
            }
            finally
            {
                _errorLogger.FinalizarCicloYGuardar();
            }
        }

        private async Task<List<Task>> CrearTareasDeProcesamientoAsync(
            WatchRouteConfig ruta,
            SemaphoreSlim semaphore,
            CancellationToken cancellationToken)
        {
            var tareas = new List<Task>();

            if (string.IsNullOrWhiteSpace(ruta.InputDirectory))
            {
                _logger.LogWarning(
                    "La ruta monitorizada {Ruta} no tiene InputDirectory configurado.",
                    ruta.Name);

                return tareas;
            }

            if (!Directory.Exists(ruta.InputDirectory))
            {
                _logger.LogWarning(
                    "El directorio de entrada no existe para la ruta {Ruta}: {InputDirectory}",
                    ruta.Name,
                    ruta.InputDirectory);

                return tareas;
            }

            var archivos = Directory.GetFiles(ruta.InputDirectory);

            foreach (var archivo in archivos)
            {
                tareas.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);

                    try
                    {
                        await ProcesarArchivoAsync(
                            ruta,
                            archivo,
                            cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation(
                            "Procesamiento cancelado para el archivo: {Archivo}",
                            archivo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error al procesar archivo {Archivo}",
                            archivo);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.CompletedTask;
            return tareas;
        }

        private async Task ProcesarArchivoAsync(
            WatchRouteConfig ruta,
            string rutaArchivo,
            CancellationToken cancellationToken)
        {
            string nombreArchivo = Path.GetFileName(rutaArchivo);
            string modoValidacion = NormalizarModoValidacion(ruta.ValidationMode);

            if (!EsArchivoEstable(rutaArchivo))
            {
                _logger.LogInformation(
                    "Archivo omitido por ser muy reciente: {Archivo}",
                    nombreArchivo);

                return;
            }

            switch (modoValidacion)
            {
                case "FILENAMEHASH":
                    await ProcesarArchivoConHashEnNombreAsync(
                        ruta,
                        rutaArchivo,
                        cancellationToken);
                    break;

                case "DELIVERYNOTE":
                    await ProcesarPaqueteConDeliveryNoteAsync(
                        ruta,
                        rutaArchivo,
                        cancellationToken);
                    break;

                case "AUTO":
                    if (EsPaqueteSoportado(rutaArchivo))
                    {
                        await ProcesarPaqueteConDeliveryNoteAsync(
                            ruta,
                            rutaArchivo,
                            cancellationToken);
                    }
                    else
                    {
                        await ProcesarArchivoConHashEnNombreAsync(
                            ruta,
                            rutaArchivo,
                            cancellationToken);
                    }

                    break;

                default:
                    _logger.LogWarning(
                        "Modo de validación no soportado en la ruta {Ruta}: {Modo}",
                        ruta.Name,
                        ruta.ValidationMode);

                    await MoverACuarentenaAsync(
                        ruta,
                        rutaArchivo,
                        "VALIDATION_MODE_NOT_SUPPORTED",
                        cancellationToken);

                    break;
            }
        }

        /*
         * Procesa un producto comprimido mediante Delivery Note.
         *
         * Si el producto es válido, se intenta mover a la ruta destino resuelta
         * por CenterId + Flow y se registra en el libro diario de aceptados.
         *
         * Si el producto es válido pero no se puede entregar, se mueve a
         * PendienteEntrega para reintento posterior.
         *
         * Si el producto es inválido, se mueve a cuarentena y se registra en el
         * libro diario de cuarentena.
         */
        private async Task ProcesarPaqueteConDeliveryNoteAsync(
            WatchRouteConfig ruta,
            string rutaArchivo,
            CancellationToken cancellationToken)
        {
            string nombreArchivo = Path.GetFileName(rutaArchivo);

            _logger.LogInformation(
                "Procesando producto con Delivery Note: {Archivo}",
                nombreArchivo);

            PackageValidationResult resultado = await _packageProcessor.ProcessAsync(
                rutaArchivo,
                ruta,
                cancellationToken);

            if (resultado.IsValid)
            {
                string? rutaDestinoFinal = await MoverPaqueteValidoADestinoAsync(
                    resultado,
                    rutaArchivo,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(rutaDestinoFinal))
                {
                    await _packageAuditLogger.RegistrarPaqueteAceptadoAsync(
                        resultado,
                        rutaDestinoFinal,
                        cancellationToken);

                    return;
                }

                await _pendingDeliveryService.RegistrarPendienteAsync(
                    resultado,
                    rutaArchivo,
                    "DESTINATION_MOVE_ERROR",
                    cancellationToken);

                return;
            }

            string motivoPrincipal = resultado.GetMainErrorCode();

            if (string.IsNullOrWhiteSpace(motivoPrincipal))
            {
                motivoPrincipal = "PACKAGE_VALIDATION_FAILED";
            }

            string detalle = resultado.GetErrorSummary();

            _logger.LogWarning(
                "Producto inválido: {Archivo}. Motivo: {Motivo}. Detalle: {Detalle}",
                nombreArchivo,
                motivoPrincipal,
                detalle);

            string? rutaCuarentena = await MoverACuarentenaAsync(
                ruta,
                rutaArchivo,
                motivoPrincipal,
                cancellationToken,
                detalle,
                resultado);

            if (!string.IsNullOrWhiteSpace(rutaCuarentena))
            {
                await _packageAuditLogger.RegistrarPaqueteCuarentenaAsync(
                    resultado,
                    rutaCuarentena,
                    cancellationToken);

                await _mailNotifier.EnviarNotificacionCuarentenaAsync(
                    resultado.EventId,
                    resultado.PackageName,
                    resultado.Flow,
                    resultado.DestinationCenterId,
                    motivoPrincipal,
                    detalle,
                    DateTime.Now,
                    cancellationToken);
            }
        }

        /*
         * Procesa un archivo usando el mecanismo temporal de hash incluido en el nombre.
         */
        private async Task ProcesarArchivoConHashEnNombreAsync(
            WatchRouteConfig ruta,
            string rutaArchivo,
            CancellationToken cancellationToken)
        {
            string nombreArchivo = Path.GetFileName(rutaArchivo);
            string extension = Path.GetExtension(rutaArchivo);

            var nombreSinExtension = Path.GetFileNameWithoutExtension(nombreArchivo);
            var partes = nombreSinExtension.Split('_');

            if (partes.Length < 2)
            {
                _logger.LogWarning(
                    "Nombre de archivo inválido. No contiene separador '_' ni hash: {Archivo}",
                    nombreArchivo);

                await MoverACuarentenaAsync(
                    ruta,
                    rutaArchivo,
                    "FILE_NAME_HASH_NOT_FOUND",
                    cancellationToken);

                return;
            }

            string nombreBase = string.Join("_", partes[..^1]);
            string hashEsperado = partes[^1];

            string hashReal;

            try
            {
                hashReal = await CalcularHashAsync(
                    rutaArchivo,
                    _settings.Hash.DefaultAlgorithm,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo calcular el hash del archivo: {Archivo}",
                    nombreArchivo);

                await MoverACuarentenaAsync(
                    ruta,
                    rutaArchivo,
                    "HASH_CALCULATION_ERROR",
                    cancellationToken);

                return;
            }

            if (hashEsperado.Equals(hashReal, StringComparison.OrdinalIgnoreCase))
            {
                await MoverArchivoValidoAsync(
                    ruta,
                    rutaArchivo,
                    $"{nombreBase}{extension}",
                    cancellationToken);

                _logger.LogInformation(
                    "Archivo válido procesado correctamente: {Archivo}",
                    nombreArchivo);
            }
            else
            {
                _logger.LogWarning(
                    "Hash incorrecto: {Archivo}. Esperado: {Esperado}. Real: {Real}",
                    nombreArchivo,
                    hashEsperado,
                    hashReal);

                await MoverACuarentenaAsync(
                    ruta,
                    rutaArchivo,
                    "HASH_MISMATCH",
                    cancellationToken);
            }
        }

        /*
         * Mueve un archivo validado por FileNameHash al OutputDirectory de la ruta.
         */
        private async Task MoverArchivoValidoAsync(
            WatchRouteConfig ruta,
            string rutaArchivo,
            string nombreFinal,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(ruta.OutputDirectory))
            {
                _logger.LogWarning(
                    "La ruta {Ruta} no tiene OutputDirectory configurado para validación FileNameHash.",
                    ruta.Name);

                await MoverACuarentenaAsync(
                    ruta,
                    rutaArchivo,
                    "OUTPUT_DIRECTORY_NOT_CONFIGURED",
                    cancellationToken);

                return;
            }

            if (!Directory.Exists(ruta.OutputDirectory))
            {
                if (_settings.DirectoryManagement.AllowServiceToCreateFlowDirectories)
                {
                    Directory.CreateDirectory(ruta.OutputDirectory);
                }
                else
                {
                    _logger.LogWarning(
                        "El OutputDirectory de la ruta {Ruta} no existe y el servicio no está autorizado a crearlo: {OutputDirectory}",
                        ruta.Name,
                        ruta.OutputDirectory);

                    await MoverACuarentenaAsync(
                        ruta,
                        rutaArchivo,
                        "OUTPUT_DIRECTORY_NOT_AVAILABLE",
                        cancellationToken);

                    return;
                }
            }

            string destino = Path.Combine(ruta.OutputDirectory, nombreFinal);

            await RetryHelper.EjecutarConReintentosAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                File.Move(rutaArchivo, destino, overwrite: true);

                await Task.CompletedTask;

                return true;
            },
            _settings.RetryPolicy.MaxAttempts,
            _settings.RetryPolicy.DelayMilliseconds);

            _logger.LogInformation(
                "Archivo movido a destino: {Origen} -> {Destino}",
                rutaArchivo,
                destino);
        }

        /*
         * Mueve un producto válido a la ruta destino resuelta desde CenterId + Flow.
         *
         * Si el destino no está disponible, no se mueve a cuarentena.
         * Devuelve null para que el flujo superior lo registre como pendiente de entrega.
         */
        private async Task<string?> MoverPaqueteValidoADestinoAsync(
            PackageValidationResult resultado,
            string rutaArchivo,
            CancellationToken cancellationToken)
        {
            string nombreArchivo = Path.GetFileName(rutaArchivo);

            if (string.IsNullOrWhiteSpace(resultado.DestinationPath))
            {
                _logger.LogError(
                    "No se puede mover el producto {Archivo} porque DestinationPath está vacío.",
                    nombreArchivo);

                _errorLogger.RegistrarError(
                    nombreArchivo,
                    "DESTINATION_PATH_EMPTY");

                return null;
            }

            try
            {
                if (!Directory.Exists(resultado.DestinationPath))
                {
                    if (_settings.DirectoryManagement.AllowServiceToCreateFlowDirectories)
                    {
                        Directory.CreateDirectory(resultado.DestinationPath);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "La ruta destino no existe o no es accesible y el servicio no está autorizado a crearla: {DestinationPath}",
                            resultado.DestinationPath);

                        _errorLogger.RegistrarError(
                            nombreArchivo,
                            "DESTINATION_PATH_NOT_AVAILABLE");

                        return null;
                    }
                }

                string destino = Path.Combine(
                    resultado.DestinationPath,
                    resultado.PackageName);

                await RetryHelper.EjecutarConReintentosAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    File.Move(rutaArchivo, destino, overwrite: true);

                    await Task.CompletedTask;

                    return true;
                },
                _settings.RetryPolicy.MaxAttempts,
                _settings.RetryPolicy.DelayMilliseconds);

                _logger.LogInformation(
                    "Producto válido movido a destino: {Origen} -> {Destino}",
                    rutaArchivo,
                    destino);

                return destino;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo mover el producto válido {Archivo} a destino. Se registrará como pendiente de entrega.",
                    nombreArchivo);

                _errorLogger.RegistrarError(
                    nombreArchivo,
                    $"DESTINATION_MOVE_ERROR - {ex.Message}");

                return null;
            }
        }

        /*
         * Mueve un archivo o producto a cuarentena y registra el motivo.
         *
         * Devuelve la ruta final de cuarentena si el movimiento fue correcto.
         * Devuelve null si no se pudo mover.
         */
        private async Task<string?> MoverACuarentenaAsync(
            WatchRouteConfig ruta,
            string rutaArchivo,
            string motivo,
            CancellationToken cancellationToken,
            string detalle = "",
            PackageValidationResult? resultado = null)
        {
            try
            {
                string nombreArchivo = Path.GetFileName(rutaArchivo);
                string directorioCuarentena = ObtenerDirectorioCuarentena(ruta, resultado);

                Directory.CreateDirectory(directorioCuarentena);

                string destino = Path.Combine(directorioCuarentena, nombreArchivo);

                await RetryHelper.EjecutarConReintentosAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    File.Move(rutaArchivo, destino, overwrite: true);

                    await Task.CompletedTask;

                    return true;
                },
                _settings.RetryPolicy.MaxAttempts,
                _settings.RetryPolicy.DelayMilliseconds);

                _logger.LogWarning(
                    "Archivo movido a cuarentena: {Archivo}. Motivo: {Motivo}. Detalle: {Detalle}",
                    nombreArchivo,
                    motivo,
                    detalle);

                /*
                 * No se registra aquí en LogsAplicacion porque el envío a
                 * cuarentena por fallo de validación ya queda documentado en
                 * LibrosRegistro\...\Cuarentena.
                 *
                 * LogsAplicacion queda reservado para errores técnicos de la
                 * aplicación, como excepciones, fallos de escritura, problemas
                 * de acceso o incidencias de ejecución.
                 */
                return destino;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al mover archivo a cuarentena: {Archivo}",
                    rutaArchivo);

                return null;
            }
        }

        /*
         * Calcula el hash de un archivo con el algoritmo configurado.
         */
        private async Task<string> CalcularHashAsync(
            string rutaArchivo,
            string algoritmo,
            CancellationToken cancellationToken)
        {
            return await RetryHelper.EjecutarConReintentosAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var stream = File.OpenRead(rutaArchivo);

                using HashAlgorithm hashAlg = algoritmo.ToUpperInvariant() switch
                {
                    "MD5" => MD5.Create(),
                    "SHA1" => SHA1.Create(),
                    "SHA256" => SHA256.Create(),
                    "SHA384" => SHA384.Create(),
                    "SHA512" => SHA512.Create(),
                    _ => throw new NotSupportedException($"Algoritmo no soportado: {algoritmo}")
                };

                var hashBytes = await hashAlg.ComputeHashAsync(stream, cancellationToken);

                return BitConverter
                    .ToString(hashBytes)
                    .Replace("-", string.Empty)
                    .ToUpperInvariant();
            },
            _settings.RetryPolicy.MaxAttempts,
            _settings.RetryPolicy.DelayMilliseconds);
        }

        /*
         * Comprueba si el archivo tiene una antigüedad mínima para evitar
         * procesarlo mientras todavía se está copiando.
         */
        private bool EsArchivoEstable(string rutaArchivo)
        {
            var edad = DateTime.Now - File.GetLastWriteTime(rutaArchivo);

            return edad.TotalSeconds >= _settings.StabilityCheck.MinFileAgeSeconds;
        }

        /*
         * Obtiene el directorio de cuarentena siguiendo la estructura:
         *
         * Cuarentena\Centros\<CenterId>\<Flow>\Productos
         *
         * o, si no hay centro resuelto:
         *
         * Cuarentena\SinResolver\Productos
         */
        private string ObtenerDirectorioCuarentena(
            WatchRouteConfig ruta,
            PackageValidationResult? resultado = null)
        {
            string raizCuarentena = ObtenerRaizCuarentena(ruta);
            string centerId = resultado?.DestinationCenterId ?? string.Empty;

            if (string.IsNullOrWhiteSpace(centerId))
            {
                return Path.Combine(
                    raizCuarentena,
                    SanitizarNombreDirectorio(ObtenerValorODefecto(
                        _settings.Quarantine.UnresolvedDirectoryName,
                        "SinResolver")),
                    SanitizarNombreDirectorio(ObtenerValorODefecto(
                        _settings.Quarantine.ProductsDirectoryName,
                        "Productos")));
            }

            string flow = ObtenerValorODefecto(
                resultado?.Flow ?? ruta.Flow,
                "SinFlujo");

            return Path.Combine(
                raizCuarentena,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.Quarantine.CentersDirectoryName,
                    "Centros")),
                SanitizarNombreDirectorio(centerId),
                SanitizarNombreDirectorio(flow),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.Quarantine.ProductsDirectoryName,
                    "Productos")));
        }

        /*
         * Obtiene la raíz de cuarentena configurada.
         *
         * Si la ruta monitorizada define una raíz específica, se usa esa.
         * En caso contrario se usa la raíz global de cuarentena.
         */
        private string ObtenerRaizCuarentena(WatchRouteConfig ruta)
        {
            if (!string.IsNullOrWhiteSpace(ruta.QuarantineDirectory))
            {
                return ruta.QuarantineDirectory;
            }

            if (!string.IsNullOrWhiteSpace(_settings.Quarantine.RootDirectory))
            {
                return _settings.Quarantine.RootDirectory;
            }

            _logger.LogWarning(
                "Quarantine.RootDirectory no está configurado. Se usará la ruta por defecto dentro del directorio base de la aplicación.");

            return Path.Combine(AppContext.BaseDirectory, "Cuarentena");
        }

        /*
         * Comprueba si el archivo tiene una extensión de paquete soportada.
         */
        private bool EsPaqueteSoportado(string rutaArchivo)
        {
            string extension = Path.GetExtension(rutaArchivo);

            return _settings.ArchiveProcessing.SupportedExtensions
                .Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        /*
         * Normaliza el modo de validación para permitir valores como:
         * DeliveryNote, delivery_note, delivery-note, FileNameHash, etc.
         */
        private static string NormalizarModoValidacion(string modo)
        {
            if (string.IsNullOrWhiteSpace(modo))
            {
                return "DELIVERYNOTE";
            }

            return modo
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Trim()
                .ToUpperInvariant();
        }

        /*
         * Devuelve un valor configurado o un valor por defecto.
         */
        private static string ObtenerValorODefecto(string? value, string valorPorDefecto)
        {
            return string.IsNullOrWhiteSpace(value)
                ? valorPorDefecto
                : value;
        }

        /*
         * Sanitiza nombres de carpetas derivados de configuración o datos del producto.
         */
        private static string SanitizarNombreDirectorio(string? value)
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
    }
}
