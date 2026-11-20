using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Application.Servicios;

namespace OpinionesEtl.Worker;

/// <summary>
/// Servicio en segundo plano que dispara la extracción. En modo "UnaVez" ejecuta el proceso y cierra la aplicación;
/// en modo "Periodico" se repite cada IntervaloMinutos hasta que se detenga el servicio.
/// </summary>
public sealed class ExtraccionWorker : BackgroundService
{
    private readonly OrquestadorExtraccion _orquestador;
    private readonly EjecucionOptions _opciones;
    private readonly IHostApplicationLifetime _ciclo;
    private readonly ILogger<ExtraccionWorker> _logger;

    public ExtraccionWorker(OrquestadorExtraccion orquestador, IOptions<EjecucionOptions> opciones,
                            IHostApplicationLifetime ciclo, ILogger<ExtraccionWorker> logger)
    {
        _orquestador = orquestador;
        _opciones = opciones.Value;
        _ciclo = ciclo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var periodico = string.Equals(_opciones.Modo, "Periodico", StringComparison.OrdinalIgnoreCase);
        try
        {
            do
            {
                var resumen = await _orquestador.EjecutarAsync(stoppingToken);
                if (resumen.ConErrores)
                {
                    Environment.ExitCode = 2;   // alguna fuente falló: el código de salida lo refleja
                }

                if (periodico)
                {
                    var minutos = Math.Max(1, _opciones.IntervaloMinutos);
                    _logger.LogInformation("Próxima ejecución en {Minutos} minuto(s)", minutos);
                    await Task.Delay(TimeSpan.FromMinutes(minutos), stoppingToken);
                }
            }
            while (periodico && !stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("El servicio se detuvo.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "La extracción terminó con un error no controlado");
            Environment.ExitCode = 1;
        }
        finally
        {
            if (!periodico)
            {
                _ciclo.StopApplication();
            }
        }
    }
}
