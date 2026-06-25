using Heimdallhash.Config;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HeimdallhashConfigurator
{
    public partial class MainWindow : Window
    {
        private const string DpapiPrefix = "dpapi:";
        private AppSettings _settings = new();
        private string _rutaJson = string.Empty;
        private readonly DispatcherTimer _estadoTimer;
        private readonly bool _esAdministrador;

        public MainWindow()
        {
            InitializeComponent();

            _esAdministrador = EsAdministrador();

            InicializarValoresServicio();
            ConfigurarPermisosVisuales();

            _estadoTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };

            _estadoTimer.Tick += (_, _) => MostrarEstadoDelServicio();
            _estadoTimer.Start();

            _rutaJson = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            RutaJsonBox.Text = _rutaJson;

            CargarConfiguracion();
            MostrarEstadoDelServicio();
        }

        private void InicializarValoresServicio()
        {
            ServiceNameBox.Text = "Heimdallhash";
            ServiceDisplayNameBox.Text = "HeimdallHash Service";
            ServiceExecutablePathBox.Text = Path.Combine(AppContext.BaseDirectory, "Heimdallhash.exe");
            ServiceStartTypeCombo.SelectedIndex = 0;
        }

        private void ConfigurarPermisosVisuales()
        {
            AdminTexto.Text = _esAdministrador
                ? "Ejecutando como administrador"
                : "Sin privilegios de administrador: acciones de servicio bloqueadas";

            if (!_esAdministrador)
            {
                InstalarServicioButton.IsEnabled = false;
                DesinstalarServicioButton.IsEnabled = false;
                IniciarServicioButton.IsEnabled = false;
                DetenerServicioButton.IsEnabled = false;
                ReiniciarServicioButton.IsEnabled = false;
            }
        }

        private void CargarConfiguracion()
        {
            try
            {
                if (!File.Exists(_rutaJson))
                {
                    _settings = new AppSettings();
                    NormalizarConfiguracion();
                    VolcarConfiguracionEnPantalla();
                    ActualizarResumen();
                    StatusBarText.Text = "No se encontró appsettings.json. Se ha creado una configuración vacía en memoria.";
                    return;
                }

                string json = File.ReadAllText(_rutaJson, Encoding.UTF8);

                _settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                }) ?? new AppSettings();

                NormalizarConfiguracion();
                VolcarConfiguracionEnPantalla();
                ActualizarResumen();
                ValidarConfiguracion(false);

                StatusBarText.Text = "Configuración cargada correctamente.";
            }
            catch (Exception ex)
            {
                MostrarError("Error al cargar configuración", ex);
            }
        }

        private void VolcarConfiguracionEnPantalla()
        {
            PollingIntervalBox.Text = _settings.PollingIntervalSeconds.ToString();
            ConcurrencyLevelBox.Text = _settings.ConcurrencyLevel.ToString();
            RetryMaxAttemptsBox.Text = _settings.RetryPolicy.MaxAttempts.ToString();
            RetryDelayMillisecondsBox.Text = _settings.RetryPolicy.DelayMilliseconds.ToString();
            MinFileAgeSecondsBox.Text = _settings.StabilityCheck.MinFileAgeSeconds.ToString();

            WatchRoutesGrid.ItemsSource = null;
            WatchRoutesGrid.ItemsSource = _settings.WatchRoutes;

            CenterRoutesGrid.ItemsSource = null;
            CenterRoutesGrid.ItemsSource = _settings.CenterRoutes;

            StorageTempDirectoryBox.Text = _settings.Storage.TempDirectory;
            ArchiveTemporaryDirectoryBox.Text = _settings.ArchiveProcessing.TemporaryDirectory;
            QuarantineRootBox.Text = _settings.Quarantine.RootDirectory;
            PendingRootBox.Text = _settings.PendingDelivery.RootDirectory;
            RecordBooksRootBox.Text = _settings.RecordBooks.RootDirectory;
            ApplicationLogsRootBox.Text = _settings.ApplicationLogs.RootDirectory;

            ValidateDirectoriesOnStartupBox.IsChecked = _settings.DirectoryManagement.ValidateDirectoriesOnStartup;
            AskUserBeforeCreatingDirectoriesFromConfiguratorBox.IsChecked = _settings.DirectoryManagement.AskUserBeforeCreatingDirectoriesFromConfigurator;
            AllowConfiguratorToCreateMissingDirectoriesBox.IsChecked = _settings.DirectoryManagement.AllowConfiguratorToCreateMissingDirectories;
            AllowServiceToCreateInternalDirectoriesBox.IsChecked = _settings.DirectoryManagement.AllowServiceToCreateInternalDirectories;
            AllowServiceToCreateFlowDirectoriesBox.IsChecked = _settings.DirectoryManagement.AllowServiceToCreateFlowDirectories;
            DisableRouteWhenInputDirectoryIsMissingBox.IsChecked = _settings.DirectoryManagement.DisableRouteWhenInputDirectoryIsMissing;
            UsePendingDeliveryWhenDestinationIsMissingBox.IsChecked = _settings.DirectoryManagement.UsePendingDeliveryWhenDestinationIsMissing;

            SeleccionarComboPorTexto(DefaultHashCombo, _settings.Hash.DefaultAlgorithm);
            HashMd5Box.IsChecked = _settings.Hash.AllowedAlgorithms.Contains("MD5", StringComparer.OrdinalIgnoreCase);
            HashSha1Box.IsChecked = _settings.Hash.AllowedAlgorithms.Contains("SHA1", StringComparer.OrdinalIgnoreCase);
            HashSha256Box.IsChecked = _settings.Hash.AllowedAlgorithms.Contains("SHA256", StringComparer.OrdinalIgnoreCase);
            HashSha384Box.IsChecked = _settings.Hash.AllowedAlgorithms.Contains("SHA384", StringComparer.OrdinalIgnoreCase);
            HashSha512Box.IsChecked = _settings.Hash.AllowedAlgorithms.Contains("SHA512", StringComparer.OrdinalIgnoreCase);

            ExtractorModeBox.Text = _settings.ArchiveProcessing.ExtractorMode;
            SevenZipExecutablePathBox.Text = _settings.ArchiveProcessing.SevenZipExecutablePath;
            SupportedExtensionsBox.Text = string.Join(";", _settings.ArchiveProcessing.SupportedExtensions);
            EnableSevenZipFallbackBox.IsChecked = _settings.ArchiveProcessing.EnableSevenZipFallback;
            RejectPasswordProtectedArchivesBox.IsChecked = _settings.ArchiveProcessing.RejectPasswordProtectedArchives;
            CleanArchiveTemporaryFilesBox.IsChecked = _settings.ArchiveProcessing.CleanTemporaryFilesAfterProcessing;
            CleanStorageTemporaryFilesBox.IsChecked = _settings.Storage.CleanTemporaryFilesAfterProcessing;

            QuarantineCentersDirectoryNameBox.Text = _settings.Quarantine.CentersDirectoryName;
            QuarantineUnresolvedDirectoryNameBox.Text = _settings.Quarantine.UnresolvedDirectoryName;
            QuarantineProductsDirectoryNameBox.Text = _settings.Quarantine.ProductsDirectoryName;
            AllowManualRetryFromGuiBox.IsChecked = _settings.Quarantine.AllowManualRetryFromGui;
            AllowManualReleaseWithoutValidationBox.IsChecked = _settings.Quarantine.AllowManualReleaseWithoutValidation;

            PendingEnabledBox.IsChecked = _settings.PendingDelivery.Enabled;
            PendingRetryEveryCyclesBox.Text = _settings.PendingDelivery.RetryEveryCycles.ToString();
            PendingMaxRetryAttemptsBox.Text = _settings.PendingDelivery.MaxRetryAttempts.ToString();
            PendingNotifyAfterFailedCyclesBox.Text = _settings.PendingDelivery.NotifyAfterFailedCycles.ToString();
            PendingMaxPendingDaysBox.Text = _settings.PendingDelivery.MaxPendingDays.ToString();
            MoveToQuarantineAfterMaxPendingDaysBox.IsChecked = _settings.PendingDelivery.MoveToQuarantineAfterMaxPendingDays;
            RevalidateBeforeFinalDeliveryBox.IsChecked = _settings.PendingDelivery.RevalidateBeforeFinalDelivery;
            RequireDestinationWriteProbeBox.IsChecked = _settings.PendingDelivery.RequireDestinationWriteProbe;
            PendingCentersDirectoryNameBox.Text = _settings.PendingDelivery.CentersDirectoryName;
            PendingProductsDirectoryNameBox.Text = _settings.PendingDelivery.ProductsDirectoryName;

            RecordBooksDelimiterBox.Text = _settings.RecordBooks.Delimiter;
            ApplicationLogsDelimiterBox.Text = _settings.ApplicationLogs.Delimiter;
            RecordBooksMaxWriteAttemptsBox.Text = _settings.RecordBooks.MaxWriteAttempts.ToString();
            ApplicationLogsMaxWriteAttemptsBox.Text = _settings.ApplicationLogs.MaxWriteAttempts.ToString();
            MergePreviousDaysOnCycleStartBox.IsChecked = _settings.RecordBooks.MergePreviousDaysOnCycleStart;
            MoveDailyToArchiveAfterMergeBox.IsChecked = _settings.RecordBooks.MoveDailyToArchiveAfterMerge;
            DeleteDailyAfterMonthlyMergeBox.IsChecked = _settings.RecordBooks.DeleteDailyAfterMonthlyMerge;
            AcceptedDailyPatternBox.Text = _settings.RecordBooks.AcceptedDailyPattern;
            QuarantineDailyPatternBox.Text = _settings.RecordBooks.QuarantineDailyPattern;
            PendingDeliveryDailyPatternBox.Text = _settings.RecordBooks.PendingDeliveryDailyPattern;
            NotificationsDailyPatternBox.Text = _settings.RecordBooks.NotificationsDailyPattern;
            ManualActionsDailyPatternBox.Text = _settings.RecordBooks.ManualActionsDailyPattern;

            SmtpServerBox.Text = _settings.Email.SmtpServer;
            SmtpPortBox.Text = _settings.Email.Port.ToString();
            SenderBox.Text = _settings.Email.Sender;
            EmailSubjectBox.Text = _settings.Email.Subject;
            RecipientsBox.Text = string.Join(";", _settings.Email.Recipients);
            EmailEnableSslBox.IsChecked = _settings.Email.EnableSsl;
            EmailUseCredentialsBox.IsChecked = _settings.Email.UseCredentials;
            EmailPasswordBox.Password = string.Empty;
            UpdatePasswordBox.IsChecked = false;

            NotificationsEnabledBox.IsChecked = _settings.Notifications.Enabled;
            NotifyOnQuarantineBox.IsChecked = _settings.Notifications.NotifyOnQuarantine;
            NotifyOnPendingCreatedBox.IsChecked = _settings.Notifications.NotifyOnPendingCreated;
            NotifyOnPendingDeliveredBox.IsChecked = _settings.Notifications.NotifyOnPendingDelivered;
            NotifyOnPendingStillFailedBox.IsChecked = _settings.Notifications.NotifyOnPendingStillFailed;
            PreventDuplicateNotificationsBox.IsChecked = _settings.Notifications.PreventDuplicateNotifications;
            RetryPendingNotificationsBox.IsChecked = _settings.Notifications.RetryPendingNotifications;
            MaxNotificationAttemptsBox.Text = _settings.Notifications.MaxNotificationAttempts.ToString();
            NotifyPendingStillFailedAfterCyclesBox.Text = _settings.Notifications.NotifyPendingStillFailedAfterCycles.ToString();
            QuarantineSubjectPrefixBox.Text = _settings.Notifications.QuarantineSubjectPrefix;
            PendingCreatedSubjectPrefixBox.Text = _settings.Notifications.PendingCreatedSubjectPrefix;
            PendingDeliveredSubjectPrefixBox.Text = _settings.Notifications.PendingDeliveredSubjectPrefix;
        }

        private void ActualizarConfiguracionDesdePantalla()
        {
            _settings.PollingIntervalSeconds = LeerEntero(PollingIntervalBox, 30);
            _settings.ConcurrencyLevel = LeerEntero(ConcurrencyLevelBox, 2);
            _settings.RetryPolicy.MaxAttempts = LeerEntero(RetryMaxAttemptsBox, 3);
            _settings.RetryPolicy.DelayMilliseconds = LeerEntero(RetryDelayMillisecondsBox, 1000);
            _settings.StabilityCheck.MinFileAgeSeconds = LeerEntero(MinFileAgeSecondsBox, 30);

            WatchRoutesGrid.CommitEdit();
            WatchRoutesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            CenterRoutesGrid.CommitEdit();
            CenterRoutesGrid.CommitEdit(DataGridEditingUnit.Row, true);

            _settings.Storage.TempDirectory = LeerTexto(StorageTempDirectoryBox);
            _settings.ArchiveProcessing.TemporaryDirectory = LeerTexto(ArchiveTemporaryDirectoryBox);
            _settings.Quarantine.RootDirectory = LeerTexto(QuarantineRootBox);
            _settings.PendingDelivery.RootDirectory = LeerTexto(PendingRootBox);
            _settings.RecordBooks.RootDirectory = LeerTexto(RecordBooksRootBox);
            _settings.ApplicationLogs.RootDirectory = LeerTexto(ApplicationLogsRootBox);

            _settings.DirectoryManagement.ValidateDirectoriesOnStartup = EstaMarcado(ValidateDirectoriesOnStartupBox);
            _settings.DirectoryManagement.AskUserBeforeCreatingDirectoriesFromConfigurator = EstaMarcado(AskUserBeforeCreatingDirectoriesFromConfiguratorBox);
            _settings.DirectoryManagement.AllowConfiguratorToCreateMissingDirectories = EstaMarcado(AllowConfiguratorToCreateMissingDirectoriesBox);
            _settings.DirectoryManagement.AllowServiceToCreateInternalDirectories = EstaMarcado(AllowServiceToCreateInternalDirectoriesBox);
            _settings.DirectoryManagement.AllowServiceToCreateFlowDirectories = EstaMarcado(AllowServiceToCreateFlowDirectoriesBox);
            _settings.DirectoryManagement.DisableRouteWhenInputDirectoryIsMissing = EstaMarcado(DisableRouteWhenInputDirectoryIsMissingBox);
            _settings.DirectoryManagement.UsePendingDeliveryWhenDestinationIsMissing = EstaMarcado(UsePendingDeliveryWhenDestinationIsMissingBox);

            _settings.Hash.DefaultAlgorithm = ObtenerTextoCombo(DefaultHashCombo, "SHA256");
            _settings.Hash.AllowedAlgorithms = new List<string>();
            AgregarAlgoritmoSiMarcado(HashMd5Box, "MD5");
            AgregarAlgoritmoSiMarcado(HashSha1Box, "SHA1");
            AgregarAlgoritmoSiMarcado(HashSha256Box, "SHA256");
            AgregarAlgoritmoSiMarcado(HashSha384Box, "SHA384");
            AgregarAlgoritmoSiMarcado(HashSha512Box, "SHA512");

            _settings.ArchiveProcessing.ExtractorMode = LeerTexto(ExtractorModeBox);
            _settings.ArchiveProcessing.SevenZipExecutablePath = LeerTexto(SevenZipExecutablePathBox);
            _settings.ArchiveProcessing.SupportedExtensions = SepararLista(SupportedExtensionsBox.Text);
            _settings.ArchiveProcessing.EnableSevenZipFallback = EstaMarcado(EnableSevenZipFallbackBox);
            _settings.ArchiveProcessing.RejectPasswordProtectedArchives = EstaMarcado(RejectPasswordProtectedArchivesBox);
            _settings.ArchiveProcessing.CleanTemporaryFilesAfterProcessing = EstaMarcado(CleanArchiveTemporaryFilesBox);
            _settings.Storage.CleanTemporaryFilesAfterProcessing = EstaMarcado(CleanStorageTemporaryFilesBox);

            _settings.Quarantine.CentersDirectoryName = LeerTexto(QuarantineCentersDirectoryNameBox);
            _settings.Quarantine.UnresolvedDirectoryName = LeerTexto(QuarantineUnresolvedDirectoryNameBox);
            _settings.Quarantine.ProductsDirectoryName = LeerTexto(QuarantineProductsDirectoryNameBox);
            _settings.Quarantine.AllowManualRetryFromGui = EstaMarcado(AllowManualRetryFromGuiBox);
            _settings.Quarantine.AllowManualReleaseWithoutValidation = EstaMarcado(AllowManualReleaseWithoutValidationBox);

            _settings.PendingDelivery.Enabled = EstaMarcado(PendingEnabledBox);
            _settings.PendingDelivery.RetryEveryCycles = LeerEntero(PendingRetryEveryCyclesBox, 1);
            _settings.PendingDelivery.MaxRetryAttempts = LeerEntero(PendingMaxRetryAttemptsBox, 0);
            _settings.PendingDelivery.NotifyAfterFailedCycles = LeerEntero(PendingNotifyAfterFailedCyclesBox, 5);
            _settings.PendingDelivery.MaxPendingDays = LeerEntero(PendingMaxPendingDaysBox, 0);
            _settings.PendingDelivery.MoveToQuarantineAfterMaxPendingDays = EstaMarcado(MoveToQuarantineAfterMaxPendingDaysBox);
            _settings.PendingDelivery.RevalidateBeforeFinalDelivery = EstaMarcado(RevalidateBeforeFinalDeliveryBox);
            _settings.PendingDelivery.RequireDestinationWriteProbe = EstaMarcado(RequireDestinationWriteProbeBox);
            _settings.PendingDelivery.CentersDirectoryName = LeerTexto(PendingCentersDirectoryNameBox);
            _settings.PendingDelivery.ProductsDirectoryName = LeerTexto(PendingProductsDirectoryNameBox);

            _settings.RecordBooks.Delimiter = LeerTexto(RecordBooksDelimiterBox);
            _settings.ApplicationLogs.Delimiter = LeerTexto(ApplicationLogsDelimiterBox);
            _settings.RecordBooks.MaxWriteAttempts = LeerEntero(RecordBooksMaxWriteAttemptsBox, 3);
            _settings.ApplicationLogs.MaxWriteAttempts = LeerEntero(ApplicationLogsMaxWriteAttemptsBox, 3);
            _settings.RecordBooks.MergePreviousDaysOnCycleStart = EstaMarcado(MergePreviousDaysOnCycleStartBox);
            _settings.RecordBooks.MoveDailyToArchiveAfterMerge = EstaMarcado(MoveDailyToArchiveAfterMergeBox);
            _settings.RecordBooks.DeleteDailyAfterMonthlyMerge = EstaMarcado(DeleteDailyAfterMonthlyMergeBox);
            _settings.RecordBooks.AcceptedDailyPattern = LeerTexto(AcceptedDailyPatternBox);
            _settings.RecordBooks.QuarantineDailyPattern = LeerTexto(QuarantineDailyPatternBox);
            _settings.RecordBooks.PendingDeliveryDailyPattern = LeerTexto(PendingDeliveryDailyPatternBox);
            _settings.RecordBooks.NotificationsDailyPattern = LeerTexto(NotificationsDailyPatternBox);
            _settings.RecordBooks.ManualActionsDailyPattern = LeerTexto(ManualActionsDailyPatternBox);

            _settings.Email.SmtpServer = LeerTexto(SmtpServerBox);
            _settings.Email.Port = LeerEntero(SmtpPortBox, 25);
            _settings.Email.Sender = LeerTexto(SenderBox);
            _settings.Email.Subject = LeerTexto(EmailSubjectBox);
            _settings.Email.Recipients = SepararLista(RecipientsBox.Text);
            _settings.Email.EnableSsl = EstaMarcado(EmailEnableSslBox);
            _settings.Email.UseCredentials = EstaMarcado(EmailUseCredentialsBox);

            if (EstaMarcado(UpdatePasswordBox))
            {
                _settings.Email.Password = string.IsNullOrEmpty(EmailPasswordBox.Password)
                    ? string.Empty
                    : DpapiPrefix + ProtegerDpapiLocalMachine(EmailPasswordBox.Password);
            }

            _settings.Notifications.Enabled = EstaMarcado(NotificationsEnabledBox);
            _settings.Notifications.NotifyOnQuarantine = EstaMarcado(NotifyOnQuarantineBox);
            _settings.Notifications.NotifyOnPendingCreated = EstaMarcado(NotifyOnPendingCreatedBox);
            _settings.Notifications.NotifyOnPendingDelivered = EstaMarcado(NotifyOnPendingDeliveredBox);
            _settings.Notifications.NotifyOnPendingStillFailed = EstaMarcado(NotifyOnPendingStillFailedBox);
            _settings.Notifications.PreventDuplicateNotifications = EstaMarcado(PreventDuplicateNotificationsBox);
            _settings.Notifications.RetryPendingNotifications = EstaMarcado(RetryPendingNotificationsBox);
            _settings.Notifications.MaxNotificationAttempts = LeerEntero(MaxNotificationAttemptsBox, 3);
            _settings.Notifications.NotifyPendingStillFailedAfterCycles = LeerEntero(NotifyPendingStillFailedAfterCyclesBox, 5);
            _settings.Notifications.QuarantineSubjectPrefix = LeerTexto(QuarantineSubjectPrefixBox);
            _settings.Notifications.PendingCreatedSubjectPrefix = LeerTexto(PendingCreatedSubjectPrefixBox);
            _settings.Notifications.PendingDeliveredSubjectPrefix = LeerTexto(PendingDeliveredSubjectPrefixBox);

            NormalizarConfiguracion();
            ActualizarResumen();
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActualizarConfiguracionDesdePantalla();

                var validaciones = ValidarConfiguracion(true);
                if (validaciones.Any(v => v.Nivel == "ERROR"))
                {
                    MessageBox.Show(
                        "La configuración contiene errores bloqueantes. Revise la pestaña 'Validar configuración' antes de guardar.",
                        "Validación de configuración",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    PestanasConfiguracion.SelectedIndex = PestanasConfiguracion.Items.Count - 1;
                    return;
                }

                var opciones = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(_settings, opciones);
                File.WriteAllText(_rutaJson, json, new UTF8Encoding(false));

                StatusBarText.Text = "Configuración guardada correctamente.";
                MessageBox.Show("Configuración guardada correctamente.", "HeimdallHash", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MostrarError("Error al guardar configuración", ex);
            }
        }

        private void CargarJson_Click(object sender, RoutedEventArgs e)
        {
            var dialogo = new OpenFileDialog
            {
                Title = "Seleccionar appsettings.json",
                Filter = "Configuración JSON (*.json)|*.json|Todos los archivos (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialogo.ShowDialog(this) == true)
            {
                _rutaJson = dialogo.FileName;
                RutaJsonBox.Text = _rutaJson;
                CargarConfiguracion();
            }
        }

        private void Validar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActualizarConfiguracionDesdePantalla();
                ValidarConfiguracion(true);
                PestanasConfiguracion.SelectedIndex = PestanasConfiguracion.Items.Count - 1;
            }
            catch (Exception ex)
            {
                MostrarError("Error al validar configuración", ex);
            }
        }

        private List<ValidacionItem> ValidarConfiguracion(bool mostrarResultado)
        {
            var resultado = new List<ValidacionItem>();

            void Error(string mensaje) => resultado.Add(new ValidacionItem("ERROR", mensaje));
            void Advertencia(string mensaje) => resultado.Add(new ValidacionItem("ADVERTENCIA", mensaje));
            void Info(string mensaje) => resultado.Add(new ValidacionItem("INFO", mensaje));

            if (_settings.PollingIntervalSeconds <= 0)
            {
                Error("El intervalo de ciclo debe ser mayor que 0.");
            }

            if (_settings.ConcurrencyLevel <= 0)
            {
                Error("La concurrencia debe ser mayor que 0.");
            }

            if (_settings.RetryPolicy.MaxAttempts <= 0)
            {
                Error("La política de reintentos debe tener al menos un intento.");
            }

            if (_settings.Hash.AllowedAlgorithms.Count == 0)
            {
                Error("Debe existir al menos un algoritmo hash permitido.");
            }

            if (!_settings.Hash.AllowedAlgorithms.Contains(_settings.Hash.DefaultAlgorithm, StringComparer.OrdinalIgnoreCase))
            {
                Advertencia("El algoritmo hash por defecto no está dentro de los algoritmos permitidos.");
            }

            if (_settings.Hash.AllowedAlgorithms.Any(a => a.Equals("MD5", StringComparison.OrdinalIgnoreCase) || a.Equals("SHA1", StringComparison.OrdinalIgnoreCase)))
            {
                Advertencia("MD5/SHA1 están habilitados por compatibilidad. No se recomiendan para nuevos flujos.");
            }

            if (_settings.WatchRoutes.Count == 0)
            {
                Error("Debe configurarse al menos una ruta de entrada.");
            }

            foreach (var ruta in _settings.WatchRoutes)
            {
                if (string.IsNullOrWhiteSpace(ruta.Name))
                {
                    Advertencia("Existe una ruta de entrada sin nombre lógico.");
                }

                if (!EsFlujoValido(ruta.Flow))
                {
                    Error($"La ruta '{ruta.Name}' tiene un flujo no válido: {ruta.Flow}");
                }

                if (!EsModoValidacionValido(ruta.ValidationMode))
                {
                    Error($"La ruta '{ruta.Name}' tiene un modo de validación no válido: {ruta.ValidationMode}");
                }

                if (string.IsNullOrWhiteSpace(ruta.InputDirectory))
                {
                    Error($"La ruta '{ruta.Name}' no tiene directorio de entrada.");
                }
                else if (!Directory.Exists(ruta.InputDirectory))
                {
                    Advertencia($"El directorio de entrada no existe: {ruta.InputDirectory}");
                }
            }

            var watchDuplicadas = _settings.WatchRoutes
                .GroupBy(r => $"{r.Flow}|{r.InputDirectory}", StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);

            foreach (var grupo in watchDuplicadas)
            {
                Advertencia($"Existen rutas de entrada duplicadas para {grupo.Key}.");
            }

            foreach (var centro in _settings.CenterRoutes)
            {
                if (!EsCenterIdValido(centro.CenterId))
                {
                    Error($"CenterId inválido: {centro.CenterId}. Debe ser numérico y tener entre 4 y 10 caracteres.");
                }

                if (!EsFlujoValido(centro.Flow))
                {
                    Error($"El centro {centro.CenterId} tiene un flujo no válido: {centro.Flow}");
                }

                if (string.IsNullOrWhiteSpace(centro.DestinationPath))
                {
                    Advertencia($"El centro {centro.CenterId}/{centro.Flow} no tiene ruta destino.");
                }
                else if (!Directory.Exists(centro.DestinationPath))
                {
                    Advertencia($"La ruta destino no existe o no es accesible: {centro.DestinationPath}");
                }
            }

            var centrosDuplicados = _settings.CenterRoutes
                .GroupBy(c => $"{c.CenterId}|{c.Flow}", StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);

            foreach (var grupo in centrosDuplicados)
            {
                Error($"Existen CenterRoutes duplicadas para {grupo.Key}.");
            }

            if (_settings.PendingDelivery.Enabled && string.IsNullOrWhiteSpace(_settings.PendingDelivery.RootDirectory))
            {
                Error("PendienteEntrega está habilitado pero no tiene directorio raíz.");
            }

            if (_settings.Notifications.Enabled)
            {
                bool smtpMinimo = !string.IsNullOrWhiteSpace(_settings.Email.SmtpServer) &&
                                  !string.IsNullOrWhiteSpace(_settings.Email.Sender) &&
                                  _settings.Email.Recipients.Count > 0;

                if (!smtpMinimo)
                {
                    Advertencia("Las notificaciones están habilitadas, pero la configuración SMTP mínima está incompleta.");
                }

                if (_settings.Email.Port <= 0 || _settings.Email.Port > 65535)
                {
                    Error("El puerto SMTP está fuera de rango.");
                }
            }

            if (_settings.Quarantine.AllowManualReleaseWithoutValidation)
            {
                Advertencia("Está activada la liberación manual sin validación. No se recomienda en entornos controlados.");
            }

            if (_settings.DirectoryManagement.AllowServiceToCreateFlowDirectories)
            {
                Advertencia("El servicio está autorizado a crear rutas funcionales externas. Revisar esta política.");
            }

            if (!resultado.Any(v => v.Nivel == "ERROR" || v.Nivel == "ADVERTENCIA"))
            {
                Info("Configuración válida sin advertencias relevantes.");
            }

            ValidacionGrid.ItemsSource = null;
            ValidacionGrid.ItemsSource = resultado;

            if (mostrarResultado)
            {
                int errores = resultado.Count(v => v.Nivel == "ERROR");
                int advertencias = resultado.Count(v => v.Nivel == "ADVERTENCIA");

                StatusBarText.Text = $"Validación finalizada. Errores: {errores}. Advertencias: {advertencias}.";
            }

            return resultado;
        }

        private void CrearDirectorios_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActualizarConfiguracionDesdePantalla();

                if (!_settings.DirectoryManagement.AllowConfiguratorToCreateMissingDirectories)
                {
                    MessageBox.Show(
                        "La política actual no permite crear directorios desde el configurador.",
                        "Crear directorios",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var rutas = new[]
                {
                    _settings.Storage.TempDirectory,
                    _settings.ArchiveProcessing.TemporaryDirectory,
                    _settings.Quarantine.RootDirectory,
                    _settings.PendingDelivery.RootDirectory,
                    _settings.RecordBooks.RootDirectory,
                    _settings.ApplicationLogs.RootDirectory
                }
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

                if (_settings.DirectoryManagement.AskUserBeforeCreatingDirectoriesFromConfigurator)
                {
                    var respuesta = MessageBox.Show(
                        $"Se crearán o validarán {rutas.Count} directorios internos. ¿Desea continuar?",
                        "Crear directorios internos",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (respuesta != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                foreach (string ruta in rutas)
                {
                    Directory.CreateDirectory(ruta);
                }

                StatusBarText.Text = "Directorios internos creados o verificados correctamente.";
                MessageBox.Show("Directorios internos creados o verificados correctamente.", "HeimdallHash", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MostrarError("Error al crear directorios", ex);
            }
        }

        private void AnadirWatchRoute_Click(object sender, RoutedEventArgs e)
        {
            _settings.WatchRoutes.Add(new WatchRouteConfig
            {
                Name = "NuevaRuta",
                Flow = "Download",
                ValidationMode = "DeliveryNote",
                Enabled = true
            });

            RefrescarTablas();
        }

        private void EliminarWatchRoute_Click(object sender, RoutedEventArgs e)
        {
            if (WatchRoutesGrid.SelectedItem is WatchRouteConfig ruta)
            {
                _settings.WatchRoutes.Remove(ruta);
                RefrescarTablas();
            }
        }

        private void AnadirCenterRoute_Click(object sender, RoutedEventArgs e)
        {
            _settings.CenterRoutes.Add(new CenterRouteConfig
            {
                CenterId = "1234",
                Flow = "Download",
                Enabled = true,
                Description = "Nuevo destino"
            });

            RefrescarTablas();
        }

        private void EliminarCenterRoute_Click(object sender, RoutedEventArgs e)
        {
            if (CenterRoutesGrid.SelectedItem is CenterRouteConfig ruta)
            {
                _settings.CenterRoutes.Remove(ruta);
                RefrescarTablas();
            }
        }

        private void RefrescarTablas()
        {
            WatchRoutesGrid.ItemsSource = null;
            WatchRoutesGrid.ItemsSource = _settings.WatchRoutes;

            CenterRoutesGrid.ItemsSource = null;
            CenterRoutesGrid.ItemsSource = _settings.CenterRoutes;

            ActualizarResumen();
        }

        private void SeleccionarDirectorio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button boton || boton.Tag is not string nombreTextBox)
            {
                return;
            }

            if (FindName(nombreTextBox) is not TextBox caja)
            {
                return;
            }

            var dialogo = new OpenFolderDialog
            {
                Title = "Seleccionar directorio",
                Multiselect = false
            };

            if (Directory.Exists(caja.Text))
            {
                dialogo.InitialDirectory = caja.Text;
            }

            if (dialogo.ShowDialog(this) == true)
            {
                caja.Text = dialogo.FolderName;
            }
        }

        private void SeleccionarSevenZip_Click(object sender, RoutedEventArgs e)
        {
            SeleccionarArchivoEnTextBox(SevenZipExecutablePathBox, "Seleccionar 7z.exe", "7-Zip (7z.exe)|7z.exe|Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*");
        }

        private void SeleccionarEjecutableServicio_Click(object sender, RoutedEventArgs e)
        {
            SeleccionarArchivoEnTextBox(ServiceExecutablePathBox, "Seleccionar ejecutable del servicio", "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*");
        }

        private void SeleccionarArchivoEnTextBox(TextBox caja, string titulo, string filtro)
        {
            var dialogo = new OpenFileDialog
            {
                Title = titulo,
                Filter = filtro,
                CheckFileExists = true
            };

            if (File.Exists(caja.Text))
            {
                dialogo.InitialDirectory = Path.GetDirectoryName(caja.Text);
                dialogo.FileName = Path.GetFileName(caja.Text);
            }

            if (dialogo.ShowDialog(this) == true)
            {
                caja.Text = dialogo.FileName;
            }
        }

        private void InstalarServicio_Click(object sender, RoutedEventArgs e)
        {
            EjecutarAccionServicio("instalar", () =>
            {
                string nombre = LeerTexto(ServiceNameBox);
                string visible = LeerTexto(ServiceDisplayNameBox);
                string ejecutable = LeerTexto(ServiceExecutablePathBox);
                string inicio = ObtenerTextoCombo(ServiceStartTypeCombo, "Automático").Equals("Automático", StringComparison.OrdinalIgnoreCase)
                    ? "auto"
                    : "demand";

                if (string.IsNullOrWhiteSpace(nombre))
                {
                    throw new InvalidOperationException("Debe indicar el nombre del servicio.");
                }

                if (!File.Exists(ejecutable))
                {
                    throw new FileNotFoundException("No se encuentra el ejecutable del servicio.", ejecutable);
                }

                var argumentos = new List<string>
                {
                    "create",
                    nombre,
                    "binPath=",
                    $"\"{ejecutable}\"",
                    "start=",
                    inicio,
                    "DisplayName=",
                    string.IsNullOrWhiteSpace(visible) ? nombre : visible
                };

                EjecutarProceso("sc.exe", argumentos);
            });
        }

        private void DesinstalarServicio_Click(object sender, RoutedEventArgs e)
        {
            EjecutarAccionServicio("desinstalar", () =>
            {
                string nombre = LeerTexto(ServiceNameBox);

                if (ServicioExiste(nombre))
                {
                    try
                    {
                        using var servicio = new ServiceController(nombre);
                        if (servicio.Status != ServiceControllerStatus.Stopped)
                        {
                            servicio.Stop();
                            servicio.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                    }
                    catch
                    {
                        // Si no se puede parar, se intenta borrar igualmente para mostrar el error real de sc.exe.
                    }
                }

                EjecutarProceso("sc.exe", new[] { "delete", nombre });
            });
        }

        private void IniciarServicio_Click(object sender, RoutedEventArgs e)
        {
            EjecutarAccionServicio("iniciar", () =>
            {
                using var servicio = new ServiceController(LeerTexto(ServiceNameBox));
                servicio.Start();
                servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            });
        }

        private void DetenerServicio_Click(object sender, RoutedEventArgs e)
        {
            EjecutarAccionServicio("detener", () =>
            {
                using var servicio = new ServiceController(LeerTexto(ServiceNameBox));
                servicio.Stop();
                servicio.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            });
        }

        private void ReiniciarServicio_Click(object sender, RoutedEventArgs e)
        {
            EjecutarAccionServicio("reiniciar", () =>
            {
                using var servicio = new ServiceController(LeerTexto(ServiceNameBox));
                if (servicio.Status != ServiceControllerStatus.Stopped)
                {
                    servicio.Stop();
                    servicio.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }

                servicio.Start();
                servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            });
        }

        private void ActualizarEstado_Click(object sender, RoutedEventArgs e)
        {
            MostrarEstadoDelServicio();
        }

        private void EjecutarAccionServicio(string accion, Action accionServicio)
        {
            try
            {
                if (!_esAdministrador)
                {
                    MessageBox.Show(
                        "Esta acción requiere ejecutar el configurador como administrador.",
                        "Permisos insuficientes",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                accionServicio();
                MostrarEstadoDelServicio();
                StatusBarText.Text = $"Acción de servicio completada: {accion}.";
            }
            catch (Exception ex)
            {
                MostrarError($"Error al {accion} el servicio", ex);
            }
        }

        private void MostrarEstadoDelServicio()
        {
            try
            {
                string nombre = string.IsNullOrWhiteSpace(ServiceNameBox?.Text)
                    ? "Heimdallhash"
                    : ServiceNameBox.Text.Trim();

                if (!ServicioExiste(nombre))
                {
                    EstadoTexto.Text = $"Servicio {nombre}: no instalado";
                    EstadoIcono.Fill = Brushes.Gray;
                    return;
                }

                using var servicio = new ServiceController(nombre);
                EstadoTexto.Text = $"Servicio {nombre}: {TraducirEstado(servicio.Status)}";
                EstadoIcono.Fill = servicio.Status == ServiceControllerStatus.Running
                    ? Brushes.LimeGreen
                    : Brushes.Orange;
            }
            catch
            {
                EstadoTexto.Text = "Servicio: estado no disponible";
                EstadoIcono.Fill = Brushes.Red;
            }
        }

        private static bool ServicioExiste(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return false;
            }

            return ServiceController.GetServices()
                .Any(s => s.ServiceName.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        }

        private static string TraducirEstado(ServiceControllerStatus estado)
        {
            return estado switch
            {
                ServiceControllerStatus.Running => "en ejecución",
                ServiceControllerStatus.Stopped => "detenido",
                ServiceControllerStatus.Paused => "pausado",
                ServiceControllerStatus.StartPending => "iniciando",
                ServiceControllerStatus.StopPending => "deteniendo",
                _ => estado.ToString()
            };
        }

        private static void EjecutarProceso(string ejecutable, IEnumerable<string> argumentos)
        {
            var inicio = new ProcessStartInfo(ejecutable)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            foreach (string argumento in argumentos)
            {
                inicio.ArgumentList.Add(argumento);
            }

            using var proceso = Process.Start(inicio)
                ?? throw new InvalidOperationException($"No se pudo iniciar {ejecutable}.");

            proceso.WaitForExit();

            string salida = proceso.StandardOutput.ReadToEnd();
            string error = proceso.StandardError.ReadToEnd();

            if (proceso.ExitCode != 0)
            {
                throw new InvalidOperationException($"{ejecutable} terminó con código {proceso.ExitCode}.{Environment.NewLine}{salida}{Environment.NewLine}{error}");
            }
        }

        private void AbrirLibros_Click(object sender, RoutedEventArgs e)
        {
            AbrirCarpeta(_settings.RecordBooks.RootDirectory);
        }

        private void AbrirLogs_Click(object sender, RoutedEventArgs e)
        {
            AbrirCarpeta(_settings.ApplicationLogs.RootDirectory);
        }

        private static void AbrirCarpeta(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return;
            }

            Directory.CreateDirectory(ruta);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ruta}\"") { UseShellExecute = true });
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void NormalizarConfiguracion()
        {
            _settings.Storage ??= new StorageConfig();
            _settings.Hash ??= new HashConfig();
            _settings.RetryPolicy ??= new RetryPolicyConfig();
            _settings.StabilityCheck ??= new StabilityCheckConfig();
            _settings.Email ??= new EmailConfig();
            _settings.WatchRoutes ??= new List<WatchRouteConfig>();
            _settings.CenterRoutes ??= new List<CenterRouteConfig>();
            _settings.ArchiveProcessing ??= new ArchiveProcessingConfig();
            _settings.Quarantine ??= new QuarantineConfig();
            _settings.PendingDelivery ??= new PendingDeliveryConfig();
            _settings.RecordBooks ??= new RecordBooksConfig();
            _settings.ApplicationLogs ??= new ApplicationLogsConfig();
            _settings.Notifications ??= new NotificationConfig();
            _settings.DirectoryManagement ??= new DirectoryManagementConfig();

            _settings.Email.Recipients ??= new List<string>();
            _settings.Hash.AllowedAlgorithms ??= new List<string>();
            _settings.ArchiveProcessing.SupportedExtensions ??= new List<string>();

            string raizBase = ObtenerRaizBaseGestionada();

            _settings.Storage.TempDirectory = ValorSiVacio(_settings.Storage.TempDirectory, Path.Combine(raizBase, "Temp"));
            _settings.ArchiveProcessing.TemporaryDirectory = ValorSiVacio(_settings.ArchiveProcessing.TemporaryDirectory, _settings.Storage.TempDirectory);
            _settings.Quarantine.RootDirectory = ValorSiVacio(_settings.Quarantine.RootDirectory, Path.Combine(raizBase, "Cuarentena"));
            _settings.PendingDelivery.RootDirectory = ValorSiVacio(_settings.PendingDelivery.RootDirectory, Path.Combine(raizBase, "PendienteEntrega"));
            _settings.RecordBooks.RootDirectory = ValorSiVacio(_settings.RecordBooks.RootDirectory, Path.Combine(raizBase, "LibrosRegistro"));
            _settings.ApplicationLogs.RootDirectory = ValorSiVacio(_settings.ApplicationLogs.RootDirectory, Path.Combine(raizBase, "LogsAplicacion"));

            _settings.Hash.DefaultAlgorithm = ValorSiVacio(_settings.Hash.DefaultAlgorithm, "SHA256");
            _settings.ArchiveProcessing.ExtractorMode = ValorSiVacio(_settings.ArchiveProcessing.ExtractorMode, "SharpCompress");
            _settings.ArchiveProcessing.SevenZipExecutablePath = ValorSiVacio(_settings.ArchiveProcessing.SevenZipExecutablePath, @"C:\Program Files\7-Zip\7z.exe");

            if (_settings.Hash.AllowedAlgorithms.Count == 0)
            {
                _settings.Hash.AllowedAlgorithms.AddRange(new[] { "MD5", "SHA1", "SHA256", "SHA384", "SHA512" });
            }

            if (_settings.ArchiveProcessing.SupportedExtensions.Count == 0)
            {
                _settings.ArchiveProcessing.SupportedExtensions.AddRange(new[] { ".zip", ".7z", ".rar" });
            }

            _settings.Quarantine.CentersDirectoryName = ValorSiVacio(_settings.Quarantine.CentersDirectoryName, "Centros");
            _settings.Quarantine.UnresolvedDirectoryName = ValorSiVacio(_settings.Quarantine.UnresolvedDirectoryName, "SinResolver");
            _settings.Quarantine.ProductsDirectoryName = ValorSiVacio(_settings.Quarantine.ProductsDirectoryName, "Productos");

            _settings.PendingDelivery.CentersDirectoryName = ValorSiVacio(_settings.PendingDelivery.CentersDirectoryName, "Centros");
            _settings.PendingDelivery.ProductsDirectoryName = ValorSiVacio(_settings.PendingDelivery.ProductsDirectoryName, "Productos");

            _settings.RecordBooks.CentersDirectoryName = ValorSiVacio(_settings.RecordBooks.CentersDirectoryName, "Centros");
            _settings.RecordBooks.UnresolvedDirectoryName = ValorSiVacio(_settings.RecordBooks.UnresolvedDirectoryName, "SinResolver");
            _settings.RecordBooks.DailyDirectoryName = ValorSiVacio(_settings.RecordBooks.DailyDirectoryName, "Diarios");
            _settings.RecordBooks.MonthlyDirectoryName = ValorSiVacio(_settings.RecordBooks.MonthlyDirectoryName, "Mensuales");
            _settings.RecordBooks.ArchiveDirectoryName = ValorSiVacio(_settings.RecordBooks.ArchiveDirectoryName, "Archivo");
            _settings.RecordBooks.AcceptedDirectoryName = ValorSiVacio(_settings.RecordBooks.AcceptedDirectoryName, "Aceptados");
            _settings.RecordBooks.QuarantineDirectoryName = ValorSiVacio(_settings.RecordBooks.QuarantineDirectoryName, "Cuarentena");
            _settings.RecordBooks.PendingDeliveryDirectoryName = ValorSiVacio(_settings.RecordBooks.PendingDeliveryDirectoryName, "PendienteEntrega");
            _settings.RecordBooks.NotificationsDirectoryName = ValorSiVacio(_settings.RecordBooks.NotificationsDirectoryName, "Notificaciones");
            _settings.RecordBooks.ManualActionsDirectoryName = ValorSiVacio(_settings.RecordBooks.ManualActionsDirectoryName, "AccionesManuales");
            _settings.RecordBooks.Delimiter = ValorSiVacio(_settings.RecordBooks.Delimiter, ";");

            _settings.RecordBooks.AcceptedDailyPattern = ValorSiVacio(_settings.RecordBooks.AcceptedDailyPattern, "yyyyMMdd_LR_productos_aceptados.csv");
            _settings.RecordBooks.QuarantineDailyPattern = ValorSiVacio(_settings.RecordBooks.QuarantineDailyPattern, "yyyyMMdd_LR_productos_cuarentena.csv");
            _settings.RecordBooks.PendingDeliveryDailyPattern = ValorSiVacio(_settings.RecordBooks.PendingDeliveryDailyPattern, "yyyyMMdd_LR_productos_pendiente_entrega.csv");
            _settings.RecordBooks.NotificationsDailyPattern = ValorSiVacio(_settings.RecordBooks.NotificationsDailyPattern, "yyyyMMdd_LR_notificaciones.csv");
            _settings.RecordBooks.ManualActionsDailyPattern = ValorSiVacio(_settings.RecordBooks.ManualActionsDailyPattern, "yyyyMMdd_LR_acciones_manuales.csv");

            _settings.ApplicationLogs.ServiceDirectoryName = ValorSiVacio(_settings.ApplicationLogs.ServiceDirectoryName, "Servicio");
            _settings.ApplicationLogs.ErrorsDirectoryName = ValorSiVacio(_settings.ApplicationLogs.ErrorsDirectoryName, "Errores");
            _settings.ApplicationLogs.DailyDirectoryName = ValorSiVacio(_settings.ApplicationLogs.DailyDirectoryName, "Diarios");
            _settings.ApplicationLogs.MonthlyDirectoryName = ValorSiVacio(_settings.ApplicationLogs.MonthlyDirectoryName, "Mensuales");
            _settings.ApplicationLogs.ArchiveDirectoryName = ValorSiVacio(_settings.ApplicationLogs.ArchiveDirectoryName, "Archivo");
            _settings.ApplicationLogs.Delimiter = ValorSiVacio(_settings.ApplicationLogs.Delimiter, ";");

            _settings.Email.Subject = ValorSiVacio(_settings.Email.Subject, "HeimdallHash - Notificación");

            _settings.Notifications.QuarantineSubjectPrefix = ValorSiVacio(_settings.Notifications.QuarantineSubjectPrefix, "[HeimdallHash] Producto enviado a cuarentena");
            _settings.Notifications.PendingCreatedSubjectPrefix = ValorSiVacio(_settings.Notifications.PendingCreatedSubjectPrefix, "[HeimdallHash] Producto pendiente de entrega");
            _settings.Notifications.PendingDeliveredSubjectPrefix = ValorSiVacio(_settings.Notifications.PendingDeliveredSubjectPrefix, "[HeimdallHash] Producto pendiente entregado");
            _settings.Notifications.PendingStillFailedSubjectPrefix = ValorSiVacio(_settings.Notifications.PendingStillFailedSubjectPrefix, "[HeimdallHash] Producto pendiente aún no entregado");
        }

        private string ObtenerRaizBaseGestionada()
        {
            string? ruta = new[]
            {
                _settings.Storage?.TempDirectory,
                _settings.Quarantine?.RootDirectory,
                _settings.PendingDelivery?.RootDirectory,
                _settings.RecordBooks?.RootDirectory,
                _settings.ApplicationLogs?.RootDirectory
            }.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));

            if (string.IsNullOrWhiteSpace(ruta))
            {
                return @"C:\HeimdallHashData";
            }

            string nombreFinal = Path.GetFileName(ruta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string[] nombresInternos = { "Temp", "Cuarentena", "PendienteEntrega", "LibrosRegistro", "LogsAplicacion" };

            if (nombresInternos.Any(n => n.Equals(nombreFinal, StringComparison.OrdinalIgnoreCase)))
            {
                return Directory.GetParent(ruta)?.FullName ?? @"C:\HeimdallHashData";
            }

            return ruta;
        }

        private void ActualizarResumen()
        {
            ResumenWatchRoutesText.Text = $"{_settings.WatchRoutes.Count} ruta(s) configurada(s)";
            ResumenCenterRoutesText.Text = $"{_settings.CenterRoutes.Count} centro(s)/destino(s) configurado(s)";
            ResumenNotificationsText.Text = _settings.Notifications.Enabled ? "Habilitadas" : "Deshabilitadas";
            ResumenPendingText.Text = _settings.PendingDelivery.Enabled ? "Habilitado" : "Deshabilitado";
        }

        private void AgregarAlgoritmoSiMarcado(CheckBox checkBox, string algoritmo)
        {
            if (EstaMarcado(checkBox))
            {
                _settings.Hash.AllowedAlgorithms.Add(algoritmo);
            }
        }

        private static bool EstaMarcado(CheckBox checkBox)
        {
            return checkBox.IsChecked == true;
        }

        private static string LeerTexto(TextBox textBox)
        {
            return textBox.Text?.Trim() ?? string.Empty;
        }

        private static int LeerEntero(TextBox textBox, int valorDefecto)
        {
            return int.TryParse(textBox.Text, out int valor)
                ? valor
                : valorDefecto;
        }

        private static List<string> SepararLista(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return new List<string>();
            }

            return texto
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ObtenerTextoCombo(ComboBox comboBox, string valorDefecto)
        {
            if (comboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? valorDefecto;
            }

            return comboBox.Text?.Trim() is { Length: > 0 } texto
                ? texto
                : valorDefecto;
        }

        private static void SeleccionarComboPorTexto(ComboBox comboBox, string texto)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Content?.ToString()?.Equals(texto, StringComparison.OrdinalIgnoreCase) == true)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            comboBox.Text = texto;
        }

        private static bool EsAdministrador()
        {
            using var identidad = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identidad);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static bool EsCenterIdValido(string centerId)
        {
            return !string.IsNullOrWhiteSpace(centerId) &&
                   centerId.Length >= 4 &&
                   centerId.Length <= 10 &&
                   centerId.All(char.IsDigit);
        }

        private static bool EsFlujoValido(string flow)
        {
            return flow.Equals("Upload", StringComparison.OrdinalIgnoreCase) ||
                   flow.Equals("Download", StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsModoValidacionValido(string mode)
        {
            return mode.Equals("DeliveryNote", StringComparison.OrdinalIgnoreCase) ||
                   mode.Equals("FileNameHash", StringComparison.OrdinalIgnoreCase) ||
                   mode.Equals("Auto", StringComparison.OrdinalIgnoreCase);
        }

        private static string ValorSiVacio(string? valor, string valorDefecto)
        {
            return string.IsNullOrWhiteSpace(valor)
                ? valorDefecto
                : valor;
        }

        private static string ProtegerDpapiLocalMachine(string textoPlano)
        {
            byte[] datos = Encoding.UTF8.GetBytes(textoPlano);
            byte[] protegidos = ProtectedData.Protect(datos, optionalEntropy: null, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(protegidos);
        }

        private static void MostrarError(string titulo, Exception ex)
        {
            MessageBox.Show(
                $"{titulo}:{Environment.NewLine}{ex.Message}",
                "HeimdallHash",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private sealed class ValidacionItem
        {
            public string Nivel { get; }
            public string Mensaje { get; }

            public ValidacionItem(string nivel, string mensaje)
            {
                Nivel = nivel;
                Mensaje = mensaje;
            }
        }
    }
}
