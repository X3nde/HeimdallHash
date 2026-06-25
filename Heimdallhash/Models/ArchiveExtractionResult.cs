namespace Heimdallhash.Models
{
    /*
     * Representa el resultado de extraer un paquete comprimido.
     * 
     * No valida todavía la Delivery Note ni los ficheros internos.
     * Solo indica si la extracción fue correcta y dónde quedó el contenido.
     */
    public class ArchiveExtractionResult
    {
        // Identificador único de la operación de extracción.
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        // Ruta original del paquete comprimido.
        public string ArchivePath { get; set; } = string.Empty;

        // Nombre del paquete comprimido.
        public string ArchiveName { get; set; } = string.Empty;

        // Directorio temporal donde se ha extraído el paquete.
        public string ExtractionDirectory { get; set; } = string.Empty;

        // Indica si la extracción finalizó correctamente.
        public bool Success { get; set; }

        // Código de error si la extracción falla.
        public string ErrorCode { get; set; } = string.Empty;

        // Mensaje de error si la extracción falla.
        public string ErrorMessage { get; set; } = string.Empty;

        // Crea un resultado correcto.
        public static ArchiveExtractionResult Ok(
            string archivePath,
            string extractionDirectory)
        {
            return new ArchiveExtractionResult
            {
                ArchivePath = archivePath,
                ArchiveName = Path.GetFileName(archivePath),
                ExtractionDirectory = extractionDirectory,
                Success = true
            };
        }

        // Crea un resultado erróneo.
        public static ArchiveExtractionResult Fail(
            string archivePath,
            string extractionDirectory,
            string errorCode,
            string errorMessage)
        {
            return new ArchiveExtractionResult
            {
                ArchivePath = archivePath,
                ArchiveName = Path.GetFileName(archivePath),
                ExtractionDirectory = extractionDirectory,
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }
}