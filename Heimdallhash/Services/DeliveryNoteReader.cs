using System.Xml.Linq;
using Heimdallhash.Models;
using Microsoft.Extensions.Logging;

namespace Heimdallhash.Services
{
    /*
     * Lee una Delivery Note desde un fichero XML y la transforma en un modelo DeliveryNote.
     * 
     * Esta clase no valida todavía el contenido real del paquete comprimido.
     * Su responsabilidad es comprobar que el XML existe, que tiene estructura válida
     * y que contiene los campos mínimos esperados.
     */
    public class DeliveryNoteReader
    {
        private readonly ILogger<DeliveryNoteReader> _logger;

        public DeliveryNoteReader(ILogger<DeliveryNoteReader> logger)
        {
            _logger = logger;
        }

        /*
         * Lee una DN desde una ruta XML.
         * 
         * Devuelve un PackageValidationResult porque la lectura de la DN ya puede
         * generar errores de validación, por ejemplo:
         * - XML inexistente.
         * - XML mal formado.
         * - DestinationCenterId vacío.
         * - Ficheros declarados incompletos.
         */
        public PackageValidationResult ReadFromXmlFile(string xmlPath)
        {
            var result = new PackageValidationResult();

            if (string.IsNullOrWhiteSpace(xmlPath))
            {
                result.AddError(
                    "DN_XML_PATH_EMPTY",
                    "La ruta del fichero XML de la Delivery Note está vacía.");

                return result;
            }

            if (!File.Exists(xmlPath))
            {
                result.AddError(
                    "DN_XML_NOT_FOUND",
                    $"No se encontró el fichero XML de la Delivery Note: {xmlPath}");

                return result;
            }

            try
            {
                var document = XDocument.Load(xmlPath);
                var root = document.Root;

                if (root is null)
                {
                    result.AddError(
                        "DN_XML_EMPTY",
                        "El XML de la Delivery Note no contiene un elemento raíz.");

                    return result;
                }

                if (!string.Equals(root.Name.LocalName, "DeliveryNote", StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError(
                        "DN_INVALID_ROOT",
                        $"El elemento raíz del XML debe ser DeliveryNote, pero se encontró: {root.Name.LocalName}");

                    return result;
                }

                var deliveryNote = new DeliveryNote
                {
                    DeliveryNoteFileName = Path.GetFileName(xmlPath),
                    DeliveryNoteTempPath = xmlPath,
                    DestinationCenterId = ObtenerValor(root, "DestinationCenterId")
                };

                ValidarDestinationCenterId(deliveryNote.DestinationCenterId, result);

                LeerFicherosDeclarados(root, deliveryNote, result);

                result.DeliveryNote = deliveryNote;
                result.DestinationCenterId = deliveryNote.DestinationCenterId;

                if (!deliveryNote.HasMinimumData())
                {
                    result.AddError(
                        "DN_MINIMUM_DATA_MISSING",
                        "La Delivery Note no contiene los datos mínimos necesarios para continuar el procesamiento.");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al leer la Delivery Note desde {XmlPath}.",
                    xmlPath);

                result.AddError(
                    "DN_XML_READ_ERROR",
                    $"Error al leer la Delivery Note: {ex.Message}");

                return result;
            }
        }

        /*
         * Lee los ficheros declarados dentro del nodo Files/File.
         */
        private static void LeerFicherosDeclarados(
            XElement root,
            DeliveryNote deliveryNote,
            PackageValidationResult result)
        {
            var filesNode = root.Element("Files");

            if (filesNode is null)
            {
                result.AddError(
                    "DN_FILES_NODE_NOT_FOUND",
                    "La Delivery Note no contiene el nodo Files.");

                return;
            }

            var fileNodes = filesNode.Elements("File").ToList();

            if (fileNodes.Count == 0)
            {
                result.AddError(
                    "DN_NO_FILES_DECLARED",
                    "La Delivery Note no contiene ficheros declarados.");

                return;
            }

            foreach (var fileNode in fileNodes)
            {
                var declaredFile = new DeliveryNoteFile
                {
                    Name = ObtenerValor(fileNode, "Name"),
                    OriginalName = ObtenerValor(fileNode, "OriginalName"),
                    Format = ObtenerValor(fileNode, "Format"),
                    Size = ObtenerLong(fileNode, "Size", result),
                    HashAlgorithm = ObtenerValor(fileNode, "HashAlgorithm"),
                    Hash = ObtenerValor(fileNode, "Hash")
                };

                deliveryNote.Files.Add(declaredFile);

                ValidarFicheroDeclarado(declaredFile, result);
            }
        }

        /*
         * Valida el identificador del centro destino.
         * 
         * Regla actual:
         * - Obligatorio.
         * - Solo numérico.
         * - Entre 4 y 10 caracteres.
         */
        private static void ValidarDestinationCenterId(
            string destinationCenterId,
            PackageValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(destinationCenterId))
            {
                result.AddError(
                    "DN_DESTINATION_CENTER_EMPTY",
                    "La Delivery Note no contiene DestinationCenterId.");

                return;
            }

            if (destinationCenterId.Length < 4 || destinationCenterId.Length > 10)
            {
                result.AddError(
                    "DN_DESTINATION_CENTER_INVALID_LENGTH",
                    "DestinationCenterId debe tener entre 4 y 10 caracteres.");
            }

            if (!destinationCenterId.All(char.IsDigit))
            {
                result.AddError(
                    "DN_DESTINATION_CENTER_NOT_NUMERIC",
                    "DestinationCenterId debe contener únicamente caracteres numéricos.");
            }
        }

        /*
         * Valida que un fichero declarado tenga los campos mínimos esperados.
         */
        private static void ValidarFicheroDeclarado(
            DeliveryNoteFile declaredFile,
            PackageValidationResult result)
        {
            string fileName = string.IsNullOrWhiteSpace(declaredFile.Name)
                ? "(sin nombre)"
                : declaredFile.Name;

            if (string.IsNullOrWhiteSpace(declaredFile.Name))
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "DN_FILE_NAME_EMPTY",
                    "Un fichero declarado en la DN no contiene Name.",
                    fileName,
                    "Name"));
            }

            if (string.IsNullOrWhiteSpace(declaredFile.Format))
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "DN_FILE_FORMAT_EMPTY",
                    "Un fichero declarado en la DN no contiene Format.",
                    fileName,
                    "Format"));
            }

            if (declaredFile.Size < 0)
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "DN_FILE_SIZE_INVALID",
                    "El tamaño declarado del fichero no es válido.",
                    fileName,
                    "Size",
                    "Mayor o igual que 0",
                    declaredFile.Size.ToString()));
            }

            if (string.IsNullOrWhiteSpace(declaredFile.HashAlgorithm))
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "DN_FILE_HASH_ALGORITHM_EMPTY",
                    "Un fichero declarado en la DN no contiene HashAlgorithm.",
                    fileName,
                    "HashAlgorithm"));
            }

            if (string.IsNullOrWhiteSpace(declaredFile.Hash))
            {
                result.AddError(PackageValidationError.CreateForFile(
                    "DN_FILE_HASH_EMPTY",
                    "Un fichero declarado en la DN no contiene Hash.",
                    fileName,
                    "Hash"));
            }
        }

        /*
         * Obtiene el valor de un nodo hijo.
         * Si el nodo no existe, devuelve cadena vacía.
         */
        private static string ObtenerValor(XElement parent, string elementName)
        {
            return parent.Element(elementName)?.Value.Trim() ?? string.Empty;
        }

        /*
         * Obtiene un valor long desde un nodo hijo.
         * Si el valor no puede convertirse, registra un error y devuelve -1.
         */
        private static long ObtenerLong(
            XElement parent,
            string elementName,
            PackageValidationResult result)
        {
            string value = ObtenerValor(parent, elementName);

            if (string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

            if (long.TryParse(value, out long parsedValue))
            {
                return parsedValue;
            }

            result.AddError(
                "DN_NUMERIC_VALUE_INVALID",
                $"El campo {elementName} contiene un valor numérico inválido: {value}");

            return -1;
        }
    }
}