namespace Heimdallhash.Models
{
    /*
     * Representa un error detectado durante la validación de un paquete.
     * 
     * Se usa para registrar de forma estructurada por qué un paquete debe ir
     * a cuarentena o por qué no puede ser aceptado.
     */
    public class PackageValidationError
    {
        // Código normalizado del error.
        // Ejemplo: HASH_MISMATCH, INVALID_XML, UNKNOWN_CENTER_ID.
        public string ErrorCode { get; set; } = string.Empty;

        // Descripción legible del error.
        public string Message { get; set; } = string.Empty;

        // Nombre del fichero afectado, si el error corresponde a un fichero concreto.
        public string FileName { get; set; } = string.Empty;

        // Campo afectado por el error, si aplica.
        // Ejemplo: Size, Hash, Format, DestinationCenterId.
        public string FieldName { get; set; } = string.Empty;

        // Valor esperado según la DN o configuración.
        public string ExpectedValue { get; set; } = string.Empty;

        // Valor real detectado durante la validación.
        public string ActualValue { get; set; } = string.Empty;

        // Crea un error simple sin fichero asociado.
        public static PackageValidationError Create(
            string errorCode,
            string message)
        {
            return new PackageValidationError
            {
                ErrorCode = errorCode,
                Message = message
            };
        }

        // Crea un error asociado a un fichero concreto.
        public static PackageValidationError CreateForFile(
            string errorCode,
            string message,
            string fileName,
            string fieldName = "",
            string expectedValue = "",
            string actualValue = "")
        {
            return new PackageValidationError
            {
                ErrorCode = errorCode,
                Message = message,
                FileName = fileName,
                FieldName = fieldName,
                ExpectedValue = expectedValue,
                ActualValue = actualValue
            };
        }
    }
}