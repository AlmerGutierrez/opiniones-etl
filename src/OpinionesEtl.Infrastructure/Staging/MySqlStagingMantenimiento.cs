using System.Data.Common;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Infrastructure.Datos;

namespace OpinionesEtl.Infrastructure.Staging;

public sealed class MySqlStagingMantenimiento : IStagingMantenimiento
{
    // Lista fija: los nombres de tabla nunca vienen de fuera.
    private static readonly string[] TablasDatos = { "stg_cliente", "stg_producto", "stg_encuesta", "stg_resena", "stg_comentario_social" };

    private readonly IConexionFactory _fabrica;

    public MySqlStagingMantenimiento(IConexionFactory fabrica) => _fabrica = fabrica;

    public async Task PrepararAsync(Guid idEjecucion, bool limpiar, CancellationToken cancellationToken = default)
    {
        await using var conexion = _fabrica.Crear(NombreConexion.Staging);
        await conexion.OpenAsync(cancellationToken);

        if (limpiar)
        {
            foreach (var tabla in TablasDatos)
            {
                await EjecutarAsync(conexion, $"TRUNCATE TABLE {tabla}", cancellationToken);
            }
        }

        await EjecutarAsync(conexion,
            "INSERT INTO stg_ejecucion (id_ejecucion, inicio, estado) VALUES (@id, UTC_TIMESTAMP(), 'EnCurso')",
            cancellationToken, ("@id", idEjecucion.ToString()));
    }

    public async Task RegistrarRechazoAsync(Guid idEjecucion, string fuente, int fila, string motivo, CancellationToken cancellationToken = default)
    {
        await using var conexion = _fabrica.Crear(NombreConexion.Staging);
        await conexion.OpenAsync(cancellationToken);
        await EjecutarAsync(conexion,
            "INSERT INTO stg_rechazo (id_ejecucion, fuente, fila, motivo, fecha_registro) VALUES (@id, @fuente, @fila, @motivo, UTC_TIMESTAMP())",
            cancellationToken, ("@id", idEjecucion.ToString()), ("@fuente", fuente), ("@fila", fila),
            ("@motivo", motivo.Length > 255 ? motivo[..255] : motivo));
    }

    public async Task FinalizarAsync(Guid idEjecucion, string estado, CancellationToken cancellationToken = default)
    {
        await using var conexion = _fabrica.Crear(NombreConexion.Staging);
        await conexion.OpenAsync(cancellationToken);
        await EjecutarAsync(conexion,
            "UPDATE stg_ejecucion SET fin = UTC_TIMESTAMP(), estado = @estado WHERE id_ejecucion = @id",
            cancellationToken, ("@estado", estado), ("@id", idEjecucion.ToString()));
    }

    private static async Task EjecutarAsync(DbConnection conexion, string sql, CancellationToken cancellationToken,
                                            params (string Nombre, object Valor)[] parametros)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            var parametro = comando.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor;
            comando.Parameters.Add(parametro);
        }
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }
}
