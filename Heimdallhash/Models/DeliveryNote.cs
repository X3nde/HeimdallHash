namespace Heimdallhash.Models
{
    /*
     * Representa la Delivery Note/DN extraída desde el XML incluido en un paquete.
     * 
     * La DN contiene el identificador del centro destino y la lista de ficheros
     * que deben existir dentro del paquete comprimido.
     */
    public class DeliveryNote
    {
        // ID numérico del centro destino. Debe tener entre 4 y 10 caracteres.
        public string DestinationCenterId { get; set; } = string.Empty;

        // Nombre del fichero XML encontrado dentro del paquete.
        // Se conserva para trazabilidad.
        public string DeliveryNoteFileName { get; set; } = string.Empty;

        // Ruta temporal donde se encontró o extrajo la DN durante el procesamiento.
        public string DeliveryNoteTempPath { get; set; } = string.Empty;

        // Lista de ficheros declarados en la DN.
        // Todos los ficheros del paquete, excepto la propia DN/XML, deben estar aquí.
        public List<DeliveryNoteFile> Files { get; set; } = new();

        /*
         * Comprueba si la DN contiene los datos mínimos para ser procesada.
         * Esta validación no sustituye a la validación completa del paquete.
         */
        public bool HasMinimumData()
        {
            return !string.IsNullOrWhiteSpace(DestinationCenterId)
                   && Files.Count > 0;
        }
    }
}