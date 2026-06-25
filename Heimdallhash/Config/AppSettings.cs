using System.Collections.Generic;

namespace Heimdallhash.Config
{
    public class AppSettings
    {
        // Parámetros generales de ejecución del servicio.
        public int PollingIntervalSeconds { get; set; } = 30;
        public int ConcurrencyLevel { get; set; } = 2;

        // Configuración de almacenamiento temporal interno.
        public StorageConfig Storage { get; set; } = new();

        // Configuración general de hash.
        public HashConfig Hash { get; set; } = new();

        // Política general de reintentos para operaciones de fichero.
        public RetryPolicyConfig RetryPolicy { get; set; } = new();

        // Control de estabilidad para evitar procesar ficheros aún en copia.
        public StabilityCheckConfig StabilityCheck { get; set; } = new();

        // Configuración SMTP/Exchange para notificaciones.
        public EmailConfig Email { get; set; } = new();

        // Rutas funcionales externas monitorizadas por la aplicación.
        public List<WatchRouteConfig> WatchRoutes { get; set; } = new();

        // Asociación entre centros, flujos y rutas funcionales de destino.
        public List<CenterRouteConfig> CenterRoutes { get; set; } = new();

        // Configuración de procesamiento de productos comprimidos.
        public ArchiveProcessingConfig ArchiveProcessing { get; set; } = new();

        // Configuración de cuarentena gestionada por HeimdallHash.
        public QuarantineConfig Quarantine { get; set; } = new();

        // Configuración de productos válidos pendientes de entrega.
        public PendingDeliveryConfig PendingDelivery { get; set; } = new();

        // Configuración de libros de registro.
        public RecordBooksConfig RecordBooks { get; set; } = new();

        // Configuración de logs técnicos de aplicación.
        public ApplicationLogsConfig ApplicationLogs { get; set; } = new();

        // Configuración de notificaciones.
        public NotificationConfig Notifications { get; set; } = new();

        // Configuración de validación y creación asistida de directorios.
        public DirectoryManagementConfig DirectoryManagement { get; set; } = new();
    }

    public class StorageConfig
    {
        // Directorio temporal interno usado para extracción y trabajo auxiliar.
        public string TempDirectory { get; set; } = string.Empty;

        // Permite limpiar el contenido temporal tras finalizar el procesamiento.
        public bool CleanTemporaryFilesAfterProcessing { get; set; } = true;
    }

    public class HashConfig
    {
        // Algoritmo por defecto para validación por nombre de fichero cuando se use FileNameHash.
        public string DefaultAlgorithm { get; set; } = "SHA256";

        // Algoritmos permitidos para validar hashes declarados.
        // MD5 y SHA1 se mantienen por compatibilidad operativa.
        public List<string> AllowedAlgorithms { get; set; } = new()
        {
            "MD5",
            "SHA1",
            "SHA256",
            "SHA384",
            "SHA512"
        };
    }

    public class RetryPolicyConfig
    {
        // Número máximo de intentos para operaciones puntuales como copiar, mover o acceder a ficheros.
        public int MaxAttempts { get; set; } = 3;

        // Tiempo de espera entre reintentos, en milisegundos.
        public int DelayMilliseconds { get; set; } = 1000;
    }

    public class StabilityCheckConfig
    {
        // Edad mínima del fichero antes de procesarlo.
        // Evita leer productos que todavía se están copiando a la ruta de entrada.
        public int MinFileAgeSeconds { get; set; } = 30;
    }

    public class EmailConfig
    {
        // Servidor SMTP/Exchange interno.
        public string SmtpServer { get; set; } = string.Empty;

        // Cuenta de correo de la aplicación.
        public string Sender { get; set; } = string.Empty;

        // Destinatarios administradores.
        public List<string> Recipients { get; set; } = new();

        // Asunto base de las notificaciones.
        public string Subject { get; set; } = "HeimdallHash - Notificación";

        // Puerto SMTP. En Exchange interno puede ser 25, 587 u otro según configuración.
        public int Port { get; set; } = 25;

        // Contraseña si se usan credenciales explícitas.
        public string Password { get; set; } = string.Empty;

        // Indica si el servidor SMTP requiere SSL/TLS.
        public bool EnableSsl { get; set; } = false;

        // Indica si se deben usar credenciales explícitas.
        public bool UseCredentials { get; set; } = false;
    }

    public class WatchRouteConfig
    {
        // Nombre lógico de la ruta monitorizada.
        public string Name { get; set; } = string.Empty;

        // Flujo asociado a esta ruta: Upload o Download.
        public string Flow { get; set; } = "Download";

        // Ruta funcional externa de origen. Puede ser local o UNC.
        public string InputDirectory { get; set; } = string.Empty;

        /*
         * Modo de validación:
         * FileNameHash: hash en el nombre del fichero.
         * DeliveryNote: producto comprimido con DN/XML.
         * Auto: detección automática.
         */
        public string ValidationMode { get; set; } = "DeliveryNote";

        // Ruta de salida opcional para compatibilidad con FileNameHash.
        // En DeliveryNote el destino se resuelve mediante CenterId + Flow.
        public string OutputDirectory { get; set; } = string.Empty;

        // Permite activar o desactivar la ruta.
        public bool Enabled { get; set; } = true;

        // Cuarentena específica opcional para esta ruta.
        // Si está vacía, se usará la raíz global de cuarentena.
        public string QuarantineDirectory { get; set; } = string.Empty;

        // Directorio temporal específico opcional para esta ruta.
        // Si está vacío, se usará Storage.TempDirectory.
        public string TempDirectory { get; set; } = string.Empty;
    }

    public class CenterRouteConfig
    {
        // ID numérico del centro, entre 4 y 10 caracteres.
        public string CenterId { get; set; } = string.Empty;

        // Flujo asociado: Upload o Download.
        public string Flow { get; set; } = "Download";

        // Ruta funcional externa donde se entregará el producto validado.
        // Puede ser una ruta local o una ruta UNC.
        public string DestinationPath { get; set; } = string.Empty;

        // Permite deshabilitar temporalmente el destino.
        public bool Enabled { get; set; } = true;

        // Descripción para facilitar su gestión desde la interfaz gráfica.
        public string Description { get; set; } = string.Empty;
    }

    public class ArchiveProcessingConfig
    {
        // Extractor principal: SharpCompress o SevenZip.
        public string ExtractorMode { get; set; } = "SharpCompress";

        // Si SharpCompress falla, permite usar 7z.exe como alternativa.
        public bool EnableSevenZipFallback { get; set; } = true;

        // Ruta al ejecutable 7z.exe si se usa como extractor principal o fallback.
        public string SevenZipExecutablePath { get; set; } = @"C:\Program Files\7-Zip\7z.exe";

        // Extensiones de producto comprimido soportadas.
        public List<string> SupportedExtensions { get; set; } = new()
        {
            ".zip",
            ".7z",
            ".rar"
        };

        // Directorio temporal opcional para extracción.
        // Si está vacío, se usará Storage.TempDirectory.
        public string TemporaryDirectory { get; set; } = string.Empty;

        // Los productos protegidos con contraseña se enviarán a cuarentena.
        public bool RejectPasswordProtectedArchives { get; set; } = true;

        // Limpia los ficheros temporales al finalizar el procesamiento.
        public bool CleanTemporaryFilesAfterProcessing { get; set; } = true;
    }

    public class QuarantineConfig
    {
        // Raíz de cuarentena gestionada por HeimdallHash.
        public string RootDirectory { get; set; } = string.Empty;

        // Nombre de la carpeta que agrupa productos por centro.
        public string CentersDirectoryName { get; set; } = "Centros";

        // Nombre de la carpeta usada cuando no se puede resolver el centro.
        public string UnresolvedDirectoryName { get; set; } = "SinResolver";

        // Nombre de la carpeta donde se almacenan los productos en cuarentena.
        public string ProductsDirectoryName { get; set; } = "Productos";

        // Permite reintentar procesamiento desde la interfaz gráfica.
        public bool AllowManualRetryFromGui { get; set; } = true;

        // No permite liberar manualmente productos que fallaron validaciones.
        public bool AllowManualReleaseWithoutValidation { get; set; } = false;
    }

    public class PendingDeliveryConfig
    {
        // Activa o desactiva la gestión de productos pendientes de entrega.
        public bool Enabled { get; set; } = true;

        // Raíz donde se guardan productos válidos pendientes de entrega.
        public string RootDirectory { get; set; } = string.Empty;

        // Nombre de la carpeta que agrupa pendientes por centro.
        public string CentersDirectoryName { get; set; } = "Centros";

        // Nombre de la carpeta donde se almacenan los productos pendientes.
        public string ProductsDirectoryName { get; set; } = "Productos";

        // Reintento cada X ciclos.
        public int RetryEveryCycles { get; set; } = 1;

        // Número máximo de intentos por producto.
        // Si vale 0, no hay límite automático.
        public int MaxRetryAttempts { get; set; } = 0;

        // Número de ciclos fallidos a partir del cual se puede notificar fallo persistente.
        public int NotifyAfterFailedCycles { get; set; } = 5;

        // Si vale 0, los pendientes no caducan automáticamente.
        public int MaxPendingDays { get; set; } = 0;

        // Por defecto, un producto válido no se mueve a cuarentena por estar pendiente.
        public bool MoveToQuarantineAfterMaxPendingDays { get; set; } = false;

        // Revalida el producto antes de la entrega final.
        public bool RevalidateBeforeFinalDelivery { get; set; } = true;

        // Comprueba que la ruta destino existe y permite escritura antes de mover el producto.
        public bool RequireDestinationWriteProbe { get; set; } = true;
    }

    public class RecordBooksConfig
    {
        // Raíz general de libros de registro.
        public string RootDirectory { get; set; } = string.Empty;

        // Nombres comunes de carpetas.
        public string CentersDirectoryName { get; set; } = "Centros";
        public string UnresolvedDirectoryName { get; set; } = "SinResolver";
        public string DailyDirectoryName { get; set; } = "Diarios";
        public string MonthlyDirectoryName { get; set; } = "Mensuales";
        public string ArchiveDirectoryName { get; set; } = "Archivo";

        // Nombres de libros funcionales.
        public string AcceptedDirectoryName { get; set; } = "Aceptados";
        public string QuarantineDirectoryName { get; set; } = "Cuarentena";
        public string PendingDeliveryDirectoryName { get; set; } = "PendienteEntrega";
        public string NotificationsDirectoryName { get; set; } = "Notificaciones";
        public string ManualActionsDirectoryName { get; set; } = "AccionesManuales";

        // Patrones diarios por centro y flujo.
        public string AcceptedDailyPattern { get; set; } = "yyyyMMdd_LR_productos_aceptados.csv";
        public string QuarantineDailyPattern { get; set; } = "yyyyMMdd_LR_productos_cuarentena.csv";
        public string PendingDeliveryDailyPattern { get; set; } = "yyyyMMdd_LR_productos_pendiente_entrega.csv";
        public string NotificationsDailyPattern { get; set; } = "yyyyMMdd_LR_notificaciones.csv";
        public string ManualActionsDailyPattern { get; set; } = "yyyyMMdd_LR_acciones_manuales.csv";

        // Patrones mensuales por centro y flujo.
        public string AcceptedMonthlyPattern { get; set; } = "yyyyMM_LR_productos_aceptados.csv";
        public string QuarantineMonthlyPattern { get; set; } = "yyyyMM_LR_productos_cuarentena.csv";
        public string PendingDeliveryMonthlyPattern { get; set; } = "yyyyMM_LR_productos_pendiente_entrega.csv";
        public string NotificationsMonthlyPattern { get; set; } = "yyyyMM_LR_notificaciones.csv";
        public string ManualActionsMonthlyPattern { get; set; } = "yyyyMM_LR_acciones_manuales.csv";

        // Patrones para libros sin centro resuelto.
        public string UnresolvedQuarantineDailyPattern { get; set; } = "yyyyMMdd_LR_productos_cuarentena_sinresolver.csv";
        public string UnresolvedQuarantineMonthlyPattern { get; set; } = "yyyyMM_LR_productos_cuarentena_sinresolver.csv";
        public string UnresolvedNotificationsDailyPattern { get; set; } = "yyyyMMdd_LR_notificaciones_sinresolver.csv";
        public string UnresolvedNotificationsMonthlyPattern { get; set; } = "yyyyMM_LR_notificaciones_sinresolver.csv";

        // Separador CSV.
        public string Delimiter { get; set; } = ";";

        // Reintentos de escritura por si el libro está abierto por un usuario.
        public int MaxWriteAttempts { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 500;

        // Consolida libros diarios antiguos al iniciar ciclos.
        public bool MergePreviousDaysOnCycleStart { get; set; } = true;

        // Por defecto, se archivan los diarios consolidados.
        public bool MoveDailyToArchiveAfterMerge { get; set; } = true;
        public bool DeleteDailyAfterMonthlyMerge { get; set; } = false;
    }

    public class ApplicationLogsConfig
    {
        // Raíz general de logs técnicos de aplicación.
        public string RootDirectory { get; set; } = string.Empty;

        // Nombres de carpetas.
        public string ServiceDirectoryName { get; set; } = "Servicio";
        public string ErrorsDirectoryName { get; set; } = "Errores";
        public string DailyDirectoryName { get; set; } = "Diarios";
        public string MonthlyDirectoryName { get; set; } = "Mensuales";
        public string ArchiveDirectoryName { get; set; } = "Archivo";

        // Patrones diarios.
        public string ServiceDailyPattern { get; set; } = "yyyyMMdd_log_servicio.csv";
        public string ErrorsDailyPattern { get; set; } = "yyyyMMdd_log_errores.csv";

        // Patrones mensuales.
        public string ServiceMonthlyPattern { get; set; } = "yyyyMM_log_servicio.csv";
        public string ErrorsMonthlyPattern { get; set; } = "yyyyMM_log_errores.csv";

        // Separador CSV.
        public string Delimiter { get; set; } = ";";

        // Reintentos de escritura por si el fichero está abierto.
        public int MaxWriteAttempts { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 500;
    }

    public class NotificationConfig
    {
        // Activa las notificaciones.
        public bool Enabled { get; set; } = true;

        // Notifica todos los eventos de cuarentena.
        public bool NotifyOnQuarantine { get; set; } = true;

        // Notifica cuando un producto válido no se puede entregar.
        public bool NotifyOnPendingCreated { get; set; } = true;

        // Notifica cuando un producto pendiente se entrega correctamente tras reintento.
        public bool NotifyOnPendingDelivered { get; set; } = true;

        // Notifica cuando un producto pendiente sigue fallando tras varios ciclos.
        public bool NotifyOnPendingStillFailed { get; set; } = false;

        // Umbral de ciclos fallidos para enviar notificación de fallo persistente.
        public int NotifyPendingStillFailedAfterCycles { get; set; } = 5;

        // Evita notificaciones duplicadas para el mismo evento.
        public bool PreventDuplicateNotifications { get; set; } = true;

        // Reintenta notificaciones pendientes.
        public bool RetryPendingNotifications { get; set; } = true;

        // Número máximo de intentos de envío.
        public int MaxNotificationAttempts { get; set; } = 3;

        // Prefijos específicos por tipo de evento.
        public string QuarantineSubjectPrefix { get; set; } = "[HeimdallHash] Producto enviado a cuarentena";
        public string PendingCreatedSubjectPrefix { get; set; } = "[HeimdallHash] Producto pendiente de entrega";
        public string PendingDeliveredSubjectPrefix { get; set; } = "[HeimdallHash] Producto pendiente entregado";
        public string PendingStillFailedSubjectPrefix { get; set; } = "[HeimdallHash] Producto pendiente aún no entregado";
    }

    public class DirectoryManagementConfig
    {
        // El servicio puede validar rutas al arrancar y registrar incidencias.
        public bool ValidateDirectoriesOnStartup { get; set; } = true;

        // El configurador preguntará al usuario antes de crear directorios inexistentes.
        public bool AskUserBeforeCreatingDirectoriesFromConfigurator { get; set; } = true;

        // Permite al configurador crear directorios inexistentes si el usuario acepta.
        public bool AllowConfiguratorToCreateMissingDirectories { get; set; } = true;

        // Permite al servicio crear automáticamente carpetas internas controladas.
        public bool AllowServiceToCreateInternalDirectories { get; set; } = true;

        // Por defecto, el servicio no debe crear rutas funcionales externas sin intervención del usuario.
        public bool AllowServiceToCreateFlowDirectories { get; set; } = false;

        // Si una ruta de entrada no existe, la ruta monitorizada se considera no procesable.
        public bool DisableRouteWhenInputDirectoryIsMissing { get; set; } = true;

        // Si un destino no existe o no es accesible, los productos válidos irán a PendienteEntrega.
        public bool UsePendingDeliveryWhenDestinationIsMissing { get; set; } = true;
    }
}
