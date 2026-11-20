using OpinionesEtl.Application.Modelos;
using OpinionesEtl.Domain;

namespace OpinionesEtl.Application.Contratos;

/// <summary>Una fuente de datos. Para agregar otra fuente se implementa esta interfaz, sin tocar el resto.</summary>
public interface IExtractor<T> where T : class
{
    string Nombre { get; }
    IAsyncEnumerable<FilaExtraida<T>> ExtraerAsync(CancellationToken cancellationToken = default);
}

/// <summary>Escribe un lote de registros en la zona de aterrizaje (staging).</summary>
public interface IStagingWriter<T> where T : class
{
    Task<int> GuardarLoteAsync(IReadOnlyList<T> lote, Guid idEjecucion, CancellationToken cancellationToken = default);
}

/// <summary>Operaciones de control del staging: preparar la ejecución, registrar rechazos y cerrarla.</summary>
public interface IStagingMantenimiento
{
    Task PrepararAsync(Guid idEjecucion, bool limpiar, CancellationToken cancellationToken = default);
    Task RegistrarRechazoAsync(Guid idEjecucion, string fuente, int fila, string motivo, CancellationToken cancellationToken = default);
    Task FinalizarAsync(Guid idEjecucion, string estado, CancellationToken cancellationToken = default);
}

/// <summary>Unidad de trabajo del orquestador: extrae una fuente y la deja en staging.</summary>
public interface ITareaExtraccion
{
    string Nombre { get; }
    Task<ResumenTarea> EjecutarAsync(Guid idEjecucion, CancellationToken cancellationToken = default);
}
