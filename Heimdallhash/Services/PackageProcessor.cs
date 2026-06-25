using Heimdallhash.Config;
using Heimdallhash.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Orquesta el procesamiento lógico de un paquete comprimido.
     * 
     * Esta clase une las piezas ya creadas:
     * - ArchiveExtractor
     * - DeliveryNoteLocator
     * - DeliveryNoteReader
     * - PackageValidator
     * 
     * No mueve todavía el paquete a destino, cuarentena o pendientes.
     * Solo devuelve un PackageValidationResult completo para que FileProcessor
     * pueda tomar la decisión correspondiente en el siguiente paso.
     */
    public class PackageProcessor
    {
        private readonly AppSettings _settings;
        private readonly ArchiveExtractor _archiveExtractor;
        private readonly DeliveryNoteLocator _deliveryNoteLocator;
        private readonly DeliveryNoteReader _deliveryNoteReader;
        private readonly PackageValidator _packageValidator;
        private readonly ILogger<PackageProcessor> _logger;

        public PackageProcessor(
            IOptions<AppSettings> settings,
            ArchiveExtractor archiveExtractor,
            DeliveryNoteLocator deliveryNoteLocator,
            DeliveryNoteReader deliveryNoteReader,
            PackageValidator packageValidator,
            ILogger<PackageProcessor> logger)
        {
            _settings = settings.Value;
            _archiveExtractor = archiveExtractor;
            _deliveryNoteLocator = deliveryNoteLocator;
            _deliveryNoteReader = deliveryNoteReader;
            _packageValidator = packageValidator;
            _logger = logger;
        }

        /*
         * Procesa un paquete comprimido desde una ruta monitorizada.
         * 
         * El resultado indica si el paquete es válido o qué errores se detectaron.
         */
        public async Task<PackageValidationResult> ProcessAsync(
            string packagePath,
            WatchRouteConfig routeConfig,
            CancellationToken cancellationToken = default)
        {
            var result = new PackageValidationResult
            {
                PackagePath = packagePath,
                PackageName = Path.GetFileName(packagePath),
                Flow = routeConfig.Flow
            };

            ArchiveExtractionResult? extractionResult = null;

            try
            {
                if (string.IsNullOrWhiteSpace(packagePath))
                {
                    result.AddError(
                        "PACKAGE_PATH_EMPTY",
                        "La ruta del paquete está vacía.");

                    return result;
                }

                if (!File.Exists(packagePath))
                {
                    result.AddError(
                        "PACKAGE_NOT_FOUND",
                        $"No se encontró el paquete: {packagePath}");

                    return result;
                }

                _logger.LogInformation(
                    "Iniciando procesamiento del paquete {PackageName}.",
                    result.PackageName);

                extractionResult = await _archiveExtractor.ExtractAsync(
                    packagePath,
                    cancellationToken);

                result.EventId = extractionResult.EventId;
                result.TemporaryExtractionDirectory = extractionResult.ExtractionDirectory;

                if (!extractionResult.Success)
                {
                    result.AddError(
                        extractionResult.ErrorCode,
                        extractionResult.ErrorMessage);

                    return result;
                }

                string? xmlPath = _deliveryNoteLocator.LocateSingleXml(
                    result,
                    extractionResult.ExtractionDirectory);

                if (xmlPath is null)
                {
                    return result;
                }

                PackageValidationResult deliveryNoteResult =
                    _deliveryNoteReader.ReadFromXmlFile(xmlPath);

                FusionarResultadoDeliveryNote(result, deliveryNoteResult);

                if (!result.IsValid)
                {
                    return result;
                }

                ResolverRutaDestino(result, routeConfig.Flow);

                if (!result.IsValid)
                {
                    return result;
                }

                result = await _packageValidator.ValidateAsync(
                    result,
                    extractionResult.ExtractionDirectory,
                    cancellationToken);

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
                    "Error inesperado al procesar el paquete {PackagePath}.",
                    packagePath);

                result.AddError(
                    "PACKAGE_PROCESSING_UNEXPECTED_ERROR",
                    $"Error inesperado al procesar el paquete: {ex.Message}");

                return result;
            }
            finally
            {
                if (_settings.ArchiveProcessing.CleanTemporaryFilesAfterProcessing &&
                    extractionResult is not null &&
                    !string.IsNullOrWhiteSpace(extractionResult.ExtractionDirectory))
                {
                    _archiveExtractor.CleanExtractionDirectory(
                        extractionResult.ExtractionDirectory);
                }
            }
        }

        /*
         * Fusiona el resultado de lectura de la DN con el resultado principal
         * del procesamiento del paquete.
         */
        private static void FusionarResultadoDeliveryNote(
            PackageValidationResult result,
            PackageValidationResult deliveryNoteResult)
        {
            result.DeliveryNote = deliveryNoteResult.DeliveryNote;
            result.DestinationCenterId = deliveryNoteResult.DestinationCenterId;

            foreach (var error in deliveryNoteResult.Errors)
            {
                result.AddError(error);
            }
        }

        /*
         * Resuelve la ruta destino a partir de:
         * - DestinationCenterId leído desde la DN.
         * - Flow asociado a la ruta monitorizada.
         */
        private void ResolverRutaDestino(
            PackageValidationResult result,
            string flow)
        {
            if (string.IsNullOrWhiteSpace(result.DestinationCenterId))
            {
                result.AddError(
                    "DESTINATION_CENTER_EMPTY",
                    "No se puede resolver la ruta destino porque DestinationCenterId está vacío.");

                return;
            }

            if (string.IsNullOrWhiteSpace(flow))
            {
                result.AddError(
                    "FLOW_EMPTY",
                    "No se puede resolver la ruta destino porque el flujo está vacío.");

                return;
            }

            var centerRoute = _settings.CenterRoutes.FirstOrDefault(route =>
                route.Enabled &&
                string.Equals(route.CenterId, result.DestinationCenterId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(route.Flow, flow, StringComparison.OrdinalIgnoreCase));

            if (centerRoute is null)
            {
                result.AddError(
                    "DESTINATION_ROUTE_NOT_FOUND",
                    $"No existe una ruta destino habilitada para el centro {result.DestinationCenterId} y flujo {flow}.");

                return;
            }

            if (string.IsNullOrWhiteSpace(centerRoute.DestinationPath))
            {
                result.AddError(
                    "DESTINATION_ROUTE_EMPTY",
                    $"La ruta destino configurada para el centro {result.DestinationCenterId} y flujo {flow} está vacía.");

                return;
            }

            result.DestinationPath = centerRoute.DestinationPath;
        }
    }
}