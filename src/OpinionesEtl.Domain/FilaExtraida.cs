namespace OpinionesEtl.Domain;

/// <summary>Resultado de leer una fila de una fuente: o un registro válido, o el motivo por el que se rechazó.</summary>
public sealed record FilaExtraida<T>(int NumeroFila, T? Registro, string? MotivoRechazo) where T : class
{
    public bool EsValida => MotivoRechazo is null;

    public static FilaExtraida<T> Valida(int numeroFila, T registro) => new(numeroFila, registro, null);

    public static FilaExtraida<T> Rechazada(int numeroFila, string motivo) => new(numeroFila, null, motivo);
}
