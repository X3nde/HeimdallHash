using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Heimdallhash.Config;
using Heimdallhash.Services;

namespace Heimdallhash
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AppSettings _settings;
        private readonly FileProcessor _fileProcessor;
        private readonly PendingDeliveryService _pendingDeliveryService;
        private readonly MailNotifier _mailNotifier;
        private readonly DirectoryStartupValidator _directoryStartupValidator;
        private readonly ConfigurationSanityValidator _configurationSanityValidator;
        private readonly ServiceCycleLogger _serviceCycleLogger;
        private readonly RecordBookMaintenanceService _recordBookMaintenanceService;

        public Worker(
            ILogger<Worker> logger,
            IOptions<AppSettings> settings,
            FileProcessor fileProcessor,
            PendingDeliveryService pendingDeliveryService,
            MailNotifier mailNotifier,
            DirectoryStartupValidator directoryStartupValidator,
            ConfigurationSanityValidator configurationSanityValidator,
            ServiceCycleLogger serviceCycleLogger,
            RecordBookMaintenanceService recordBookMaintenanceService)
        {
            _logger = logger;
            _settings = settings.Value;
            _fileProcessor = fileProcessor;
            _pendingDeliveryService = pendingDeliveryService;
            _mailNotifier = mailNotifier;
            _directoryStartupValidator = directoryStartupValidator;
            _configurationSanityValidator = configurationSanityValidator;
            _serviceCycleLogger = serviceCycleLogger;
            _recordBookMaintenanceService = recordBookMaintenanceService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string serviceStartCycleId = _serviceCycleLogger.CrearCycleId();

            _logger.LogInformation("HEIMDALLHASH iniciado correctamente. Intervalo: {Interval} segundos", _settings.PollingIntervalSeconds);

            await _serviceCycleLogger.RegistrarEventoAsync(
                serviceStartCycleId,
                "SERVICE_STARTED",
                "OK",
                $"HEIMDALLHASH iniciado correctamente. Intervalo: {_settings.PollingIntervalSeconds} segundos.",
                stoppingToken);

            await _directoryStartupValidator.ValidarAsync(stoppingToken);

            await _serviceCycleLogger.RegistrarEventoAsync(
                serviceStartCycleId,
                "STARTUP_VALIDATION_COMPLETED",
                "OK",
                "Validación de directorios de arranque finalizada.",
                stoppingToken);

            await _configurationSanityValidator.ValidarAsync(stoppingToken);

            await _serviceCycleLogger.RegistrarEventoAsync(
                serviceStartCycleId,
                "CONFIGURATION_VALIDATION_COMPLETED",
                "OK",
                "Validación de coherencia de configuración finalizada.",
                stoppingToken);

            await _recordBookMaintenanceService.EjecutarMantenimientoInicialAsync(stoppingToken);

            await _serviceCycleLogger.RegistrarEventoAsync(
                serviceStartCycleId,
                "RECORD_BOOK_MAINTENANCE_COMPLETED",
                "OK",
                "Mantenimiento inicial de libros de registro finalizado.",
                stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                string cycleId = _serviceCycleLogger.CrearCycleId();

                try
                {
                    _logger.LogInformation("Ciclo iniciado: {time}", DateTimeOffset.Now);

                    await _serviceCycleLogger.RegistrarEventoAsync(
                        cycleId,
                        "CYCLE_STARTED",
                        "OK",
                        "Ciclo de procesamiento iniciado.",
                        stoppingToken);

                    await _fileProcessor.ProcesarDirectorioAsync(stoppingToken);

                    await _pendingDeliveryService.ReintentarPendientesAsync(stoppingToken);

                    await _mailNotifier.EnviarCorreoSiCorresponde();

                    await _serviceCycleLogger.RegistrarEventoAsync(
                        cycleId,
                        "CYCLE_COMPLETED",
                        "OK",
                        "Ciclo de procesamiento finalizado correctamente.",
                        stoppingToken);

                    _logger.LogInformation("Ciclo finalizado: {time}", DateTimeOffset.Now);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Detención normal del servicio.
                }
                catch (Exception ex)
                {
                    await _serviceCycleLogger.RegistrarEventoAsync(
                        cycleId,
                        "CYCLE_FAILED",
                        "ERROR",
                        ex.Message,
                        CancellationToken.None);

                    _logger.LogError(ex, "Error durante el ciclo de procesamiento.");
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_settings.PollingIntervalSeconds), stoppingToken);
                }
            }

            await _serviceCycleLogger.RegistrarEventoAsync(
                _serviceCycleLogger.CrearCycleId(),
                "SERVICE_STOPPED",
                "OK",
                "HEIMDALLHASH detenido.",
                CancellationToken.None);

            _logger.LogInformation("HEIMDALLHASH detenido.");
        }
    }
}
