using Heimdallhash.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Registra errores técnicos generales de ejecución asociados al ciclo actual.
     *
     * Los errores funcionales asociados a productos aceptados, productos en
     * cuarentena o productos pendientes de entrega se registran mediante los
     * libros de registro correspondientes.
     */
    public class ErrorLogger
    {
        private readonly AppSettings _settings;
        private readonly ILogger<ErrorLogger> _logger;

        private int _contadorCiclos = 0;
        private readonly List<string> _erroresCicloActual = new();

        public ErrorLogger(IOptions<AppSettings> settings, ILogger<ErrorLogger> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /*
         * Inicia un nuevo ciclo de ejecución y limpia la colección temporal de errores.
         */
        public void IniciarNuevoCiclo()
        {
            _contadorCiclos++;
            _erroresCicloActual.Clear();
        }

        /*
         * Registra un error en memoria para que quede asociado al ciclo actual.
         */
        public void RegistrarError(string nombreArchivo, string motivo)
        {
            string marcaTiempo = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string linea = string.Join(ObtenerSeparador(), new[]
            {
                EscaparCsv(marcaTiempo),
                EscaparCsv(nombreArchivo),
                EscaparCsv(motivo)
            });

            _erroresCicloActual.Add(linea);
        }

        /*
         * Guarda los errores acumulados del ciclo actual en el fichero diario.
         */
        public void FinalizarCicloYGuardar()
        {
            if (_erroresCicloActual.Count == 0)
            {
                return;
            }

            try
            {
                string directorioLogs = ObtenerDirectorioLogsErroresDiarios();
                Directory.CreateDirectory(directorioLogs);

                string rutaLog = ObtenerRutaLogActual();
                bool ficheroExiste = File.Exists(rutaLog);

                using var writer = new StreamWriter(rutaLog, append: true);

                if (!ficheroExiste)
                {
                    writer.WriteLine(
                        string.Join(ObtenerSeparador(), new[]
                        {
                            "FechaHora",
                            "Archivo",
                            "Motivo"
                        }));
                }

                writer.WriteLine(
                    string.Join(ObtenerSeparador(), new[]
                    {
                        "CICLO",
                        _contadorCiclos.ToString(),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    }));

                foreach (var error in _erroresCicloActual)
                {
                    writer.WriteLine(error);
                }

                _logger.LogInformation(
                    "Se registraron {CantidadErrores} errores técnicos en el ciclo {Ciclo}. Ruta: {RutaLog}",
                    _erroresCicloActual.Count,
                    _contadorCiclos,
                    rutaLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar el log técnico de errores.");
            }
        }

        /*
         * Devuelve la ruta del fichero diario de errores técnicos.
         */
        public string ObtenerRutaLogActual()
        {
            string directorioLogs = ObtenerDirectorioLogsErroresDiarios();

            string patron = ObtenerValorODefecto(
                _settings.ApplicationLogs.ErrorsDailyPattern,
                "yyyyMMdd_log_errores.csv");

            string nombreLog = patron.Replace(
                "yyyyMMdd",
                DateTime.Now.ToString("yyyyMMdd"));

            return Path.Combine(
                directorioLogs,
                SanitizarNombreFichero(nombreLog));
        }

        /*
         * Devuelve el tamaño actual del fichero diario de errores técnicos.
         */
        public long ObtenerTamanoLogActual()
        {
            string ruta = ObtenerRutaLogActual();
            return File.Exists(ruta) ? new FileInfo(ruta).Length : 0;
        }

        /*
         * Obtiene el directorio diario de logs técnicos de errores:
         *
         * LogsAplicacion\Errores\Diarios
         */
        private string ObtenerDirectorioLogsErroresDiarios()
        {
            string raizLogs = ObtenerRaizLogsAplicacion();

            string errores = ObtenerValorODefecto(
                _settings.ApplicationLogs.ErrorsDirectoryName,
                "Errores");

            string diarios = ObtenerValorODefecto(
                _settings.ApplicationLogs.DailyDirectoryName,
                "Diarios");

            return Path.Combine(
                raizLogs,
                SanitizarNombreDirectorio(errores),
                SanitizarNombreDirectorio(diarios));
        }

        /*
         * Obtiene la raíz de logs técnicos de aplicación.
         * Si no está configurada, se usa una ruta segura dentro del directorio base.
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
         * Obtiene el separador configurado para los CSV.
         * Si no se ha definido, usa ';' por defecto.
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
        private string ObtenerValorODefecto(string? value, string valorPorDefecto)
        {
            return string.IsNullOrWhiteSpace(value)
                ? valorPorDefecto
                : value;
        }

        /*
         * Sanitiza nombres de carpetas derivados de configuración.
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
                return "yyyyMMdd_log_errores.csv"
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
    }
}
