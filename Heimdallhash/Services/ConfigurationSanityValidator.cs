using Heimdallhash.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Valida coherencia básica de la configuración al arrancar el servicio.
     *
     * No sustituye al configurador gráfico. Su objetivo es detectar errores
     * peligrosos o incoherencias antes de empezar a procesar productos.
     *
     * Esta validación no detiene el servicio por defecto. Registra advertencias
     * y errores en el log técnico para que puedan corregirse.
     */
    public class ConfigurationSanityValidator
    {
        private readonly AppSettings _settings;
        private readonly ILogger<ConfigurationSanityValidator> _logger;

        private static readonly HashSet<string> FlujosPermitidos = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "Upload",
            "Download"
        };

        private static readonly HashSet<string> ModosValidacionPermitidos = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "FileNameHash",
            "DeliveryNote",
            "Auto"
        };

        private static readonly HashSet<string> AlgoritmosHashPermitidos = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "MD5",
            "SHA1",
            "SHA256",
            "SHA384",
            "SHA512"
        };

        public ConfigurationSanityValidator(
            IOptions<AppSettings> settings,
            ILogger<ConfigurationSanityValidator> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /*
         * Ejecuta la validación de coherencia de configuración.
         */
        public Task ValidarAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Iniciando validación de coherencia de configuración.");

            ValidarParametrosGenerales();
            ValidarHash();
            ValidarWatchRoutes();
            ValidarCenterRoutes();
            ValidarProcesamientoArchivos();
            ValidarNotificaciones();
            ValidarPoliticasDirectorios();

            _logger.LogInformation(
                "Validación de coherencia de configuración finalizada.");

            return Task.CompletedTask;
        }

        /*
         * Valida parámetros generales de ejecución.
         */
        private void ValidarParametrosGenerales()
        {
            if (_settings.PollingIntervalSeconds <= 0)
            {
                _logger.LogError(
                    "PollingIntervalSeconds debe ser mayor que 0. Valor actual: {Valor}",
                    _settings.PollingIntervalSeconds);
            }

            if (_settings.ConcurrencyLevel <= 0)
            {
                _logger.LogError(
                    "ConcurrencyLevel debe ser mayor que 0. Valor actual: {Valor}",
                    _settings.ConcurrencyLevel);
            }

            if (_settings.RetryPolicy.MaxAttempts <= 0)
            {
                _logger.LogError(
                    "RetryPolicy.MaxAttempts debe ser mayor que 0. Valor actual: {Valor}",
                    _settings.RetryPolicy.MaxAttempts);
            }

            if (_settings.RetryPolicy.DelayMilliseconds < 0)
            {
                _logger.LogError(
                    "RetryPolicy.DelayMilliseconds no puede ser negativo. Valor actual: {Valor}",
                    _settings.RetryPolicy.DelayMilliseconds);
            }

            if (_settings.StabilityCheck.MinFileAgeSeconds < 0)
            {
                _logger.LogError(
                    "StabilityCheck.MinFileAgeSeconds no puede ser negativo. Valor actual: {Valor}",
                    _settings.StabilityCheck.MinFileAgeSeconds);
            }
        }

        /*
         * Valida configuración de algoritmos hash.
         */
        private void ValidarHash()
        {
            if (string.IsNullOrWhiteSpace(_settings.Hash.DefaultAlgorithm))
            {
                _logger.LogError(
                    "Hash.DefaultAlgorithm no está configurado.");

                return;
            }

            if (!AlgoritmosHashPermitidos.Contains(_settings.Hash.DefaultAlgorithm))
            {
                _logger.LogError(
                    "Hash.DefaultAlgorithm no está soportado: {Algoritmo}",
                    _settings.Hash.DefaultAlgorithm);
            }

            if (_settings.Hash.AllowedAlgorithms.Count == 0)
            {
                _logger.LogError(
                    "Hash.AllowedAlgorithms no puede estar vacío.");
            }

            foreach (string algoritmo in _settings.Hash.AllowedAlgorithms)
            {
                if (!AlgoritmosHashPermitidos.Contains(algoritmo))
                {
                    _logger.LogError(
                        "Hash.AllowedAlgorithms contiene un algoritmo no soportado: {Algoritmo}",
                        algoritmo);
                }
            }

            if (!_settings.Hash.AllowedAlgorithms.Contains(
                    _settings.Hash.DefaultAlgorithm,
                    StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Hash.DefaultAlgorithm no está incluido en Hash.AllowedAlgorithms. Default={DefaultAlgorithm}",
                    _settings.Hash.DefaultAlgorithm);
            }

            if (_settings.Hash.AllowedAlgorithms.Any(algoritmo =>
                    algoritmo.Equals("MD5", StringComparison.OrdinalIgnoreCase) ||
                    algoritmo.Equals("SHA1", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning(
                    "MD5/SHA1 están habilitados por compatibilidad. No se recomienda su uso para nuevos flujos.");
            }
        }

        /*
         * Valida rutas monitorizadas.
         */
        private void ValidarWatchRoutes()
        {
            if (_settings.WatchRoutes.Count == 0)
            {
                _logger.LogWarning(
                    "No hay WatchRoutes configuradas. El servicio no tendrá rutas de entrada que procesar.");

                return;
            }

            var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ruta in _settings.WatchRoutes)
            {
                if (string.IsNullOrWhiteSpace(ruta.Name))
                {
                    _logger.LogWarning(
                        "Existe una WatchRoute sin nombre lógico configurado.");
                }

                if (!FlujosPermitidos.Contains(ruta.Flow))
                {
                    _logger.LogError(
                        "WatchRoute {RouteName} tiene un Flow no permitido: {Flow}",
                        ruta.Name,
                        ruta.Flow);
                }

                if (!ModosValidacionPermitidos.Contains(ruta.ValidationMode))
                {
                    _logger.LogError(
                        "WatchRoute {RouteName} tiene un ValidationMode no permitido: {ValidationMode}",
                        ruta.Name,
                        ruta.ValidationMode);
                }

                if (string.IsNullOrWhiteSpace(ruta.InputDirectory))
                {
                    _logger.LogError(
                        "WatchRoute {RouteName} está configurada sin InputDirectory.",
                        ruta.Name);
                }

                string clave = $"{ruta.Flow}|{ruta.InputDirectory}";

                if (!string.IsNullOrWhiteSpace(ruta.InputDirectory) &&
                    !claves.Add(clave))
                {
                    _logger.LogWarning(
                        "Existen WatchRoutes duplicadas para Flow={Flow} e InputDirectory={InputDirectory}.",
                        ruta.Flow,
                        ruta.InputDirectory);
                }

                if (ruta.ValidationMode.Equals("DeliveryNote", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(ruta.OutputDirectory))
                {
                    _logger.LogWarning(
                        "WatchRoute {RouteName} usa DeliveryNote pero tiene OutputDirectory configurado. En DeliveryNote el destino se resuelve por CenterId + Flow.",
                        ruta.Name);
                }
            }
        }

        /*
         * Valida rutas de destino por centro y flujo.
         */
        private void ValidarCenterRoutes()
        {
            var claves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ruta in _settings.CenterRoutes)
            {
                if (string.IsNullOrWhiteSpace(ruta.CenterId))
                {
                    _logger.LogError(
                        "Existe una CenterRoute sin CenterId.");
                }
                else if (!EsCenterIdValido(ruta.CenterId))
                {
                    _logger.LogError(
                        "CenterRoute tiene CenterId inválido: {CenterId}. Debe ser numérico y tener entre 4 y 10 caracteres.",
                        ruta.CenterId);
                }

                if (!FlujosPermitidos.Contains(ruta.Flow))
                {
                    _logger.LogError(
                        "CenterRoute {CenterId} tiene un Flow no permitido: {Flow}",
                        ruta.CenterId,
                        ruta.Flow);
                }

                if (string.IsNullOrWhiteSpace(ruta.DestinationPath))
                {
                    _logger.LogWarning(
                        "CenterRoute {CenterId}/{Flow} no tiene DestinationPath configurado.",
                        ruta.CenterId,
                        ruta.Flow);
                }

                string clave = $"{ruta.CenterId}|{ruta.Flow}";

                if (!claves.Add(clave))
                {
                    _logger.LogError(
                        "Existen CenterRoutes duplicadas para CenterId={CenterId} y Flow={Flow}.",
                        ruta.CenterId,
                        ruta.Flow);
                }
            }
        }

        /*
         * Valida opciones de procesamiento de archivos comprimidos.
         */
        private void ValidarProcesamientoArchivos()
        {
            if (_settings.ArchiveProcessing.SupportedExtensions.Count == 0)
            {
                _logger.LogError(
                    "ArchiveProcessing.SupportedExtensions no puede estar vacío.");
            }

            foreach (string extension in _settings.ArchiveProcessing.SupportedExtensions)
            {
                if (string.IsNullOrWhiteSpace(extension) ||
                    !extension.StartsWith('.'))
                {
                    _logger.LogError(
                        "Extensión de producto comprimido inválida: {Extension}",
                        extension);
                }
            }

            if (_settings.ArchiveProcessing.EnableSevenZipFallback &&
                string.IsNullOrWhiteSpace(_settings.ArchiveProcessing.SevenZipExecutablePath))
            {
                _logger.LogWarning(
                    "SevenZip fallback está habilitado pero SevenZipExecutablePath está vacío.");
            }

            if (_settings.PendingDelivery.MaxRetryAttempts < 0)
            {
                _logger.LogError(
                    "PendingDelivery.MaxRetryAttempts no puede ser negativo.");
            }

            if (_settings.PendingDelivery.MaxPendingDays < 0)
            {
                _logger.LogError(
                    "PendingDelivery.MaxPendingDays no puede ser negativo.");
            }

            if (_settings.PendingDelivery.RetryEveryCycles <= 0)
            {
                _logger.LogWarning(
                    "PendingDelivery.RetryEveryCycles debería ser mayor que 0. Se aplicará valor efectivo 1.");
            }
        }

        /*
         * Valida configuración de notificaciones.
         */
        private void ValidarNotificaciones()
        {
            if (!_settings.Notifications.Enabled)
            {
                return;
            }

            bool hayEventoActivado =
                _settings.Notifications.NotifyOnQuarantine ||
                _settings.Notifications.NotifyOnPendingCreated ||
                _settings.Notifications.NotifyOnPendingDelivered ||
                _settings.Notifications.NotifyOnPendingStillFailed;

            if (!hayEventoActivado)
            {
                _logger.LogWarning(
                    "Notifications.Enabled está activado pero no hay ningún tipo de notificación habilitado.");
            }

            bool smtpConfigurado =
                !string.IsNullOrWhiteSpace(_settings.Email.SmtpServer) &&
                !string.IsNullOrWhiteSpace(_settings.Email.Sender) &&
                _settings.Email.Recipients.Count > 0;

            if (!smtpConfigurado)
            {
                _logger.LogWarning(
                    "Las notificaciones están habilitadas pero SMTP no tiene configuración mínima completa. Se registrarán como FAILED en el LR de Notificaciones.");
            }

            if (_settings.Notifications.MaxNotificationAttempts < 0)
            {
                _logger.LogError(
                    "Notifications.MaxNotificationAttempts no puede ser negativo.");
            }

            if (_settings.Notifications.NotifyPendingStillFailedAfterCycles < 0)
            {
                _logger.LogError(
                    "Notifications.NotifyPendingStillFailedAfterCycles no puede ser negativo.");
            }

            if (_settings.Email.Port <= 0 || _settings.Email.Port > 65535)
            {
                _logger.LogError(
                    "Email.Port está fuera de rango: {Port}",
                    _settings.Email.Port);
            }
        }

        /*
         * Valida políticas de directorios.
         */
        private void ValidarPoliticasDirectorios()
        {
            if (_settings.DirectoryManagement.AllowServiceToCreateFlowDirectories)
            {
                _logger.LogWarning(
                    "AllowServiceToCreateFlowDirectories está activado. En entornos clasificados se recomienda que el servicio no cree rutas funcionales externas.");
            }

            if (!_settings.DirectoryManagement.UsePendingDeliveryWhenDestinationIsMissing &&
                _settings.PendingDelivery.Enabled)
            {
                _logger.LogWarning(
                    "PendingDelivery está habilitado, pero UsePendingDeliveryWhenDestinationIsMissing está desactivado.");
            }
        }

        /*
         * Valida formato de CenterId.
         */
        private static bool EsCenterIdValido(string centerId)
        {
            if (centerId.Length < 4 || centerId.Length > 10)
            {
                return false;
            }

            return centerId.All(char.IsDigit);
        }
    }
}
