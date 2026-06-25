using System.Security.Cryptography;
using Heimdallhash.Config;
using Heimdallhash.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Valida un paquete ya extraído comparando los ficheros reales
     * contra la información declarada en la Delivery Note.
     * 
     * Esta clase no extrae paquetes comprimidos y no mueve ficheros.
     * Solo valida contenido y añade errores a PackageValidationResult.
     */
    public class PackageValidator
    {
        private readonly AppSettings _settings;
        private readonly ILogger<PackageValidator> _logger;

        public PackageValidator(
            IOptions<AppSettings> settings,
            ILogger<PackageValidator> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /*
         * Valida un paquete extraído previamente.
         * 
         * Parámetros:
         * - result: resultado inicial generado por DeliveryNoteReader.
         * - extractionDirectory: carpeta temporal donde está extraído el paquete.
         */
        public async Task<PackageValidationResult> ValidateAsync(
            PackageValidationResult result,
            string extractionDirectory,
            CancellationToken cancellationToken = default)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.TemporaryExtractionDirectory = extractionDirectory;

            if (result.DeliveryNote is null)
            {
                result.AddError(
                    "PACKAGE_DN_NOT_AVAILABLE",
                    "No se puede validar el paquete porque no se ha leído ninguna Delivery Note.");

                return result;
            }

            if (string.IsNullOrWhiteSpace(extractionDirectory))
            {
                result.AddError(
                    "PACKAGE_EXTRACTION_DIRECTORY_EMPTY",
                    "No se ha indicado el directorio temporal de extracción del paquete.");

                return result;
            }

            if (!Directory.Exists(extractionDirectory))
            {
                result.AddError(
                    "PACKAGE_EXTRACTION_DIRECTORY_NOT_FOUND",
                    $"No existe el directorio temporal de extracción: {extractionDirectory}");

                return result;
            }

            try
            {
                ValidarFicherosDuplicadosEnDeliveryNote(result);
                ValidarFicherosNoDeclarados(result, extractionDirectory);

                foreach (var declaredFile in result.DeliveryNote.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await ValidarFicheroDeclaradoAsync(
                        result,
                        extractionDirectory,
                        declaredFile,
                        cancellationToken);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error general durante la validación del paquete {PackageName}.",
                    result.PackageName);

                result.AddError(
                    "PACKAGE_VALIDATION_UNEXPECTED_ERROR",
                    $"Error inesperado durante la validación del paquete: {ex.Message}");

                return result;
            }
        }

        /*
         * Comprueba si la DN contiene nombres de fichero duplicados.
         * Un duplicado impide saber de forma fiable qué fichero real corresponde a cada entrada.
         */
        private static void ValidarFicherosDuplicadosEnDeliveryNote(
            PackageValidationResult result)
        {
            if (result.DeliveryNote is null)
            {
                return;
            }

            var duplicados = result.DeliveryNote.Files
                .GroupBy(file => NormalizarRutaRelativa(file.Name))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            foreach (var duplicado in duplicados)
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "DN_DUPLICATED_FILE",
                    "La Delivery Note contiene un fichero declarado más de una vez.",
                    duplicado,
                    "Name"));
            }
        }

        /*
         * Comprueba si dentro del paquete existen ficheros que no aparecen declarados en la DN.
         * La propia DN/XML queda excluida de esta comprobación.
         */
        private static void ValidarFicherosNoDeclarados(
            PackageValidationResult result,
            string extractionDirectory)
        {
            if (result.DeliveryNote is null)
            {
                return;
            }

            var declarados = result.DeliveryNote.Files
                .Select(file => NormalizarRutaRelativa(file.Name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var ficherosReales = ObtenerFicherosRealesDelPaquete(
                extractionDirectory,
                result.DeliveryNote);

            foreach (var ficheroReal in ficherosReales)
            {
                string rutaRelativa = NormalizarRutaRelativa(
                    Path.GetRelativePath(extractionDirectory, ficheroReal));

                if (!declarados.Contains(rutaRelativa))
                {
                    result.AddError(PackageValidationError.CreateForFile(
                        "PACKAGE_UNDECLARED_FILE",
                        "El paquete contiene un fichero no declarado en la Delivery Note.",
                        rutaRelativa,
                        "Name"));
                }
            }
        }

        /*
         * Valida un fichero concreto declarado en la DN.
         */
        private async Task ValidarFicheroDeclaradoAsync(
            PackageValidationResult result,
            string extractionDirectory,
            DeliveryNoteFile declaredFile,
            CancellationToken cancellationToken)
        {
            string declaredRelativePath = NormalizarRutaRelativa(declaredFile.Name);

            if (string.IsNullOrWhiteSpace(declaredRelativePath))
            {
                return;
            }

            string realFilePath = Path.Combine(extractionDirectory, declaredRelativePath);

            if (!File.Exists(realFilePath))
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "PACKAGE_DECLARED_FILE_NOT_FOUND",
                    "El fichero declarado en la Delivery Note no existe dentro del paquete.",
                    declaredFile.Name,
                    "Name"));

                return;
            }

            ValidarFormato(result, declaredFile, realFilePath);
            ValidarTamano(result, declaredFile, realFilePath);

            if (!ValidarAlgoritmoHash(result, declaredFile))
            {
                return;
            }

            await ValidarHashAsync(
                result,
                declaredFile,
                realFilePath,
                cancellationToken);
        }

        /*
         * Comprueba que el formato/extensión real coincide con el formato declarado.
         */
        private static void ValidarFormato(
            PackageValidationResult result,
            DeliveryNoteFile declaredFile,
            string realFilePath)
        {
            string formatoDeclarado = declaredFile.GetNormalizedFormat();

            string formatoReal = Path.GetExtension(realFilePath)
                .TrimStart('.')
                .ToLowerInvariant();

            if (!string.Equals(formatoDeclarado, formatoReal, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "FORMAT_MISMATCH",
                    "El formato real del fichero no coincide con el formato declarado en la Delivery Note.",
                    declaredFile.Name,
                    "Format",
                    formatoDeclarado,
                    formatoReal));
            }
        }

        /*
         * Comprueba que el tamaño real coincide con el tamaño declarado.
         */
        private static void ValidarTamano(
            PackageValidationResult result,
            DeliveryNoteFile declaredFile,
            string realFilePath)
        {
            long tamanoReal = new FileInfo(realFilePath).Length;

            if (declaredFile.Size != tamanoReal)
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "SIZE_MISMATCH",
                    "El tamaño real del fichero no coincide con el tamaño declarado en la Delivery Note.",
                    declaredFile.Name,
                    "Size",
                    declaredFile.Size.ToString(),
                    tamanoReal.ToString()));
            }
        }

        /*
         * Comprueba que el algoritmo de hash declarado está permitido por configuración.
         */
        private bool ValidarAlgoritmoHash(
            PackageValidationResult result,
            DeliveryNoteFile declaredFile)
        {
            string algoritmoDeclarado = declaredFile.GetNormalizedHashAlgorithm();

            bool permitido = _settings.Hash.AllowedAlgorithms
                .Any(algorithm => string.Equals(
                    algorithm.Trim(),
                    algoritmoDeclarado,
                    StringComparison.OrdinalIgnoreCase));

            if (!permitido)
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "HASH_ALGORITHM_NOT_ALLOWED",
                    "El algoritmo de hash declarado no está permitido por la configuración.",
                    declaredFile.Name,
                    "HashAlgorithm",
                    string.Join(",", _settings.Hash.AllowedAlgorithms),
                    algoritmoDeclarado));

                return false;
            }

            return true;
        }

        /*
         * Calcula el hash real del fichero y lo compara con el hash declarado en la DN.
         */
        private static async Task ValidarHashAsync(
            PackageValidationResult result,
            DeliveryNoteFile declaredFile,
            string realFilePath,
            CancellationToken cancellationToken)
        {
            string algoritmo = declaredFile.GetNormalizedHashAlgorithm();
            string hashDeclarado = declaredFile.GetNormalizedHash();

            string hashReal = await CalcularHashAsync(
                realFilePath,
                algoritmo,
                cancellationToken);

            if (!string.Equals(hashDeclarado, hashReal, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "HASH_MISMATCH",
                    "El hash real del fichero no coincide con el hash declarado en la Delivery Note.",
                    declaredFile.Name,
                    "Hash",
                    hashDeclarado,
                    hashReal));
            }
        }

        /*
         * Devuelve todos los ficheros reales del paquete, excluyendo la propia DN/XML.
         */
        private static List<string> ObtenerFicherosRealesDelPaquete(
            string extractionDirectory,
            DeliveryNote deliveryNote)
        {
            return Directory
                .GetFiles(extractionDirectory, "*", SearchOption.AllDirectories)
                .Where(file => !EsFicheroDeliveryNote(file, deliveryNote))
                .ToList();
        }

        /*
         * Determina si un fichero real corresponde a la DN/XML.
         */
        private static bool EsFicheroDeliveryNote(
            string filePath,
            DeliveryNote deliveryNote)
        {
            if (!string.IsNullOrWhiteSpace(deliveryNote.DeliveryNoteTempPath))
            {
                string rutaReal = Path.GetFullPath(filePath);
                string rutaDn = Path.GetFullPath(deliveryNote.DeliveryNoteTempPath);

                if (string.Equals(rutaReal, rutaDn, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(deliveryNote.DeliveryNoteFileName))
            {
                string nombreReal = Path.GetFileName(filePath);

                if (string.Equals(
                        nombreReal,
                        deliveryNote.DeliveryNoteFileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /*
         * Calcula el hash hexadecimal de un fichero usando el algoritmo indicado.
         */
        private static async Task<string> CalcularHashAsync(
            string filePath,
            string algorithm,
            CancellationToken cancellationToken)
        {
            using var hashAlgorithm = CrearAlgoritmoHash(algorithm);

            await using var stream = File.Open(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            byte[] hashBytes = await hashAlgorithm.ComputeHashAsync(
                stream,
                cancellationToken);

            return Convert.ToHexString(hashBytes);
        }

        /*
         * Crea la instancia del algoritmo de hash correspondiente.
         */
        private static HashAlgorithm CrearAlgoritmoHash(string algorithm)
        {
            return algorithm.ToUpperInvariant() switch
            {
                "MD5" => MD5.Create(),
                "SHA1" => SHA1.Create(),
                "SHA256" => SHA256.Create(),
                "SHA384" => SHA384.Create(),
                "SHA512" => SHA512.Create(),
                _ => throw new InvalidOperationException(
                    $"Algoritmo de hash no soportado: {algorithm}")
            };
        }

        /*
         * Normaliza una ruta relativa para evitar diferencias entre '\' y '/'.
         */
        private static string NormalizarRutaRelativa(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            return relativePath
                .Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
        }
    }
}