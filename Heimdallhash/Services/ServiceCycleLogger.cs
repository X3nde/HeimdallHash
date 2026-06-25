using Heimdallhash.Config;
using Heimdallhash.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Registra eventos técnicos del servicio en LogsAplicacion\Servicio.
     *
     * Este log no sustituye a los libros de registro funcionales.
     * Su objetivo es dejar trazabilidad técnica de:
     *
     * - arranque del servicio
     * - validación inicial
     * - inicio de ciclo
     * - fin de ciclo
     * - errores técnicos de ciclo
     * - detención del servicio
     */
    public class ServiceCycleLogger
    {
        private readonly AppSettings _settings;
        private readonly ILogger<ServiceCycleLogger> _logger;

        public ServiceCycleLogger(
            IOptions<AppSettings> settings,
            ILogger<ServiceCycleLogger> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /*
         * Registra un evento técnico del servicio.
         */
        public async Task RegistrarEventoAsync(
            string cycleId,
            string eventType,
            string status,
            string detail,
            CancellationToken cancellationToken = default)
        {
            string rutaLog = ObtenerRutaLogServicioDiario();
            string separador = ObtenerSeparador();

            string cabecera = string.Join(separador, new[]
            {
                "CycleId",
                "TimestampLocal",
                "TimestampUtc",
                "EventType",
                "Status",
                "Detail"
            });

            string linea = string.Join(separador, new[]
            {
                EscaparCsv(cycleId),
                EscaparCsv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                EscaparCsv(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                EscaparCsv(eventType),
                EscaparCsv(status),
                EscaparCsv(detail)
            });

            try
            {
                await RetryHelper.EjecutarConReintentosAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string? directorio = Path.GetDirectoryName(rutaLog);

                    if (!string.IsNullOrWhiteSpace(directorio))
                    {
                        Directory.CreateDirectory(directorio);
                    }

                    bool existe = File.Exists(rutaLog);

                    await using var stream = new FileStream(
                        rutaLog,
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
                _settings.ApplicationLogs.MaxWriteAttempts,
                _settings.ApplicationLogs.RetryDelayMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo registrar el evento técnico de servicio {EventType} en {RutaLog}.",
                    eventType,
                    rutaLog);
            }
        }

        /*
         * Genera un identificador de ciclo técnico.
         */
        public string CrearCycleId()
        {
            return DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..8];
        }

        /*
         * Devuelve la ruta del log diario técnico de servicio.
         */
        private string ObtenerRutaLogServicioDiario()
        {
            string directorio = ObtenerDirectorioLogsServicioDiarios();

            string patron = ObtenerValorODefecto(
                _settings.ApplicationLogs.ServiceDailyPattern,
                "yyyyMMdd_log_servicio.csv");

            string nombreLog = patron.Replace(
                "yyyyMMdd",
                DateTime.Now.ToString("yyyyMMdd"));

            return Path.Combine(
                directorio,
                SanitizarNombreFichero(nombreLog));
        }

        /*
         * Obtiene el directorio:
         *
         * LogsAplicacion\Servicio\Diarios
         */
        private string ObtenerDirectorioLogsServicioDiarios()
        {
            string raizLogs = ObtenerRaizLogsAplicacion();

            string servicio = ObtenerValorODefecto(
                _settings.ApplicationLogs.ServiceDirectoryName,
                "Servicio");

            string diarios = ObtenerValorODefecto(
                _settings.ApplicationLogs.DailyDirectoryName,
                "Diarios");

            return Path.Combine(
                raizLogs,
                SanitizarNombreDirectorio(servicio),
                SanitizarNombreDirectorio(diarios));
        }

        /*
         * Obtiene la raíz de LogsAplicacion.
         */
        private string ObtenerRaizLogsAplicacion()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ApplicationLogs.RootDirectory))
            {
                return _settings.ApplicationLogs.RootDirectory;
            }

            _logger.LogWarning(
                "ApplicationLogs.RootDirectory no está configurado. Se usará la ruta por defecto dentro del directorio base de la aplicación.");

            return Path.Combine(AppContext.BaseDirectory, "LogsAplicacion");
        }

        /*
         * Obtiene el separador CSV configurado.
         */
        private string ObtenerSeparador()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ApplicationLogs.Delimiter))
            {
                return _settings.ApplicationLogs.Delimiter;
            }

            return ";";
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
         * Sanitiza nombres de directorio.
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

        /*
         * Sanitiza nombres de fichero.
         */
        private static string SanitizarNombreFichero(string? value)
        {
            value ??= string.Empty;
            value = value.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                return "yyyyMMdd_log_servicio.csv"
                    .Replace("yyyyMMdd", DateTime.Now.ToString("yyyyMMdd"));
            }

            foreach (char caracterInvalido in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(caracterInvalido, '_');
            }

            return value;
        }

        /*
         * Escapa un campo CSV.
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
