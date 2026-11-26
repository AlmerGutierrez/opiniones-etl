using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Application.Servicios;

namespace OpinionesEtl.Worker;

/// <summary>
/// Servicio en segundo plano que dispara la extracción a staging y, a continuación, la carga de las
/// dimensiones del Data Warehouse mediante procedimientos almacenados.
/// </summary>
public sealed class ExtraccionWorker : BackgroundService
{
    private readonly OrquestadorExtraccion _orquestador;
    private readonly OrquestadorCarga _orquestadorCarga;
    private readonly EjecucionOptions _opciones;
    private readonly CargaDwOptions _opcionesDw;
    private readonly IHostApplicationLifetime _ciclo;
    private readonly ILogger<ExtraccionWorker> _logger;

    public ExtraccionWorker(OrquestadorExtraccion orquestador,
                            OrquestadorCarga orquestadorCarga,
                            IOptions<EjecucionOptions> opciones,
                            IOptions<CargaDwOptions> opcionesDw,
                            IHostApplicationLifetime ciclo,
                            ILogger<ExtraccionWorker> logger)
    {
        _orquestador = orquestador;
        _orquestadorCarga = orquestadorCarga;
        _opciones = opciones.Value;
        _opcionesDw = opcionesDw.Value;
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
                var conErrores = resumen.ConErrores;

                if (_opcionesDw.Habilitada)
                {
                    var resumenCarga = await _orquestadorCarga.EjecutarAsync(resumen.IdEjecucion, stoppingToken);
                    conErrores = conErrores || resumenCarga.ConErrores;
                }

                if (conErrores)
                {
                    Environment.ExitCode = 2;   // alguna fuente o dimensión falló: el código de salida lo refleja
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
            _logger.LogCritical(ex, "El proceso ETL terminó con un error no controlado");
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