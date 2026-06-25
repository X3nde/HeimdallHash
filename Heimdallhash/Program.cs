using Heimdallhash;
using Heimdallhash.Config;
using Heimdallhash.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal class Program
{
    public static void Main(string[] args)
    {
        var isConsole = args.Contains("--console");

        IHostBuilder builder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                //Registra la configuración de la aplicación desde appsettings.json
                services.Configure<AppSettings>(hostContext.Configuration);

                //Registra los servicios principales de HeimdallHash.
                services.AddSingleton<FileProcessor>();

                services.AddSingleton<ErrorLogger>();

                services.AddSingleton<ServiceCycleLogger>();

                services.AddSingleton<RecordBookMaintenanceService>();

                services.AddSingleton<MailNotifier>();

                services.AddSingleton<DeliveryNoteReader>();

                services.AddSingleton<PackageValidator>();

                services.AddSingleton<ArchiveExtractor>();

                services.AddSingleton<DeliveryNoteLocator>();

                services.AddSingleton<PackageProcessor>();

                services.AddSingleton<PackageAuditLogger>();

                services.AddSingleton<PendingDeliveryService>();

                services.AddSingleton<DirectoryStartupValidator>();

                services.AddSingleton<ConfigurationSanityValidator>();

                //Registra el worker como servicio de fondo.
                services.AddHostedService<Worker>();
            });


        if (isConsole)
        {
            // Ejecución en modo consola para desarrollo y pruebas.
            builder.UseConsoleLifetime(); // permite CTRL+C
        }
        else
        {
            // Ejecución como servicio de Windows para entorno operativo.
            builder.UseWindowsService(); // integración con SCM
        }

        builder.Build().Run();
    }
}
