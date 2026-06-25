using Heimdallhash.Config;
using Heimdallhash.Models;
using Heimdallhash.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Registra en libros CSV los productos aceptados, los productos enviados
     * a cuarentena y los productos válidos pendientes de entrega.
     *
     * Los libros de registro son evidencia funcional y auditable. No deben
     * mezclarse con los logs técnicos de aplicación.
     */
    public class PackageAuditLogger
    {
        private readonly AppSettings _settings;
        private readonly ILogger<PackageAuditLogger> _logger;

        public PackageAuditLogger(
            IOptions<AppSettings> settings,
            ILogger<PackageAuditLogger> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /*
         * Registra un producto validado correctamente y movido a destino.
         */
        public async Task RegistrarPaqueteAceptadoAsync(
            PackageValidationResult result,
            string destinationFilePath,
            CancellationToken cancellationToken = default)
        {
            string rutaLibro = ObtenerRutaLibroAceptadosDiario(result);
            string separador = ObtenerSeparador();

            string cabecera = string.Join(separador, new[]
            {
                "EventId",
                "TimestampLocal",
                "TimestampUtc",
                "ProductName",
                "ProductPath",
                "Flow",
                "CenterId",
                "DestinationPath",
                "DestinationFilePath",
                "DeliveryNoteFileName",
                "ValidationResult"
            });

            string linea = string.Join(separador, new[]
            {
                EscaparCsv(result.EventId),
                EscaparCsv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                EscaparCsv(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                EscaparCsv(result.PackageName),
                EscaparCsv(result.PackagePath),
                EscaparCsv(result.Flow),
                EscaparCsv(result.DestinationCenterId),
                EscaparCsv(result.DestinationPath),
                EscaparCsv(destinationFilePath),
                EscaparCsv(result.DeliveryNote?.DeliveryNoteFileName ?? string.Empty),
                EscaparCsv("ACCEPTED")
            });

            await EscribirLineaAsync(
                rutaLibro,
                cabecera,
                linea,
                cancellationToken);

            _logger.LogInformation(
                "Producto aceptado registrado en libro diario: {ProductName}. Libro: {RutaLibro}",
                result.PackageName,
                rutaLibro);
        }

        /*
         * Registra un producto enviado a cuarentena.
         */
        public async Task RegistrarPaqueteCuarentenaAsync(
            PackageValidationResult result,
            string quarantineFilePath,
            CancellationToken cancellationToken = default)
        {
            string rutaLibro = ObtenerRutaLibroCuarentenaDiario(result);
            string separador = ObtenerSeparador();

            string cabecera = string.Join(separador, new[]
            {
                "EventId",
                "TimestampLocal",
                "TimestampUtc",
                "ProductName",
                "ProductPath",
                "Flow",
                "CenterId",
                "QuarantineFilePath",
                "MainErrorCode",
                "ErrorSummary",
                "DeliveryNoteFileName"
            });

            string linea = string.Join(separador, new[]
            {
                EscaparCsv(result.EventId),
                EscaparCsv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                EscaparCsv(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                EscaparCsv(result.PackageName),
                EscaparCsv(result.PackagePath),
                EscaparCsv(result.Flow),
                EscaparCsv(result.DestinationCenterId),
                EscaparCsv(quarantineFilePath),
                EscaparCsv(result.GetMainErrorCode()),
                EscaparCsv(result.GetErrorSummary()),
                EscaparCsv(result.DeliveryNote?.DeliveryNoteFileName ?? string.Empty)
            });

            await EscribirLineaAsync(
                rutaLibro,
                cabecera,
                linea,
                cancellationToken);

            _logger.LogInformation(
                "Producto en cuarentena registrado en libro diario: {ProductName}. Libro: {RutaLibro}",
                result.PackageName,
                rutaLibro);
        }

        /*
         * Registra un producto válido que no ha podido entregarse en destino
         * y queda pendiente de entrega.
         */
        public async Task RegistrarProductoPendienteEntregaAsync(
            PackageValidationResult result,
            string pendingFilePath,
            string reason,
            CancellationToken cancellationToken = default)
        {
            string rutaLibro = ObtenerRutaLibroPendienteEntregaDiario(result);
            string separador = ObtenerSeparador();

            string cabecera = string.Join(separador, new[]
            {
                "EventId",
                "TimestampLocal",
                "TimestampUtc",
                "ProductName",
                "ProductPath",
                "Flow",
                "CenterId",
                "DestinationPath",
                "PendingFilePath",
                "Reason",
                "DeliveryNoteFileName"
            });

            string linea = string.Join(separador, new[]
            {
                EscaparCsv(result.EventId),
                EscaparCsv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                EscaparCsv(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                EscaparCsv(result.PackageName),
                EscaparCsv(result.PackagePath),
                EscaparCsv(result.Flow),
                EscaparCsv(result.DestinationCenterId),
                EscaparCsv(result.DestinationPath),
                EscaparCsv(pendingFilePath),
                EscaparCsv(reason),
                EscaparCsv(result.DeliveryNote?.DeliveryNoteFileName ?? string.Empty)
            });

            await EscribirLineaAsync(
                rutaLibro,
                cabecera,
                linea,
                cancellationToken);

            _logger.LogInformation(
                "Producto pendiente de entrega registrado en libro diario: {ProductName}. Libro: {RutaLibro}",
                result.PackageName,
                rutaLibro);
        }

        /*
         * Escribe una línea en un libro CSV.
         *
         * Si el fichero no existe, escribe primero la cabecera.
         * Usa reintentos porque el CSV podría estar abierto por un usuario.
         */
        private async Task EscribirLineaAsync(
            string rutaLibro,
            string cabecera,
            string linea,
            CancellationToken cancellationToken)
        {
            await RetryHelper.EjecutarConReintentosAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? directorio = Path.GetDirectoryName(rutaLibro);

                if (!string.IsNullOrWhiteSpace(directorio))
                {
                    Directory.CreateDirectory(directorio);
                }

                bool existe = File.Exists(rutaLibro);

                await using var stream = new FileStream(
                    rutaLibro,
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
         * Devuelve la ruta del libro diario de productos aceptados.
         */
        private string ObtenerRutaLibroAceptadosDiario(PackageValidationResult result)
        {
            string directorio = ObtenerDirectorioDiarioPorCentroYFlujo(
                result,
                ObtenerValorODefecto(
                    _settings.RecordBooks.AcceptedDirectoryName,
                    "Aceptados"));

            string patron = ObtenerValorODefecto(
                _settings.RecordBooks.AcceptedDailyPattern,
                "yyyyMMdd_LR_productos_aceptados.csv");

            return Path.Combine(directorio, ResolverPatronFechaDiario(patron));
        }

        /*
         * Devuelve la ruta del libro diario de productos enviados a cuarentena.
         */
        private string ObtenerRutaLibroCuarentenaDiario(PackageValidationResult result)
        {
            string nombreLibro = ResolverPatronFechaDiario(
                string.IsNullOrWhiteSpace(result.DestinationCenterId)
                    ? ObtenerValorODefecto(
                        _settings.RecordBooks.UnresolvedQuarantineDailyPattern,
                        "yyyyMMdd_LR_productos_cuarentena_sinresolver.csv")
                    : ObtenerValorODefecto(
                        _settings.RecordBooks.QuarantineDailyPattern,
                        "yyyyMMdd_LR_productos_cuarentena.csv"));

            string directorio = string.IsNullOrWhiteSpace(result.DestinationCenterId)
                ? ObtenerDirectorioDiarioSinResolver(
                    ObtenerValorODefecto(
                        _settings.RecordBooks.QuarantineDirectoryName,
                        "Cuarentena"))
                : ObtenerDirectorioDiarioPorCentroYFlujo(
                    result,
                    ObtenerValorODefecto(
                        _settings.RecordBooks.QuarantineDirectoryName,
                        "Cuarentena"));

            return Path.Combine(directorio, nombreLibro);
        }

        /*
         * Devuelve la ruta del libro diario de productos pendientes de entrega.
         */
        private string ObtenerRutaLibroPendienteEntregaDiario(PackageValidationResult result)
        {
            string directorio = ObtenerDirectorioDiarioPorCentroYFlujo(
                result,
                ObtenerValorODefecto(
                    _settings.RecordBooks.PendingDeliveryDirectoryName,
                    "PendienteEntrega"));

            string patron = ObtenerValorODefecto(
                _settings.RecordBooks.PendingDeliveryDailyPattern,
                "yyyyMMdd_LR_productos_pendiente_entrega.csv");

            return Path.Combine(directorio, ResolverPatronFechaDiario(patron));
        }

        /*
         * Obtiene el directorio diario de un libro organizado por centro y flujo:
         *
         * LibrosRegistro\Centros\<CenterId>\<Flow>\<TipoLibro>\Diarios
         */
        private string ObtenerDirectorioDiarioPorCentroYFlujo(
            PackageValidationResult result,
            string tipoLibro)
        {
            string raizLibros = ObtenerRaizLibrosRegistro();

            string centros = ObtenerValorODefecto(
                _settings.RecordBooks.CentersDirectoryName,
                "Centros");

            string diarios = ObtenerValorODefecto(
                _settings.RecordBooks.DailyDirectoryName,
                "Diarios");

            string centerId = ObtenerValorODefecto(
                result.DestinationCenterId,
                "SinCentro");

            string flow = ObtenerValorODefecto(
                result.Flow,
                "SinFlujo");

            return Path.Combine(
                raizLibros,
                SanitizarNombreDirectorio(centros),
                SanitizarNombreDirectorio(centerId),
                SanitizarNombreDirectorio(flow),
                SanitizarNombreDirectorio(tipoLibro),
                SanitizarNombreDirectorio(diarios));
        }

        /*
         * Obtiene el directorio diario de libros sin centro resuelto:
         *
         * LibrosRegistro\SinResolver\<TipoLibro>\Diarios
         */
        private string ObtenerDirectorioDiarioSinResolver(string tipoLibro)
        {
            string raizLibros = ObtenerRaizLibrosRegistro();

            string sinResolver = ObtenerValorODefecto(
                _settings.RecordBooks.UnresolvedDirectoryName,
                "SinResolver");

            string diarios = ObtenerValorODefecto(
                _settings.RecordBooks.DailyDirectoryName,
                "Diarios");

            return Path.Combine(
                raizLibros,
                SanitizarNombreDirectorio(sinResolver),
                SanitizarNombreDirectorio(tipoLibro),
                SanitizarNombreDirectorio(diarios));
        }

        /*
         * Obtiene la raíz de libros de registro.
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
         * Sustituye yyyyMMdd por la fecha actual en un patrón diario.
         */
        private string ResolverPatronFechaDiario(string patron)
        {
            return patron.Replace(
                "yyyyMMdd",
                DateTime.Now.ToString("yyyyMMdd"));
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
        private string SanitizarNombreDirectorio(string value)
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
         * Escapa un campo CSV para evitar que saltos de línea, comillas o separadores
         * rompan la estructura del libro.
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
    }
}
