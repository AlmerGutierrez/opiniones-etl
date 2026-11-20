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
