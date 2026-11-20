using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Application.Modelos;

namespace OpinionesEtl.Application.Servicios;

/// <summary>
/// Extrae una fuente y la guarda en staging por lotes. Los datos se procesan en flujo (no se cargan completos
/// en memoria), así que el consumo de memoria depende del tamaño del lote y no del volumen de la fuente.
/// </summary>
public sealed class TareaExtraccion<T> : ITareaExtraccion where T : class
{
    private readonly IExtractor<T> _extractor;
    private readonly IStagingWriter<T> _writer;
    private readonly IStagingMantenimiento _mantenimiento;
    private readonly EjecucionOptions _opciones;
    private readonly ILogger<TareaExtraccion<T>> _logger;

    public TareaExtraccion(IExtractor<T> extractor, IStagingWriter<T> writer, IStagingMantenimiento mantenimiento,
                           IOptions<EjecucionOptions> opciones, ILogger<TareaExtraccion<T>> logger)
    {
        _extractor = extractor;
        _writer = writer;
        _mantenimiento = mantenimiento;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public string Nombre => _extractor.Nombre;

    public async Task<ResumenTarea> EjecutarAsync(Guid idEjecucion, CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();
        int leidas = 0, guardadas = 0, rechazadas = 0;
        var tamanoLote = Math.Max(1, _opciones.TamanoLote);
        var lote = new List<T>(tamanoLote);
        _logger.LogInformation("[{Fuente}] Extracción iniciada", Nombre);

        try
        {
            await foreach (var fila in _extractor.ExtraerAsync(cancellationToken).WithCancellation(cancellationToken))
            {
                leidas++;
                if (!fila.EsValida)
                {
                    rechazadas++;
                    _logger.LogWarning("[{Fuente}] Fila {Fila} rechazada: {Motivo}", Nombre, fila.NumeroFila, fila.MotivoRechazo);
                    await _mantenimiento.RegistrarRechazoAsync(idEjecucion, Nombre, fila.NumeroFila, fila.MotivoRechazo!, cancellationToken);
                    continue;
                }

                lote.Add(fila.Registro!);
                if (lote.Count >= tamanoLote)
                {
                    guardadas += await _writer.GuardarLoteAsync(lote, idEjecucion, cancellationToken);
                    lote.Clear();
                }
            }

            if (lote.Count > 0)
            {
                guardadas += await _writer.GuardarLoteAsync(lote, idEjecucion, cancellationToken);
            }

            _logger.LogInformation("[{Fuente}] Completada: leídas={Leidas} guardadas={Guardadas} rechazadas={Rechazadas} en {Ms} ms",
                Nombre, leidas, guardadas, rechazadas, cronometro.ElapsedMilliseconds);
            return new ResumenTarea(Nombre, leidas, guardadas, rechazadas, cronometro.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Un fallo en una fuente no detiene a las demás: se registra y se informa en el resumen.
            _logger.LogError(ex, "[{Fuente}] La extracción falló", Nombre);
            return new ResumenTarea(Nombre, leidas, guardadas, rechazadas, cronometro.Elapsed, ex.Message);
        }
    }
}
