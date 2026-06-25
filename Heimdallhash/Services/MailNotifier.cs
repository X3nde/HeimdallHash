using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Heimdallhash.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heimdallhash.Services
{
    /*
     * Gestiona el envío de notificaciones mediante SMTP/Exchange interno.
     *
     * Las notificaciones se registran en libros de registro funcionales, no
     * en los logs técnicos de aplicación.
     *
     * Con centro resuelto:
     *
     * LibrosRegistro\Centros\<CenterId>\<Flow>\Notificaciones\Diarios
     *
     * Sin centro resuelto:
     *
     * LibrosRegistro\SinResolver\Notificaciones\Diarios
     */
    public class MailNotifier
    {
        private const string TipoQuarantineCreated = "QUARANTINE_CREATED";
        private const string TipoPendingCreated = "PENDING_CREATED";
        private const string TipoPendingDelivered = "PENDING_DELIVERED";
        private const string TipoPendingStillFailed = "PENDING_STILL_FAILED";
        private const string DpapiPrefix = "dpapi:";

        private readonly AppSettings _settings;
        private readonly ILogger<MailNotifier> _logger;

        public MailNotifier(
            IOptions<AppSettings> settings,
            ILogger<MailNotifier> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /*
         * Método mantenido por compatibilidad con Worker.cs.
         *
         * Las notificaciones se envían por eventos concretos. Más adelante
         * este método podrá revisar notificaciones pendientes que no se
         * hubieran enviado correctamente.
         */
        public Task EnviarCorreoSiCorresponde()
        {
            return Task.CompletedTask;
        }

        /*
         * Envía una notificación cuando un producto entra en cuarentena.
         */
        public async Task EnviarNotificacionCuarentenaAsync(
            string originalEventId,
            string packageName,
            string flow,
            string centerId,
            string reasonCode,
            string reasonDetail,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.Notifications.Enabled || !_settings.Notifications.NotifyOnQuarantine)
            {
                _logger.LogInformation("Las notificaciones de cuarentena están desactivadas.");
                return;
            }

            string prefijoAsunto = ObtenerValorODefecto(
                _settings.Notifications.QuarantineSubjectPrefix,
                "[HeimdallHash] Producto enviado a cuarentena");

            string asunto = ConstruirAsunto(prefijoAsunto, packageName);

            string cuerpo =
$@"Se ha enviado un producto a cuarentena.

Producto: {packageName}
Fecha: {timestamp:yyyy-MM-dd}
Hora: {timestamp:HH:mm:ss}
Flujo: {flow}
Centro: {centerId}
Causa: {reasonCode}
Detalle: {reasonDetail}

Acción recomendada:
Revisar el libro de cuarentena correspondiente al centro o, si no existe centro resuelto, el libro de eventos sin resolver.";

            await EnviarNotificacionAsync(
                originalEventId,
                TipoQuarantineCreated,
                packageName,
                flow,
                centerId,
                reasonCode,
                asunto,
                cuerpo,
                cancellationToken);
        }

        /*
         * Envía una notificación cuando un producto válido no puede entregarse
         * y se mueve a PendienteEntrega.
         */
        public async Task EnviarNotificacionPendienteCreadoAsync(
            string originalEventId,
            string packageName,
            string flow,
            string centerId,
            string destinationPath,
            string pendingPackagePath,
            string reasonCode,
            string reasonDetail,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.Notifications.Enabled || !_settings.Notifications.NotifyOnPendingCreated)
            {
                _logger.LogInformation("Las notificaciones de creación de pendientes están desactivadas.");
                return;
            }

            string prefijoAsunto = ObtenerValorODefecto(
                _settings.Notifications.PendingCreatedSubjectPrefix,
                "[HeimdallHash] Producto pendiente de entrega");

            string asunto = ConstruirAsunto(prefijoAsunto, packageName);

            string cuerpo =
$@"Se ha registrado un producto válido como pendiente de entrega.

Producto: {packageName}
Fecha: {timestamp:yyyy-MM-dd}
Hora: {timestamp:HH:mm:ss}
Flujo: {flow}
Centro: {centerId}
Destino previsto: {destinationPath}
Ruta pendiente: {pendingPackagePath}
Causa: {reasonCode}
Detalle: {reasonDetail}

Acción recomendada:
Comprobar disponibilidad de red, permisos sobre la ruta destino, estado del servidor remoto, firewall o recurso compartido.";

            await EnviarNotificacionAsync(
                originalEventId,
                TipoPendingCreated,
                packageName,
                flow,
                centerId,
                reasonCode,
                asunto,
                cuerpo,
                cancellationToken);
        }

        /*
         * Envía una notificación cuando un producto pendiente se entrega
         * correctamente tras uno o varios reintentos.
         */
        public async Task EnviarNotificacionPendienteEntregadoAsync(
            string originalEventId,
            string packageName,
            string flow,
            string centerId,
            string destinationPath,
            string deliveredFilePath,
            int attempts,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.Notifications.Enabled || !_settings.Notifications.NotifyOnPendingDelivered)
            {
                _logger.LogInformation("Las notificaciones de entrega de pendientes están desactivadas.");
                return;
            }

            string prefijoAsunto = ObtenerValorODefecto(
                _settings.Notifications.PendingDeliveredSubjectPrefix,
                "[HeimdallHash] Producto pendiente entregado");

            string asunto = ConstruirAsunto(prefijoAsunto, packageName);

            string cuerpo =
$@"Se ha entregado correctamente un producto que estaba pendiente de entrega.

Producto: {packageName}
Fecha: {timestamp:yyyy-MM-dd}
Hora: {timestamp:HH:mm:ss}
Flujo: {flow}
Centro: {centerId}
Destino previsto: {destinationPath}
Ruta final entregada: {deliveredFilePath}
Intentos acumulados: {attempts}

Resultado:
La incidencia queda resuelta y el producto ha sido entregado en su destino final.";

            await EnviarNotificacionAsync(
                originalEventId,
                TipoPendingDelivered,
                packageName,
                flow,
                centerId,
                "PENDING_DELIVERED",
                asunto,
                cuerpo,
                cancellationToken);
        }

        /*
         * Envía una notificación cuando un producto pendiente acumula fallos
         * persistentes tras varios ciclos.
         */
        public async Task EnviarNotificacionPendienteSigueFallandoAsync(
            string originalEventId,
            string packageName,
            string flow,
            string centerId,
            string destinationPath,
            string pendingPackagePath,
            int failedCycles,
            string reasonCode,
            string reasonDetail,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.Notifications.Enabled || !_settings.Notifications.NotifyOnPendingStillFailed)
            {
                _logger.LogInformation("Las notificaciones de fallo persistente de pendientes están desactivadas.");
                return;
            }

            string prefijoAsunto = ObtenerValorODefecto(
                _settings.Notifications.PendingStillFailedSubjectPrefix,
                "[HeimdallHash] Producto pendiente aún no entregado");

            string asunto = ConstruirAsunto(prefijoAsunto, packageName);

            string cuerpo =
$@"Un producto pendiente de entrega sigue sin poder entregarse.

Producto: {packageName}
Fecha: {timestamp:yyyy-MM-dd}
Hora: {timestamp:HH:mm:ss}
Flujo: {flow}
Centro: {centerId}
Destino previsto: {destinationPath}
Ruta pendiente: {pendingPackagePath}
Ciclos fallidos acumulados: {failedCycles}
Causa: {reasonCode}
Detalle: {reasonDetail}

Acción recomendada:
Revisar la conectividad, permisos de escritura, recurso compartido, firewall y disponibilidad del destino.";

            await EnviarNotificacionAsync(
                originalEventId,
                TipoPendingStillFailed,
                packageName,
                flow,
                centerId,
                reasonCode,
                asunto,
                cuerpo,
                cancellationToken);
        }

        /*
         * Ejecuta el flujo común de envío y registro de una notificación.
         */
        private async Task EnviarNotificacionAsync(
            string originalEventId,
            string notificationType,
            string packageName,
            string flow,
            string centerId,
            string reasonCode,
            string asunto,
            string cuerpo,
            CancellationToken cancellationToken)
        {
            if (_settings.Notifications.PreventDuplicateNotifications &&
                YaExisteNotificacionEnviada(originalEventId, notificationType, flow, centerId))
            {
                _logger.LogInformation(
                    "No se reenvía notificación. El evento {EventId} de tipo {NotificationType} ya fue notificado.",
                    originalEventId,
                    notificationType);

                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                await EnviarCorreoAsync(asunto, cuerpo, cancellationToken);

                RegistrarNotificacion(
                    originalEventId,
                    notificationType,
                    packageName,
                    flow,
                    centerId,
                    reasonCode,
                    "SENT",
                    "Notificación enviada correctamente.");

                _logger.LogInformation(
                    "Notificación {NotificationType} enviada correctamente para el producto {ProductName}.",
                    notificationType,
                    packageName);
            }
            catch (Exception ex)
            {
                RegistrarNotificacion(
                    originalEventId,
                    notificationType,
                    packageName,
                    flow,
                    centerId,
                    reasonCode,
                    "FAILED",
                    ex.Message);

                _logger.LogError(
                    ex,
                    "Error al enviar notificación {NotificationType} para el producto {ProductName}.",
                    notificationType,
                    packageName);
            }
        }

        /*
         * Envía un correo usando SMTP/Exchange interno.
         */
        private async Task EnviarCorreoAsync(
            string asunto,
            string cuerpo,
            CancellationToken cancellationToken)
        {
            ValidarConfiguracionCorreo();

            using var mensaje = new MailMessage
            {
                From = new MailAddress(_settings.Email.Sender),
                Subject = asunto,
                Body = cuerpo,
                IsBodyHtml = false
            };

            foreach (var destinatario in _settings.Email.Recipients)
            {
                if (!string.IsNullOrWhiteSpace(destinatario))
                {
                    mensaje.To.Add(destinatario.Trim());
                }
            }

            using var clienteSmtp = new SmtpClient(_settings.Email.SmtpServer, _settings.Email.Port)
            {
                EnableSsl = _settings.Email.EnableSsl
            };

            if (_settings.Email.UseCredentials)
            {
                clienteSmtp.Credentials = new NetworkCredential(
                    _settings.Email.Sender,
                    ObtenerPasswordSmtp());
            }

            cancellationToken.ThrowIfCancellationRequested();

            await clienteSmtp.SendMailAsync(mensaje);
        }

        /*
         * Obtiene la contraseña SMTP.
         *
         * Si el valor empieza por dpapi:, se descifra usando DPAPI con ámbito
         * LocalMachine. Esto permite que el configurador guarde la contraseña
         * cifrada y que el servicio la use con una cuenta de servicio local.
         */
        private string ObtenerPasswordSmtp()
        {
            string password = _settings.Email.Password ?? string.Empty;

            if (!password.StartsWith(DpapiPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return password;
            }

            string payload = password.Substring(DpapiPrefix.Length);

            try
            {
                return DesprotegerDpapiLocalMachine(payload);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "No se pudo descifrar la contraseña SMTP protegida con DPAPI.",
                    ex);
            }
        }

        private static string DesprotegerDpapiLocalMachine(string textoProtegidoBase64)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException(
                    "DPAPI LocalMachine solo está disponible en sistemas Windows.");
            }

            byte[] protegidos = Convert.FromBase64String(textoProtegidoBase64);
            byte[] datos = ProtectedData.Unprotect(
                protegidos,
                optionalEntropy: null,
                DataProtectionScope.LocalMachine);

            return Encoding.UTF8.GetString(datos);
        }

        /*
         * Valida que la configuración mínima de correo esté definida.
         */
        private void ValidarConfiguracionCorreo()
        {
            if (string.IsNullOrWhiteSpace(_settings.Email.SmtpServer))
            {
                throw new InvalidOperationException("No se ha configurado el servidor SMTP.");
            }

            if (string.IsNullOrWhiteSpace(_settings.Email.Sender))
            {
                throw new InvalidOperationException("No se ha configurado la cuenta remitente.");
            }

            if (_settings.Email.Recipients == null || _settings.Email.Recipients.Count == 0)
            {
                throw new InvalidOperationException("No se han configurado destinatarios de notificación.");
            }
        }

        /*
         * Construye el asunto final de la notificación.
         */
        private static string ConstruirAsunto(string prefijo, string packageName)
        {
            return $"{prefijo} - {packageName}";
        }

        /*
         * Registra el resultado del envío en el libro diario de notificaciones.
         */
        private void RegistrarNotificacion(
            string originalEventId,
            string notificationType,
            string packageName,
            string flow,
            string centerId,
            string reasonCode,
            string result,
            string detail)
        {
            try
            {
                string rutaLibro = ObtenerRutaLibroNotificaciones(centerId, flow);

                string? directorio = Path.GetDirectoryName(rutaLibro);

                if (!string.IsNullOrWhiteSpace(directorio))
                {
                    Directory.CreateDirectory(directorio);
                }

                bool existe = File.Exists(rutaLibro);
                string separador = ObtenerSeparador();

                using var writer = new StreamWriter(rutaLibro, append: true);

                if (!existe)
                {
                    writer.WriteLine(string.Join(separador, new[]
                    {
                        "NotificationEventId",
                        "OriginalEventId",
                        "NotificationType",
                        "TimestampLocal",
                        "TimestampUtc",
                        "ProductName",
                        "Flow",
                        "CenterId",
                        "ReasonCode",
                        "Recipients",
                        "Result",
                        "Detail"
                    }));
                }

                string notificationEventId = Guid.NewGuid().ToString();
                string timestampLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                string recipients = _settings.Email.Recipients == null
                    ? string.Empty
                    : string.Join(",", _settings.Email.Recipients);

                string linea = string.Join(separador, new[]
                {
                    EscaparCsv(notificationEventId),
                    EscaparCsv(originalEventId),
                    EscaparCsv(notificationType),
                    EscaparCsv(timestampLocal),
                    EscaparCsv(timestampUtc),
                    EscaparCsv(packageName),
                    EscaparCsv(flow),
                    EscaparCsv(centerId),
                    EscaparCsv(reasonCode),
                    EscaparCsv(recipients),
                    EscaparCsv(result),
                    EscaparCsv(detail)
                });

                writer.WriteLine(linea);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo registrar el resultado de la notificación.");
            }
        }

        /*
         * Comprueba si un evento y tipo de notificación ya fueron enviados correctamente.
         *
         * Se comprueba por OriginalEventId + NotificationType + Result = SENT.
         * Esto permite enviar varias notificaciones distintas para el mismo producto:
         * cuarentena, pendiente creado, pendiente entregado, etc.
         */
        private bool YaExisteNotificacionEnviada(
            string originalEventId,
            string notificationType,
            string flow,
            string centerId)
        {
            try
            {
                string rutaLibro = ObtenerRutaLibroNotificaciones(centerId, flow);

                if (!File.Exists(rutaLibro))
                {
                    return false;
                }

                foreach (var linea in File.ReadLines(rutaLibro).Skip(1))
                {
                    var campos = SepararCsv(linea);

                    if (campos.Count < 11)
                    {
                        continue;
                    }

                    string originalEventIdRegistrado = campos[1];
                    string notificationTypeRegistrado = campos[2];
                    string resultado = campos[10];

                    bool coincideEvento = originalEventIdRegistrado.Equals(
                        originalEventId,
                        StringComparison.OrdinalIgnoreCase);

                    bool coincideTipo = notificationTypeRegistrado.Equals(
                        notificationType,
                        StringComparison.OrdinalIgnoreCase);

                    bool enviado = resultado.Equals(
                        "SENT",
                        StringComparison.OrdinalIgnoreCase);

                    if (coincideEvento && coincideTipo && enviado)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo comprobar si la notificación ya había sido enviada.");

                return false;
            }
        }

        /*
         * Obtiene la ruta del libro diario de notificaciones.
         *
         * Con centro resuelto:
         * LibrosRegistro\Centros\<CenterId>\<Flow>\Notificaciones\Diarios\yyyyMMdd_LR_notificaciones.csv
         *
         * Sin centro resuelto:
         * LibrosRegistro\SinResolver\Notificaciones\Diarios\yyyyMMdd_LR_notificaciones_sinresolver.csv
         */
        private string ObtenerRutaLibroNotificaciones(string centerId, string flow)
        {
            string directorioDiario = ObtenerDirectorioDiarioNotificaciones(centerId, flow);

            string patron = string.IsNullOrWhiteSpace(centerId)
                ? ObtenerValorODefecto(
                    _settings.RecordBooks.UnresolvedNotificationsDailyPattern,
                    "yyyyMMdd_LR_notificaciones_sinresolver.csv")
                : ObtenerValorODefecto(
                    _settings.RecordBooks.NotificationsDailyPattern,
                    "yyyyMMdd_LR_notificaciones.csv");

            string nombreLibro = patron.Replace(
                "yyyyMMdd",
                DateTime.Now.ToString("yyyyMMdd"));

            return Path.Combine(
                directorioDiario,
                SanitizarNombreFichero(nombreLibro));
        }

        /*
         * Obtiene el directorio diario de notificaciones según centro y flujo.
         */
        private string ObtenerDirectorioDiarioNotificaciones(string centerId, string flow)
        {
            string raizLibros = ObtenerRaizLibrosRegistro();

            string notificaciones = ObtenerValorODefecto(
                _settings.RecordBooks.NotificationsDirectoryName,
                "Notificaciones");

            string diarios = ObtenerValorODefecto(
                _settings.RecordBooks.DailyDirectoryName,
                "Diarios");

            if (string.IsNullOrWhiteSpace(centerId))
            {
                string sinResolver = ObtenerValorODefecto(
                    _settings.RecordBooks.UnresolvedDirectoryName,
                    "SinResolver");

                return Path.Combine(
                    raizLibros,
                    SanitizarNombreDirectorio(sinResolver),
                    SanitizarNombreDirectorio(notificaciones),
                    SanitizarNombreDirectorio(diarios));
            }

            string centros = ObtenerValorODefecto(
                _settings.RecordBooks.CentersDirectoryName,
                "Centros");

            return Path.Combine(
                raizLibros,
                SanitizarNombreDirectorio(centros),
                SanitizarNombreDirectorio(centerId),
                SanitizarNombreDirectorio(ObtenerValorODefecto(flow, "SinFlujo")),
                SanitizarNombreDirectorio(notificaciones),
                SanitizarNombreDirectorio(diarios));
        }

        /*
         * Obtiene la raíz de libros de registro.
         */
        private string ObtenerRaizLibrosRegistro()
        {
            if (!string.IsNullOrWhiteSpace(_settings.RecordBooks.RootDirectory))
            {
                return _settings.RecordBooks.RootDirectory;
            }

            _logger.LogWarning(
                "RecordBooks.RootDirectory no está configurado. Se usará la ruta por defecto dentro del directorio base de la aplicación.");

            return Path.Combine(AppContext.BaseDirectory, "LibrosRegistro");
        }

        /*
         * Devuelve el separador CSV configurado.
         */
        private string ObtenerSeparador()
        {
            return string.IsNullOrWhiteSpace(_settings.RecordBooks.Delimiter)
                ? ";"
                : _settings.RecordBooks.Delimiter;
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
         * Sanitiza nombres de carpetas derivados de configuración o de datos de producto.
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

        /*
         * Sanitiza nombres de fichero derivados de configuración.
         */
        private static string SanitizarNombreFichero(string? value)
        {
            value ??= string.Empty;
            value = value.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                return "yyyyMMdd_LR_notificaciones.csv"
                    .Replace("yyyyMMdd", DateTime.Now.ToString("yyyyMMdd"));
            }

            foreach (char caracterInvalido in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(caracterInvalido, '_');
            }

            return value;
        }

        /*
         * Escapa un campo CSV para evitar que saltos de línea, comillas o separadores
         * rompan la estructura del fichero.
         */
        private string EscaparCsv(string? value)
        {
            value ??= string.Empty;

            string separador = ObtenerSeparador();

            bool necesitaComillas =
                value.Contains(separador) ||
                value.Contains('"') ||
                value.Contains('\n') ||
                value.Contains('\r');

            value = value.Replace("\"", "\"\"");

            return necesitaComillas
                ? $"\"{value}\""
                : value;
        }

        /*
         * Separa una línea CSV teniendo en cuenta comillas.
         *
         * Es suficiente para los libros generados por HeimdallHash, donde
         * el separador se configura normalmente como ';'.
         */
        private List<string> SepararCsv(string linea)
        {
            var campos = new List<string>();
            string separador = ObtenerSeparador();

            bool dentroDeComillas = false;
            var campoActual = new System.Text.StringBuilder();

            for (int i = 0; i < linea.Length; i++)
            {
                char caracter = linea[i];

                if (caracter == '"')
                {
                    if (dentroDeComillas &&
                        i + 1 < linea.Length &&
                        linea[i + 1] == '"')
                    {
                        campoActual.Append('"');
                        i++;
                    }
                    else
                    {
                        dentroDeComillas = !dentroDeComillas;
                    }

                    continue;
                }

                if (!dentroDeComillas &&
                    i <= linea.Length - separador.Length &&
                    linea.Substring(i, separador.Length) == separador)
                {
                    campos.Add(campoActual.ToString());
                    campoActual.Clear();
                    i += separador.Length - 1;
                    continue;
                }

                campoActual.Append(caracter);
            }

            campos.Add(campoActual.ToString());

            return campos;
        }
    }
}
