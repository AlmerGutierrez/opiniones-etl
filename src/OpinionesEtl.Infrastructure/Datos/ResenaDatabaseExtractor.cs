using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Domain;
using OpinionesEtl.Infrastructure.Validacion;

namespace OpinionesEtl.Infrastructure.Datos;

/// <summary>Extrae las reseñas web de la base de datos relacional del sitio (consulta fija y de solo lectura).</summary>
public sealed class ResenaDatabaseExtractor : IExtractor<ResenaExtraida>
{
    private const string Consulta =
        "SELECT id_resena, id_cliente, id_producto, fecha, comentario, rating FROM resenas ORDER BY id_resena";

    private readonly IConexionFactory _fabrica;
    private readonly ILogger<ResenaDatabaseExtractor> _logger;

    public ResenaDatabaseExtractor(IConexionFactory fabrica, ILogger<ResenaDatabaseExtractor> logger)
    {
        _fabrica = fabrica;
        _logger = logger;
    }

    public string Nombre => "BD relacional: reseñas web";

    public async IAsyncEnumerable<FilaExtraida<ResenaExtraida>> ExtraerAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var conexion = _fabrica.Crear(NombreConexion.FuenteWeb);
        await conexion.OpenAsync(cancellationToken);
        _logger.LogInformation("[{Fuente}] Conexión abierta; ejecutando consulta", Nombre);

        await using var comando = conexion.CreateCommand();
        comando.CommandText = Consulta;
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        var fila = 0;
        while (await lector.ReadAsync(cancellationToken))
        {
            fila++;
            var v = new ValidadorFila();
            var id = v.Requerido(lector.IsDBNull(0) ? null : lector.GetString(0), "id_resena");
            var idCliente = v.Requerido(lector.IsDBNull(1) ? null : lector.GetString(1), "id_cliente");
            var idProducto = v.Requerido(lector.IsDBNull(2) ? null : lector.GetString(2), "id_producto");

            var fecha = default(DateOnly);
            if (lector.IsDBNull(3))
            {
                v.Error("fecha nula");
            }
            else
            {
                fecha = DateOnly.FromDateTime(lector.GetDateTime(3));
            }

            var comentario = v.Requerido(lector.IsDBNull(4) ? null : lector.GetString(4), "comentario");

            var rating = 0;
            if (lector.IsDBNull(5))
            {
                v.Error("rating nulo");
            }
            else
            {
                rating = v.Rango(Convert.ToInt32(lector.GetValue(5)), "rating", 1, 5);
            }

            yield return v.EsValida
                ? FilaExtraida<ResenaExtraida>.Valida(fila, new ResenaExtraida(id, idCliente, idProducto, fecha, comentario, rating))
                : FilaExtraida<ResenaExtraida>.Rechazada(fila, v.Motivo);
        }
    }
}
