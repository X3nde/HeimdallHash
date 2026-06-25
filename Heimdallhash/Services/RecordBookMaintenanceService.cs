using Heimdallhash.Config;
using Heimdallhash.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Heimdallhash.Services
{
    /*
     * Mantiene los libros de registro funcionales.
     *
     * Responsabilidades:
     *
     * - Fusionar libros diarios antiguos en libros mensuales.
     * - Archivar o eliminar diarios ya fusionados según configuración.
     *
     * Este servicio actúa sobre LibrosRegistro, no sobre LogsAplicacion.
     */
    public class RecordBookMaintenanceService
    {
        private readonly AppSettings _settings;
        private readonly ILogger<RecordBookMaintenanceService> _logger;

        private static readonly Regex DailyBookPattern = new(
            @"^(?<date>\d{8})(?<suffix>_.*\.csv)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public RecordBookMaintenanceService(
            IOptions<AppSettings> settings,
            ILogger<RecordBookMaintenanceService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /*
         * Ejecuta mantenimiento de libros de registro al arranque.
         */
        public async Task EjecutarMantenimientoInicialAsync(
            CancellationToken cancellationToken = default)
        {
            if (!_settings.RecordBooks.MergePreviousDaysOnCycleStart)
            {
                _logger.LogInformation(
                    "Mantenimiento de libros de registro desactivado por configuración.");

                return;
            }

            string raizLibros = ObtenerRaizLibrosRegistro();

            if (!Directory.Exists(raizLibros))
            {
                _logger.LogInformation(
                    "No se ejecuta mantenimiento de libros porque no existe la raíz: {RaizLibros}",
                    raizLibros);

                return;
            }

            var librosDiarios = ObtenerLibrosDiariosAntiguos(raizLibros)
                .ToList();

            if (librosDiarios.Count == 0)
            {
                _logger.LogInformation(
                    "No hay libros diarios antiguos pendientes de fusionar.");

                return;
            }

            _logger.LogInformation(
                "Se han detectado {Cantidad} libro(s) diario(s) antiguo(s) para mantenimiento.",
                librosDiarios.Count);

            foreach (string libroDiario in librosDiarios)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await FusionarLibroDiarioAsync(
                    libroDiario,
                    cancellationToken);
            }
        }

        /*
         * Obtiene libros diarios antiguos.
         *
         * No procesa libros del día actual para no interferir con el ciclo activo.
         */
        private IEnumerable<string> ObtenerLibrosDiariosAntiguos(string raizLibros)
        {
            string nombreDirectorioDiario = ObtenerValorODefecto(
                _settings.RecordBooks.DailyDirectoryName,
                "Diarios");

            string fechaHoy = DateTime.Now.ToString("yyyyMMdd");

            foreach (string fichero in Directory.EnumerateFiles(
                         raizLibros,
                         "*.csv",
                         SearchOption.AllDirectories))
            {
                string? directorio = Path.GetDirectoryName(fichero);

                if (string.IsNullOrWhiteSpace(directorio))
                {
                    continue;
                }

                bool estaEnDiarios = directorio
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(segmento => segmento.Equals(
                        nombreDirectorioDiario,
                        StringComparison.OrdinalIgnoreCase));

                if (!estaEnDiarios)
                {
                    continue;
                }

                string nombreFichero = Path.GetFileName(fichero);
                Match match = DailyBookPattern.Match(nombreFichero);

                if (!match.Success)
                {
                    continue;
                }

                string fechaLibro = match.Groups["date"].Value;

                if (fechaLibro == fechaHoy)
                {
                    continue;
                }

                yield return fichero;
            }
        }

        /*
         * Fusiona un libro diario en su equivalente mensual.
         */
        private async Task FusionarLibroDiarioAsync(
            string rutaLibroDiario,
            CancellationToken cancellationToken)
        {
            string? rutaLibroMensual = ObtenerRutaLibroMensual(rutaLibroDiario);

            if (string.IsNullOrWhiteSpace(rutaLibroMensual))
            {
                _logger.LogWarning(
                    "No se pudo resolver libro mensual para el diario: {RutaLibroDiario}",
                    rutaLibroDiario);

                return;
            }

            try
            {
                await RetryHelper.EjecutarConReintentosAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await FusionarContenidoAsync(
                        rutaLibroDiario,
                        rutaLibroMensual,
                        cancellationToken);

                    return true;
                },
                _settings.RecordBooks.MaxWriteAttempts,
                _settings.RecordBooks.RetryDelayMilliseconds);

                GestionarLibroDiarioTrasFusion(
                    rutaLibroDiario);

                _logger.LogInformation(
                    "Libro diario fusionado correctamente. Diario={RutaLibroDiario}. Mensual={RutaLibroMensual}",
                    rutaLibroDiario,
                    rutaLibroMensual);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al fusionar libro diario {RutaLibroDiario}.",
                    rutaLibroDiario);
            }
        }

        /*
         * Copia el contenido del diario en el mensual.
         *
         * Si el mensual no existe, copia cabecera y datos.
         * Si el mensual existe, copia solo líneas de datos.
         */
        private async Task FusionarContenidoAsync(
            string rutaLibroDiario,
            string rutaLibroMensual,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(rutaLibroDiario))
            {
                return;
            }

            string[] lineasDiario = await File.ReadAllLinesAsync(
                rutaLibroDiario,
                cancellationToken);

            if (lineasDiario.Length == 0)
            {
                return;
            }

            string? directorioMensual = Path.GetDirectoryName(rutaLibroMensual);

            if (!string.IsNullOrWhiteSpace(directorioMensual))
            {
                Directory.CreateDirectory(directorioMensual);
            }

            bool mensualExiste = File.Exists(rutaLibroMensual);

            await using var stream = new FileStream(
                rutaLibroMensual,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);

            await using var writer = new StreamWriter(stream);

            if (!mensualExiste)
            {
                await writer.WriteLineAsync(lineasDiario[0]);
            }

            foreach (string linea in lineasDiario.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(linea))
                {
                    continue;
                }

                await writer.WriteLineAsync(linea);
            }
        }

        /*
         * Archiva, elimina o conserva el diario tras fusionarlo.
         */
        private void GestionarLibroDiarioTrasFusion(string rutaLibroDiario)
        {
            if (_settings.RecordBooks.DeleteDailyAfterMonthlyMerge)
            {
                File.Delete(rutaLibroDiario);

                _logger.LogInformation(
                    "Libro diario eliminado tras fusión mensual: {RutaLibroDiario}",
                    rutaLibroDiario);

                return;
            }

            if (_settings.RecordBooks.MoveDailyToArchiveAfterMerge)
            {
                string? rutaArchivo = ObtenerRutaArchivoDiario(rutaLibroDiario);

                if (string.IsNullOrWhiteSpace(rutaArchivo))
                {
                    _logger.LogWarning(
                        "No se pudo resolver ruta de archivo para el libro diario: {RutaLibroDiario}",
                        rutaLibroDiario);

                    return;
                }

                string? directorioArchivo = Path.GetDirectoryName(rutaArchivo);

                if (!string.IsNullOrWhiteSpace(directorioArchivo))
                {
                    Directory.CreateDirectory(directorioArchivo);
                }

                string rutaFinalArchivo = ResolverRutaSinSobrescritura(rutaArchivo);

                File.Move(
                    rutaLibroDiario,
                    rutaFinalArchivo,
                    overwrite: false);

                _logger.LogInformation(
                    "Libro diario archivado tras fusión mensual: {RutaFinalArchivo}",
                    rutaFinalArchivo);

                return;
            }

            _logger.LogWarning(
                "El libro diario {RutaLibroDiario} fue fusionado, pero no se eliminó ni archivó. Podría volver a fusionarse en el siguiente arranque.",
                rutaLibroDiario);
        }

        /*
         * Resuelve la ruta mensual equivalente a un libro diario.
         *
         * Ejemplo:
         *
         * 20260621_LR_productos_aceptados.csv
         * -> 202606_LR_productos_aceptados.csv
         *
         * Diarios
         * -> Mensuales
         */
        private string? ObtenerRutaLibroMensual(string rutaLibroDiario)
        {
            string nombreDiario = Path.GetFileName(rutaLibroDiario);
            Match match = DailyBookPattern.Match(nombreDiario);

            if (!match.Success)
            {
                return null;
            }

            string fechaDiaria = match.Groups["date"].Value;
            string sufijo = match.Groups["suffix"].Value;
            string fechaMensual = fechaDiaria[..6];

            string nombreMensual = $"{fechaMensual}{sufijo}";

            string? directorioDiario = Path.GetDirectoryName(rutaLibroDiario);

            if (string.IsNullOrWhiteSpace(directorioDiario))
            {
                return null;
            }

            string directorioMensual = ReemplazarUltimoSegmento(
                directorioDiario,
                ObtenerValorODefecto(_settings.RecordBooks.DailyDirectoryName, "Diarios"),
                ObtenerValorODefecto(_settings.RecordBooks.MonthlyDirectoryName, "Mensuales"));

            return Path.Combine(
                directorioMensual,
                SanitizarNombreFichero(nombreMensual));
        }

        /*
         * Resuelve la ruta de archivo equivalente a un libro diario.
         */
        private string? ObtenerRutaArchivoDiario(string rutaLibroDiario)
        {
            string? directorioDiario = Path.GetDirectoryName(rutaLibroDiario);

            if (string.IsNullOrWhiteSpace(directorioDiario))
            {
                return null;
            }

            string directorioArchivo = ReemplazarUltimoSegmento(
                directorioDiario,
                ObtenerValorODefecto(_settings.RecordBooks.DailyDirectoryName, "Diarios"),
                ObtenerValorODefecto(_settings.RecordBooks.ArchiveDirectoryName, "Archivo"));

            return Path.Combine(
                directorioArchivo,
                Path.GetFileName(rutaLibroDiario));
        }

        /*
         * Reemplaza el último segmento coincidente de una ruta.
         */
        private static string ReemplazarUltimoSegmento(
            string ruta,
            string segmentoOrigen,
            string segmentoDestino)
        {
            var segmentos = ruta.Split(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
                .ToList();

            for (int i = segmentos.Count - 1; i >= 0; i--)
            {
                if (segmentos[i].Equals(segmentoOrigen, StringComparison.OrdinalIgnoreCase))
                {
                    segmentos[i] = segmentoDestino;
                    break;
                }
            }

            return string.Join(Path.DirectorySeparatorChar, segmentos);
        }

        /*
         * Evita sobrescribir archivos ya archivados.
         */
        private static string ResolverRutaSinSobrescritura(string ruta)
        {
            if (!File.Exists(ruta))
            {
                return ruta;
            }

            string? directorio = Path.GetDirectoryName(ruta);
            string nombreSinExtension = Path.GetFileNameWithoutExtension(ruta);
            string extension = Path.GetExtension(ruta);

            string marcaTiempo = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            return Path.Combine(
                directorio ?? string.Empty,
                $"{nombreSinExtension}_{marcaTiempo}{extension}");
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
         * Devuelve un valor configurado o un valor por defecto.
         */
        private static string ObtenerValorODefecto(string? value, string valorPorDefecto)
        {
            return string.IsNullOrWhiteSpace(value)
                ? valorPorDefecto
                : value;
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
                return "libro.csv";
            }

            foreach (char caracterInvalido in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(caracterInvalido, '_');
            }

            return value;
        }
    }
}
