namespace Heimdallhash.Models
{
    /*
     * Representa un producto válido que no ha podido entregarse a su ruta destino.
     *
     * No se considera un producto inválido, por lo que no debe ir automáticamente
     * a cuarentena. Queda almacenado en PendienteEntrega para reintentos posteriores.
     */
    public class PendingDeliveryRecord
    {
        // Identificador propio del registro pendiente.
        public string PendingId { get; set; } = Guid.NewGuid().ToString();

        // EventId original generado durante la validación del producto.
        public string OriginalEventId { get; set; } = string.Empty;

        // Nombre del producto.
        public string ProductName { get; set; } = string.Empty;

        // Ruta original donde se recibió el producto.
        public string OriginalProductPath { get; set; } = string.Empty;

        // Ruta donde queda almacenado temporalmente el producto pendiente.
        public string PendingProductPath { get; set; } = string.Empty;

        // Flujo operativo asociado: Upload o Download.
        public string Flow { get; set; } = string.Empty;

        // Centro destino leído desde la Delivery Note.
        public string CenterId { get; set; } = string.Empty;

        // Ruta destino que no estaba disponible en el momento de la entrega.
        public string DestinationPath { get; set; } = string.Empty;

        // Estado actual del pendiente.
        public string Status { get; set; } = "PENDING";

        // Número de intentos de entrega realizados.
        public int Attempts { get; set; }

        // Número de ciclos en los que el producto no ha podido entregarse.
        public int FailedCycles { get; set; }

        // Fecha/hora local de creación del pendiente.
        public string CreatedAtLocal { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Fecha/hora UTC de creación del pendiente.
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Último error registrado durante la entrega.
        public string LastError { get; set; } = string.Empty;
    }
}
