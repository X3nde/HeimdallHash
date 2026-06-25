using Heimdallhash.Models;
using Microsoft.Extensions.Logging;

namespace Heimdallhash.Services
{
    /*
     * Localiza la Delivery Note dentro de un paquete ya extraído.
     * 
     * Regla operativa actual:
     * - Si no hay ningún XML, el paquete no es válido.
     * - Si hay exactamente un XML, se asume que es la Delivery Note.
     * - Si hay más de un XML, el paquete no es válido porque no se puede
     *   determinar de forma inequívoca cuál es la DN.
     */
    public class DeliveryNoteLocator
    {
        private readonly ILogger<DeliveryNoteLocator> _logger;

        public DeliveryNoteLocator(ILogger<DeliveryNoteLocator> logger)
        {
            _logger = logger;
        }

        /*
         * Busca la única Delivery Note XML dentro del directorio de extracción.
         * 
         * Devuelve la ruta del XML si la localización es correcta.
         * Devuelve null si no se encuentra XML o si hay más de uno.
         * Los errores se añaden al PackageValidationResult recibido.
         */
        public string? LocateSingleXml(
            PackageValidationResult result,
            string extractionDirectory)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (string.IsNullOrWhiteSpace(extractionDirectory))
            {
                result.AddError(
                    "DN_EXTRACTION_DIRECTORY_EMPTY",
                    "No se ha indicado el directorio de extracción donde buscar la Delivery Note.");

                return null;
            }

            if (!Directory.Exists(extractionDirectory))
            {
                result.AddError(
                    "DN_EXTRACTION_DIRECTORY_NOT_FOUND",
                    $"No existe el directorio de extracción donde buscar la Delivery Note: {extractionDirectory}");

                return null;
            }

            try
            {
                var xmlFiles = Directory
                    .GetFiles(extractionDirectory, "*.xml", SearchOption.AllDirectories)
                    .ToList();

                if (xmlFiles.Count == 0)
                {
                    result.AddError(
                        "DN_XML_NOT_FOUND_IN_PACKAGE",
                        "El paquete no contiene ningún fichero XML de Delivery Note.");

                    return null;
                }

                if (xmlFiles.Count > 1)
                {
                    string xmlList = string.Join(
                        ", ",
                        xmlFiles.Select(file => Path.GetRelativePath(extractionDirectory, file)));

                    result.AddError(
                        "DN_MULTIPLE_XML_FOUND_IN_PACKAGE",
                        $"El paquete contiene más de un fichero XML. No se puede determinar cuál es la Delivery Note. XML encontrados: {xmlList}");

                    return null;
                }

                string xmlPath = xmlFiles[0];

                _logger.LogInformation(
                    "Delivery Note localizada correctamente: {XmlPath}.",
                    xmlPath);

                return xmlPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al localizar la Delivery Note en {ExtractionDirectory}.",
                    extractionDirectory);

                result.AddError(
                    "DN_XML_LOCATION_ERROR",
                    $"Error al localizar la Delivery Note: {ex.Message}");

                return null;
            }
        }
    }
}