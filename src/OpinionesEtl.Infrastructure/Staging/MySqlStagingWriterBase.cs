using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Logging;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Infrastructure.Datos;

namespace OpinionesEtl.Infrastructure.Staging;

/// <summary>
/// Inserta lotes en una tabla de staging con una sola sentencia INSERT de varias filas dentro de una transacción
/// (un viaje a la base por lote, no uno por fila). Los nombres de tabla y columna son constantes del código;
/// los valores siempre viajan como parámetros.
/// </summary>
public abstract class MySqlStagingWriterBase<T> : IStagingWriter<T> where T : class
{
    private readonly IConexionFactory _fabrica;
    private readonly ILogger _logger;

    protected MySqlStagingWriterBase(IConexionFactory fabrica, ILogger logger)
    {
        _fabrica = fabrica;
        _logger = logger;
    }

    protected abstract string Tabla { get; }

    protected abstract string[] Columnas { get; }

    protected abstract object?[] Valores(T registro);

    public async Task<int> GuardarLoteAsync(IReadOnlyList<T> lote, Guid idEjecucion, CancellationToken cancellationToken = default)
    {
        if (lote.Count == 0)
        {
            return 0;
        }

        await using var conexion = _fabrica.Crear(NombreConexion.Staging);
        await conexion.OpenAsync(cancellationToken);
        await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);
        await using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;

        var sql = new StringBuilder($"INSERT INTO {Tabla} ({string.Join(", ", Columnas)}, id_ejecucion, fecha_extraccion) VALUES ");
        var indice = 0;
        for (var i = 0; i < lote.Count; i++)
        {
            if (i > 0)
            {
                sql.Append(", ");
            }
            sql.Append('(');
            foreach (var valor in Valores(lote[i]))
            {
                var nombre = "@p" + indice++;
                sql.Append(nombre).Append(", ");
                Agregar(comando, nombre, valor);
            }
            var nombreEjecucion = "@p" + indice++;
            Agregar(comando, nombreEjecucion, idEjecucion.ToString());
            sql.Append(nombreEjecucion).Append(", UTC_TIMESTAMP())");
        }

        comando.CommandText = sql.ToString();
        var insertadas = await comando.ExecuteNonQueryAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        _logger.LogDebug("Lote de {Cantidad} filas guardado en {Tabla}", insertadas, Tabla);
        return insertadas;
    }

    private static void Agregar(DbCommand comando, string nombre, object? valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Value = valor ?? DBNull.Value;
        comando.Parameters.Add(parametro);
    }
}
