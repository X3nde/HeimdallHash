using Heimdallhash.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Valida la estructura de directorios al arrancar el servicio.
     *
     * El servicio puede crear carpetas internas controladas por HeimdallHash,
     * pero no debe crear rutas funcionales externas de origen o destino.
     */
    public class DirectoryStartupValidator
    {
        private readonly AppSettings _settings;
        private readonly ILogger<DirectoryStartupValidator> _logger;

        public DirectoryStartupValidator(
            IOptions<AppSettings> settings,
            ILogger<DirectoryStartupValidator> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /*
         * Ejecuta la validación de directorios de arranque.
         */
        public Task ValidarAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.DirectoryManagement.ValidateDirectoriesOnStartup)
            {
                _logger.LogInformation(
                    "Validación de directorios al arranque desactivada por configuración.");

                return Task.CompletedTask;
            }

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Iniciando validación de directorios de HeimdallHash.");

            ValidarDirectoriosInternos(cancellationToken);
            ValidarRutasExternasOrigen();
            ValidarRutasExternasDestino();

            _logger.LogInformation(
                "Validación de directorios de HeimdallHash finalizada.");

            return Task.CompletedTask;
        }

        /*
         * Valida y, si está permitido, crea directorios internos controlados
         * por HeimdallHash.
         */
        private void ValidarDirectoriosInternos(CancellationToken cancellationToken)
        {
            bool puedeCrear = _settings.DirectoryManagement.AllowServiceToCreateInternalDirectories;

            ValidarOCrearDirectorioInterno(
                "Storage.TempDirectory",
                _settings.Storage.TempDirectory,
                puedeCrear);

            ValidarOCrearDirectorioInterno(
                "Quarantine.RootDirectory",
                _settings.Quarantine.RootDirectory,
                puedeCrear);

            ValidarOCrearDirectorioInterno(
                "PendingDelivery.RootDirectory",
                _settings.PendingDelivery.RootDirectory,
                puedeCrear);

            ValidarOCrearDirectorioInterno(
                "RecordBooks.RootDirectory",
                _settings.RecordBooks.RootDirectory,
                puedeCrear);

            ValidarOCrearDirectorioInterno(
                "ApplicationLogs.RootDirectory",
                _settings.ApplicationLogs.RootDirectory,
                puedeCrear);

            if (!string.IsNullOrWhiteSpace(_settings.ArchiveProcessing.TemporaryDirectory))
            {
                ValidarOCrearDirectorioInterno(
                    "ArchiveProcessing.TemporaryDirectory",
                    _settings.ArchiveProcessing.TemporaryDirectory,
                    puedeCrear);
            }

            if (!puedeCrear)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            CrearEstructuraInternaCuarentena();
            CrearEstructuraInternaPendienteEntrega();
            CrearEstructuraInternaLibrosRegistro();
            CrearEstructuraInternaLogsAplicacion();
        }

        /*
         * Valida una ruta interna y la crea si está permitido.
         */
        private void ValidarOCrearDirectorioInterno(
            string nombreConfiguracion,
            string ruta,
            bool puedeCrear)
        {
            if (string.IsNullOrWhiteSpace(ruta))
            {
                _logger.LogWarning(
                    "La ruta interna {NombreConfiguracion} no está configurada.",
                    nombreConfiguracion);

                return;
            }

            if (Directory.Exists(ruta))
            {
                _logger.LogDebug(
                    "Directorio interno validado: {NombreConfiguracion} = {Ruta}",
                    nombreConfiguracion,
                    ruta);

                return;
            }

            if (!puedeCrear)
            {
                _logger.LogWarning(
                    "El directorio interno no existe y el servicio no tiene permitido crearlo. {NombreConfiguracion} = {Ruta}",
                    nombreConfiguracion,
                    ruta);

                return;
            }

            try
            {
                Directory.CreateDirectory(ruta);

                _logger.LogInformation(
                    "Directorio interno creado: {NombreConfiguracion} = {Ruta}",
                    nombreConfiguracion,
                    ruta);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo crear el directorio interno {NombreConfiguracion}: {Ruta}",
                    nombreConfiguracion,
                    ruta);
            }
        }

        /*
         * Valida rutas externas de origen.
         *
         * El servicio no crea estas rutas porque son rutas funcionales externas
         * que deben existir previamente o ser creadas por operación/configuración.
         */
        private void ValidarRutasExternasOrigen()
        {
            foreach (var ruta in _settings.WatchRoutes.Where(ruta => ruta.Enabled))
            {
                if (string.IsNullOrWhiteSpace(ruta.InputDirectory))
                {
                    _logger.LogWarning(
                        "La ruta monitorizada {RouteName} está habilitada pero no tiene InputDirectory configurado.",
                        ruta.Name);

                    continue;
                }

                if (!Directory.Exists(ruta.InputDirectory))
                {
                    string severidad = _settings.DirectoryManagement.DisableRouteWhenInputDirectoryIsMissing
                        ? "no será procesable"
                        : "se intentará procesar igualmente";

                    _logger.LogWarning(
                        "La ruta de origen no existe. Route={RouteName}, InputDirectory={InputDirectory}. Según configuración, la ruta {Severidad}.",
                        ruta.Name,
                        ruta.InputDirectory,
                        severidad);

                    continue;
                }

                _logger.LogDebug(
                    "Ruta externa de origen validada: {RouteName} = {InputDirectory}",
                    ruta.Name,
                    ruta.InputDirectory);
            }
        }

        /*
         * Valida rutas externas de destino.
         *
         * El servicio no crea estas rutas. Si no existen, los productos válidos
         * se gestionarán como PendienteEntrega cuando corresponda.
         */
        private void ValidarRutasExternasDestino()
        {
            foreach (var ruta in _settings.CenterRoutes.Where(ruta => ruta.Enabled))
            {
                if (string.IsNullOrWhiteSpace(ruta.DestinationPath))
                {
                    _logger.LogWarning(
                        "La ruta destino del centro {CenterId}/{Flow} está habilitada pero no tiene DestinationPath configurado.",
                        ruta.CenterId,
                        ruta.Flow);

                    continue;
                }

                if (!Directory.Exists(ruta.DestinationPath))
                {
                    string accion = _settings.DirectoryManagement.UsePendingDeliveryWhenDestinationIsMissing
                        ? "los productos válidos se enviarán a PendienteEntrega"
                        : "el fallo se registrará como incidencia de entrega";

                    _logger.LogWarning(
                        "La ruta destino no existe o no está accesible. CenterId={CenterId}, Flow={Flow}, DestinationPath={DestinationPath}. Acción prevista: {Accion}.",
                        ruta.CenterId,
                        ruta.Flow,
                        ruta.DestinationPath,
                        accion);

                    continue;
                }

                _logger.LogDebug(
                    "Ruta externa de destino validada: CenterId={CenterId}, Flow={Flow}, DestinationPath={DestinationPath}",
                    ruta.CenterId,
                    ruta.Flow,
                    ruta.DestinationPath);
            }
        }

        /*
         * Crea estructura mínima de Cuarentena.
         */
        private void CrearEstructuraInternaCuarentena()
        {
            string root = _settings.Quarantine.RootDirectory;

            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            CrearDirectorioInternoControlado(Path.Combine(
                root,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.Quarantine.CentersDirectoryName,
                    "Centros"))));

            CrearDirectorioInternoControlado(Path.Combine(
                root,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.Quarantine.UnresolvedDirectoryName,
                    "SinResolver")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.Quarantine.ProductsDirectoryName,
                    "Productos"))));
        }

        /*
         * Crea estructura mínima de PendienteEntrega.
         */
        private void CrearEstructuraInternaPendienteEntrega()
        {
            string root = _settings.PendingDelivery.RootDirectory;

            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            CrearDirectorioInternoControlado(Path.Combine(
                root,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.PendingDelivery.CentersDirectoryName,
                    "Centros"))));
        }

        /*
         * Crea estructura mínima de LibrosRegistro.
         */
        private void CrearEstructuraInternaLibrosRegistro()
        {
            string root = _settings.RecordBooks.RootDirectory;

            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            CrearDirectorioInternoControlado(Path.Combine(
                root,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.CentersDirectoryName,
                    "Centros"))));

            CrearDirectorioInternoControlado(Path.Combine(
                root,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.UnresolvedDirectoryName,
                    "SinResolver")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.QuarantineDirectoryName,
                    "Cuarentena")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.DailyDirectoryName,
                    "Diarios"))));

            CrearDirectorioInternoControlado(Path.Combine(
                root,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.UnresolvedDirectoryName,
                    "SinResolver")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.NotificationsDirectoryName,
                    "Notificaciones")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.RecordBooks.DailyDirectoryName,
                    "Diarios"))));
        }

        /*
         * Crea estructura mínima de LogsAplicacion.
         */
        private void CrearEstructuraInternaLogsAplicacion()
        {
            string root = _settings.ApplicationLogs.RootDirectory;

            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            CrearDirectorioInternoControlado(Path.Combine(
                root,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.ApplicationLogs.ServiceDirectoryName,
                    "Servicio")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.ApplicationLogs.DailyDirectoryName,
                    "Diarios"))));

            CrearDirectorioInternoControlado(Path.Combine(
                root,
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.ApplicationLogs.ErrorsDirectoryName,
                    "Errores")),
                SanitizarNombreDirectorio(ObtenerValorODefecto(
                    _settings.ApplicationLogs.DailyDirectoryName,
                    "Diarios"))));
        }

        /*
         * Crea un directorio interno controlado ignorando si ya existe.
         */
        private void CrearDirectorioInternoControlado(string ruta)
        {
            try
            {
                Directory.CreateDirectory(ruta);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo crear el directorio interno controlado: {Ruta}",
                    ruta);
            }
        }

        /*
         * Devuelve un valor configurado o un valor por defecto.
         */
        private static string ObtenerValorODefecto(string? value, string valorPorDefecto)
        {
            return string.IsNullOrWhiteSpace(value)
                ? valorPorDefecto
                : value;
        }

        /*
         * Sanitiza nombres de carpeta.
         */
        private static string SanitizarNombreDirectorio(string? value)
        {
            value ??= string.Empty;
            value = value.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                return "SinResolver";
            }

            foreach (char caracterInvalido in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(caracterInvalido, '_');
            }

            return value;
        }
    }
}
