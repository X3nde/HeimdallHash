using System.Diagnostics;
using System.IO.Compression;
using Heimdallhash.Config;
using Heimdallhash.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Extrae paquetes comprimidos a un directorio temporal.
     * 
     * Esta clase no valida la Delivery Note, no calcula hashes y no mueve
     * paquetes a destino o cuarentena. Solo se encarga de la extracción.
     */
    public class ArchiveExtractor
    {
        private readonly AppSettings _settings;
        private readonly ILogger<ArchiveExtractor> _logger;

        public ArchiveExtractor(
            IOptions<AppSettings> settings,
            ILogger<ArchiveExtractor> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /*
         * Extrae un paquete comprimido a una carpeta temporal única.
         */
        public async Task<ArchiveExtractionResult> ExtractAsync(
            string archivePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                return ArchiveExtractionResult.Fail(
                    archivePath,
                    string.Empty,
                    "ARCHIVE_PATH_EMPTY",
                    "La ruta del paquete comprimido está vacía.");
            }

            if (!File.Exists(archivePath))
            {
                return ArchiveExtractionResult.Fail(
                    archivePath,
                    string.Empty,
                    "ARCHIVE_NOT_FOUND",
                    $"No se encontró el paquete comprimido: {archivePath}");
            }

            string extension = Path.GetExtension(archivePath).ToLowerInvariant();

            if (!EsExtensionSoportada(extension))
            {
                return ArchiveExtractionResult.Fail(
                    archivePath,
                    string.Empty,
                    "ARCHIVE_EXTENSION_NOT_SUPPORTED",
                    $"La extensión del paquete no está soportada: {extension}");
            }

            string extractionDirectory = CrearDirectorioTemporalExtraccion(archivePath);

            try
            {
                Directory.CreateDirectory(extractionDirectory);

                if (extension == ".zip")
                {
                    ExtraerZipNativo(archivePath, extractionDirectory);
                }
                else
                {
                    await ExtraerConSevenZipAsync(
                        archivePath,
                        extractionDirectory,
                        cancellationToken);
                }

                _logger.LogInformation(
                    "Paquete {ArchiveName} extraído correctamente en {ExtractionDirectory}.",
                    Path.GetFileName(archivePath),
                    extractionDirectory);

                return ArchiveExtractionResult.Ok(
                    archivePath,
                    extractionDirectory);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al extraer el paquete {ArchivePath}.",
                    archivePath);

                return ArchiveExtractionResult.Fail(
                    archivePath,
                    extractionDirectory,
                    "ARCHIVE_EXTRACTION_FAILED",
                    ex.Message);
            }
        }

        /*
         * Elimina un directorio temporal de extracción.
         */
        public void CleanExtractionDirectory(string extractionDirectory)
        {
            if (string.IsNullOrWhiteSpace(extractionDirectory))
            {
                return;
            }

            try
            {
                if (Directory.Exists(extractionDirectory))
                {
                    Directory.Delete(extractionDirectory, recursive: true);

                    _logger.LogInformation(
                        "Directorio temporal eliminado: {ExtractionDirectory}.",
                        extractionDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo eliminar el directorio temporal {ExtractionDirectory}.",
                    extractionDirectory);
            }
        }

        /*
         * Comprueba si la extensión está permitida en la configuración.
         */
        private bool EsExtensionSoportada(string extension)
        {
            return _settings.ArchiveProcessing.SupportedExtensions
                .Any(supportedExtension => string.Equals(
                    supportedExtension,
                    extension,
                    StringComparison.OrdinalIgnoreCase));
        }

        /*
         * Crea una carpeta temporal única para extraer el producto.
         */
        private string CrearDirectorioTemporalExtraccion(string archivePath)
        {
            string temporaryRoot = ObtenerDirectorioTemporalRaiz();

            string archiveNameWithoutExtension = SanitizarNombreDirectorio(
                Path.GetFileNameWithoutExtension(archivePath));

            string uniqueId = Guid.NewGuid().ToString("N");

            return Path.Combine(
                temporaryRoot,
                $"{archiveNameWithoutExtension}_{uniqueId}");
        }

        /*
         * Obtiene la carpeta raíz temporal desde configuración.
         *
         * Orden de prioridad:
         * 1. ArchiveProcessing.TemporaryDirectory, si se define explícitamente.
         * 2. Storage.TempDirectory, como raíz temporal interna principal.
         * 3. HeimdallHashData\Temp bajo el directorio de ejecución, como fallback seguro.
         */
        private string ObtenerDirectorioTemporalRaiz()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ArchiveProcessing.TemporaryDirectory))
            {
                return _settings.ArchiveProcessing.TemporaryDirectory;
            }

            if (!string.IsNullOrWhiteSpace(_settings.Storage.TempDirectory))
            {
                return _settings.Storage.TempDirectory;
            }

            _logger.LogWarning(
                "No se ha configurado ArchiveProcessing.TemporaryDirectory ni Storage.TempDirectory. Se usará una ruta temporal bajo el directorio base de la aplicación.");

            return Path.Combine(
                AppContext.BaseDirectory,
                "HeimdallHashData",
                "Temp");
        }

        /*
         * Sanitiza nombres de carpeta derivados del nombre del producto.
         */
        private static string SanitizarNombreDirectorio(string value)
        {
            value ??= string.Empty;
            value = value.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                return "Producto";
            }

            foreach (char caracterInvalido in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(caracterInvalido, '_');
            }

            return value;
        }

        /*
         * Extrae archivos ZIP usando las librerías nativas de .NET.
         */
        private static void ExtraerZipNativo(
            string archivePath,
            string extractionDirectory)
        {
            ZipFile.ExtractToDirectory(
                archivePath,
                extractionDirectory,
                overwriteFiles: true);
        }

        /*
         * Extrae paquetes .7z o .rar usando 7z.exe.
         * 
         * Se usa como primera solución operativa para estos formatos.
         * Más adelante se podrá añadir SharpCompress si interesa mantener
         * un extractor gestionado por código .NET.
         */
        private async Task ExtraerConSevenZipAsync(
            string archivePath,
            string extractionDirectory,
            CancellationToken cancellationToken)
        {
            string sevenZipPath = _settings.ArchiveProcessing.SevenZipExecutablePath;

            if (string.IsNullOrWhiteSpace(sevenZipPath))
            {
                throw new InvalidOperationException(
                    "No se ha configurado la ruta de 7z.exe.");
            }

            if (!File.Exists(sevenZipPath))
            {
                throw new FileNotFoundException(
                    "No se encontró el ejecutable 7z.exe configurado.",
                    sevenZipPath);
            }

            /*
             * Argumentos:
             * x  = extraer con rutas completas.
             * -y = responder sí a sobrescrituras.
             * -o = directorio de salida.
             */
            string arguments = $"x \"{archivePath}\" -o\"{extractionDirectory}\" -y";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };

            process.Start();

            string standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            string standardError = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                string detalle = !string.IsNullOrWhiteSpace(standardError)
                    ? standardError
                    : standardOutput;

                throw new InvalidOperationException(
                    $"7z.exe devolvió código de salida {process.ExitCode}. Detalle: {detalle}");
            }
        }
    }
}