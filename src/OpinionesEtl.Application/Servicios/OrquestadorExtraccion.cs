using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Application.Modelos;

namespace OpinionesEtl.Application.Servicios;

/// <summary>Ejecuta todas las fuentes habilitadas en paralelo (con un tope) y consolida el resultado.</summary>
public sealed class OrquestadorExtraccion
{
    private readonly IReadOnlyList<ITareaExtraccion> _tareas;
    private readonly IStagingMantenimiento _mantenimiento;
    private readonly EjecucionOptions _opciones;
    private readonly ILogger<OrquestadorExtraccion> _logger;

    public OrquestadorExtraccion(IEnumerable<ITareaExtraccion> tareas, IStagingMantenimiento mantenimiento,
                                 IOptions<EjecucionOptions> opciones, ILogger<OrquestadorExtraccion> logger)
    {
        _tareas = tareas.ToList();
        _mantenimiento = mantenimiento;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task<ResumenEjecucion> EjecutarAsync(CancellationToken cancellationToken = default)
    {
        var idEjecucion = Guid.NewGuid();
        var cronometro = Stopwatch.StartNew();
        _logger.LogInformation("Ejecución {Id} iniciada con {Fuentes} fuente(s), paralelismo máximo {Paralelismo}",
            idEjecucion, _tareas.Count, _opciones.MaxParalelismo);

        await _mantenimiento.PrepararAsync(idEjecucion, _opciones.LimpiarStagingAlIniciar, cancellationToken);

        using var semaforo = new SemaphoreSlim(Math.Max(1, _opciones.MaxParalelismo));
        var ejecuciones = _tareas.Select(async tarea =>
        {
            await semaforo.WaitAsync(cancellationToken);
            try
            {
                return await tarea.EjecutarAsync(idEjecucion, cancellationToken);
            }
            finally
            {
                semaforo.Release();
            }
        }).ToList();

        var resultados = await Task.WhenAll(ejecuciones);
        var resumen = new ResumenEjecucion(idEjecucion, resultados, cronometro.Elapsed);

        await _mantenimiento.FinalizarAsync(idEjecucion, resumen.ConErrores ? "ConErrores" : "Completada", cancellationToken);
        RegistrarResumen(resumen);
        return resumen;
    }

    private void RegistrarResumen(ResumenEjecucion resumen)
    {
        _logger.LogInformation("=== RESUMEN DE LA EXTRACCIÓN (ejecución {Id}) ===", resumen.IdEjecucion);
        foreach (var t in resumen.Tareas)
        {
            _logger.LogInformation("{Fuente,-30} leídas={Leidas,5} guardadas={Guardadas,5} rechazadas={Rechazadas,4} {Ms,6} ms {Estado}",
                t.Nombre, t.Leidas, t.Guardadas, t.Rechazadas, (long)t.Duracion.TotalMilliseconds, t.Fallo ? "FALLÓ: " + t.Error : "OK");
        }
        _logger.LogInformation("TOTAL leídas={Leidas} guardadas={Guardadas} rechazadas={Rechazadas} en {Ms} ms",
            resumen.TotalLeidas, resumen.TotalGuardadas, resumen.TotalRechazadas, (long)resumen.Duracion.TotalMilliseconds);
    }
}
