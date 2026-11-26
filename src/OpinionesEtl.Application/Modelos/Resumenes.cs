namespace OpinionesEtl.Application.Modelos;

public sealed record ResumenTarea(
    string Nombre, int Leidas, int Guardadas, int Rechazadas, TimeSpan Duracion, string? Error = null)
{
    public bool Fallo => Error is not null;
}

public sealed record ResumenEjecucion(Guid IdEjecucion, IReadOnlyList<ResumenTarea> Tareas, TimeSpan Duracion)
{
    public bool ConErrores => Tareas.Any(t => t.Fallo);
    public int TotalLeidas => Tareas.Sum(t => t.Leidas);
    public int TotalGuardadas => Tareas.Sum(t => t.Guardadas);
    public int TotalRechazadas => Tareas.Sum(t => t.Rechazadas);
}

public sealed record ResultadoCarga(int Leidas, int Insertadas, int Actualizadas, int Rechazadas);

public sealed record ResumenCargaDimension(
    string Dimension, int Leidas, int Insertadas, int Actualizadas, int Rechazadas, TimeSpan Duracion, string? Error = null)
{
    public bool Fallo => Error is not null;
}

public sealed record ResumenCarga(Guid IdEjecucion, IReadOnlyList<ResumenCargaDimension> Dimensiones, TimeSpan Duracion)
{
    public bool ConErrores => Dimensiones.Any(d => d.Fallo);
    public int TotalLeidas => Dimensiones.Sum(d => d.Leidas);
    public int TotalInsertadas => Dimensiones.Sum(d => d.Insertadas);
    public int TotalActualizadas => Dimensiones.Sum(d => d.Actualizadas);
    public int TotalRechazadas => Dimensiones.Sum(d => d.Rechazadas);
}