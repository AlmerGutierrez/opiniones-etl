using System.Data.Common;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace OpinionesEtl.Infrastructure.Datos;

public static class NombreConexion
{
    public const string Staging = "Staging";
    public const string FuenteWeb = "FuenteWeb";
    public const string DataWarehouse = "DataWarehouse";
}

/// <summary>Crea conexiones a partir de las cadenas de conexión de la configuración. Las cadenas con contraseña
/// no están en appsettings.json: se leen de user-secrets o de variables de entorno (ConnectionStrings__Staging).</summary>
public interface IConexionFactory
{
    DbConnection Crear(string nombre);
}

public sealed class MySqlConexionFactory : IConexionFactory
{
    private readonly IConfiguration _configuracion;

    public MySqlConexionFactory(IConfiguration configuracion) => _configuracion = configuracion;

    public DbConnection Crear(string nombre)
    {
        var cadena = _configuracion.GetConnectionString(nombre);
        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión '{nombre}'. Configúrela con 'dotnet user-secrets' o con la variable de entorno ConnectionStrings__{nombre}.");
        }
        return new MySqlConnection(cadena);
    }
}