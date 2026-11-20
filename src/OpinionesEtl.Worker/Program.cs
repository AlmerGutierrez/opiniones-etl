using OpinionesEtl.Infrastructure;
using OpinionesEtl.Worker;

var constructor = Host.CreateApplicationBuilder(args);

// Logs en una línea con hora. Todos los eventos y errores del proceso pasan por ILogger.
constructor.Logging.ClearProviders();
constructor.Logging.AddSimpleConsole(opciones =>
{
    opciones.SingleLine = true;
    opciones.TimestampFormat = "HH:mm:ss ";
});

constructor.Services.AddInfraestructura(constructor.Configuration);
constructor.Services.AddHostedService<ExtraccionWorker>();

await constructor.Build().RunAsync();
