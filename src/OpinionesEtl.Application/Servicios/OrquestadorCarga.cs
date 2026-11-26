using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Application.Modelos;

namespace OpinionesEtl.Application.Servicios;

/// <summary>Ejecuta la carga de todas las dimensiones en paralelo con aislamiento de fallos por dimensión.</summary>
public sealed class OrquestadorCarga
{
    private readonly IReadOnlyList<ICargadorDimension> _cargadores;
    private readonly EjecucionOptions _ejecucion;
    private readonly ILogger<OrquestadorCarga> _logger;

    public OrquestadorCarga(IEnumerable<ICargadorDimension> cargadores,
                            IOptions<EjecucionOptions> ejecucion,
                            ILogger<OrquestadorCarga> logger)
    {
        _cargadores = cargadores.ToList();
        _ejecucion = ejecucion.Value;
        _logger = logger;
    }

    public async Task<ResumenCarga> EjecutarAsync(Guid idEjecucion, CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();
        _logger.LogInformation("Carga de dimensiones DW (ejecución {Id}) iniciada para {Cantidad} dimensión(es)",
            idEjecucion, _cargadores.Count);

        using var semaforo = new SemaphoreSlim(Math.Max(1, _ejecucion.MaxParalelismo));
        var tareas = _cargadores.Select(async cargador =>
        {
            await semaforo.WaitAsync(cancellationToken);
            var reloj = Stopwatch.StartNew();
            try
            {
                var res = await cargador.CargarAsync(idEjecucion, cancellationToken);
                return new ResumenCargaDimension(
                    cargador.Dimension, res.Leidas, res.Insertadas, res.Actualizadas, res.Rechazadas, reloj.Elapsed);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando dimensión {Dimension}", cargador.Dimension);
                return new ResumenCargaDimension(cargador.Dimension, 0, 0, 0, 0, reloj.Elapsed, ex.Message);
            }
            finally
            {
                semaforo.Release();
            }
        }).ToList();

        var resultados = await Task.WhenAll(tareas);
        var resumen = new ResumenCarga(idEjecucion, resultados, cronometro.Elapsed);
        RegistrarResumen(resumen);
        return resumen;
    }

    private void RegistrarResumen(ResumenCarga resumen)
    {
        _logger.LogInformation("=== RESUMEN DE CARGA DE DIMENSIONES DW (ejecución {Id}) ===", resumen.IdEjecucion);
        foreach (var d in resumen.Dimensiones)
        {
            _logger.LogInformation(
                "{Dimension,-20} leídas={Leidas,5} insertadas={Insertadas,5} actualizadas={Actualizadas,4} rechazadas={Rechazadas,4} {Ms,6} ms {Estado}",
                d.Dimension, d.Leidas, d.Insertadas, d.Actualizadas, d.Rechazadas,
                (long)d.Duracion.TotalMilliseconds, d.Fallo ? "FALLÓ: " + d.Error : "OK");
        }
        _logger.LogInformation(
            "TOTAL DW leídas={Leidas} insertadas={Insertadas} actualizadas={Actualizadas} rechazadas={Rechazadas} en {Ms} ms",
            resumen.TotalLeidas, resumen.TotalInsertadas, resumen.TotalActualizadas, resumen.TotalRechazadas,
            (long)resumen.Duracion.TotalMilliseconds);
    }
}
