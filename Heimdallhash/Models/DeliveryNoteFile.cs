namespace Heimdallhash.Models
{
    /*
     * Representa un fichero declarado dentro de la Delivery Note.
     * 
     * Cada entrada debe corresponderse con un fichero real dentro del paquete,
     * salvo la propia DN/XML, que queda excluida de la validación de contenido.
     */
    public class DeliveryNoteFile
    {
        // Nombre esperado del fichero dentro del paquete. Ejemplo: documento1.pdf
        public string Name { get; set; } = string.Empty;

        // Nombre original del fichero antes de posibles transformaciones.
        // Se conserva como dato de trazabilidad.
        public string OriginalName { get; set; } = string.Empty;

        // Formato o extensión declarada. Ejemplo: pdf, txt, docx.
        public string Format { get; set; } = string.Empty;

        // Tamaño declarado del fichero en bytes.
        public long Size { get; set; }

        // Algoritmo utilizado para calcular el hash declarado.
        // Ejemplo: MD5, SHA1, SHA256, SHA384 o SHA512.
        public string HashAlgorithm { get; set; } = string.Empty;

        // Hash hexadecimal declarado en la Delivery Note.
        public string Hash { get; set; } = string.Empty;

        // Devuelve el formato normalizado sin punto inicial y en minúsculas.
        public string GetNormalizedFormat()
        {
            return Format
                .Trim()
                .TrimStart('.')
                .ToLowerInvariant();
        }

        // Devuelve el hash normalizado en mayúsculas y sin espacios.
        public string GetNormalizedHash()
        {
            return Hash
                .Trim()
                .Replace(" ", string.Empty)
                .ToUpperInvariant();
        }

        // Devuelve el algoritmo normalizado en mayúsculas.
        public string GetNormalizedHashAlgorithm()
        {
            return HashAlgorithm
                .Trim()
                .ToUpperInvariant();
        }
    }
}