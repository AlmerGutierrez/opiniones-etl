namespace OpinionesEtl.Infrastructure;

/// <summary>
/// Resuelve rutas relativas contra la raíz de la solución (la carpeta que contiene OpinionesEtl.sln),
/// de modo que funcionen sin importar desde qué carpeta se ejecute el programa.
/// </summary>
public static class RutaSolucion
{
    public static string Resolver(string ruta)
    {
        if (Path.IsPathRooted(ruta))
        {
            return ruta;
        }

        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "OpinionesEtl.sln")))
            {
                return Path.GetFullPath(Path.Combine(directorio.FullName, ruta));
            }
            directorio = directorio.Parent;
        }

        return Path.GetFullPath(ruta);
    }
}
