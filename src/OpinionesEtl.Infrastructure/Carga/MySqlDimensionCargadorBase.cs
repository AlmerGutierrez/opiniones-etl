using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Application.Modelos;
using OpinionesEtl.Infrastructure.Datos;

namespace OpinionesEtl.Infrastructure.Carga;

/// <summary>
/// Consume procedimientos almacenados del Data Warehouse mediante ADO.NET (CommandType.StoredProcedure),
/// enviando los parámetros de entrada y leyendo los contadores de salida (OUT).
/// </summary>
public abstract class MySqlDimensionCargadorBase : ICargadorDimension
{
    private readonly IConexionFactory _fabrica;
    private readonly CargaDwOptions _opciones;
    private readonly ILogger _logger;

    protected MySqlDimensionCargadorBase(IConexionFactory fabrica, IOptions<CargaDwOptions> opciones, ILogger logger)
    {
        _fabrica = fabrica;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public abstract string Dimension { get; }

    protected abstract string Procedimiento { get; }

    protected virtual void AgregarParametrosEntrada(DbCommand comando) { }

    public async Task<ResultadoCarga> CargarAsync(Guid idEjecucion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DW: {Dimension}] Ejecutando procedimiento {Procedimiento}", Dimension, Procedimiento);

        await using var conexion = _fabrica.Crear(NombreConexion.DataWarehouse);
        await conexion.OpenAsync(cancellationToken);

        await using var comando = conexion.CreateCommand();
        comando.CommandText = Procedimiento;
        comando.CommandType = CommandType.StoredProcedure;
        comando.CommandTimeout = Math.Max(5, _opciones.TimeoutSegundos);

        AgregarEntrada(comando, "p_id_ejecucion", DbType.String, idEjecucion.ToString());
        AgregarParametrosEntrada(comando);

        var pLeidas = AgregarSalidaInt(comando, "p_leidas");
        var pInsertadas = AgregarSalidaInt(comando, "p_insertadas");
        var pActualizadas = AgregarSalidaInt(comando, "p_actualizadas");
        var pRechazadas = AgregarSalidaInt(comando, "p_rechazadas");

        await comando.ExecuteNonQueryAsync(cancellationToken);

        var resultado = new ResultadoCarga(
            LeerInt(pLeidas),
            LeerInt(pInsertadas),
            LeerInt(pActualizadas),
            LeerInt(pRechazadas));

        _logger.LogInformation(
            "[DW: {Dimension}] Completada: leídas={Leidas} insertadas={Insertadas} actualizadas={Actualizadas} rechazadas={Rechazadas}",
            Dimension, resultado.Leidas, resultado.Insertadas, resultado.Actualizadas, resultado.Rechazadas);

        return resultado;
    }

    protected static void AgregarEntrada(DbCommand comando, string nombre, DbType tipo, object? valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Direction = ParameterDirection.Input;
        parametro.DbType = tipo;
        parametro.Value = valor ?? DBNull.Value;
        comando.Parameters.Add(parametro);
    }

    private static DbParameter AgregarSalidaInt(DbCommand comando, string nombre)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Direction = ParameterDirection.Output;
        parametro.DbType = DbType.Int32;
        comando.Parameters.Add(parametro);
        return parametro;
    }

    private static int LeerInt(DbParameter parametro) =>
        parametro.Value is null or DBNull ? 0 : Convert.ToInt32(parametro.Value);
}