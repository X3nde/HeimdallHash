namespace Heimdallhash.Models
{
    /*
     * Representa el resultado completo de validar un paquete comprimido.
     * 
     * Este objeto será usado por el procesador de paquetes para decidir si
     * el paquete se mueve a destino, queda pendiente de distribución o va a cuarentena.
     */
    public class PackageValidationResult
    {
        // Identificador único del evento de validación.
        // Sirve para trazabilidad, libros de registro y notificaciones.
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        // Ruta original del paquete recibido.
        public string PackagePath { get; set; } = string.Empty;

        // Nombre del paquete recibido.
        public string PackageName { get; set; } = string.Empty;

        // Flujo en el que se procesó el paquete. Valores previstos: Upload o Download.
        public string Flow { get; set; } = string.Empty;

        // Centro destino leído desde la Delivery Note.
        public string DestinationCenterId { get; set; } = string.Empty;

        // Ruta destino resuelta a partir de CenterId + Flow.
        public string DestinationPath { get; set; } = string.Empty;

        // Ruta temporal donde se extrajo el paquete.
        public string TemporaryExtractionDirectory { get; set; } = string.Empty;

        // Delivery Note leída desde el paquete.
        // Puede ser null si no se encontró XML o si el XML era inválido.
        public DeliveryNote? DeliveryNote { get; set; }

        // Errores detectados durante la validación.
        // Si esta lista está vacía, el paquete se considera válido.
        public List<PackageValidationError> Errors { get; set; } = new();

        // Indica si el paquete es válido.
        public bool IsValid => Errors.Count == 0;

        // Añade un error al resultado de validación.
        public void AddError(PackageValidationError error)
        {
            Errors.Add(error);
        }

        // Añade un error simple al resultado de validación.
        public void AddError(string errorCode, string message)
        {
            Errors.Add(PackageValidationError.Create(errorCode, message));
        }

        // Devuelve el primer código de error encontrado.
        // Es útil para registrar una causa principal de cuarentena.
        public string GetMainErrorCode()
        {
            return Errors.Count > 0
                ? Errors[0].ErrorCode
                : string.Empty;
        }

        // Devuelve una descripción resumida de todos los errores.
        public string GetErrorSummary()
        {
            if (Errors.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(
                " | ",
                Errors.Select(error => $"{error.ErrorCode}: {error.Message}"));
        }
    }
}