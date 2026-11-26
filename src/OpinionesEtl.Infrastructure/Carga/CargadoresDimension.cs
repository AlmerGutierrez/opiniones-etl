using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Infrastructure.Datos;

namespace OpinionesEtl.Infrastructure.Carga;

public sealed class DimSentimientoCargador : MySqlDimensionCargadorBase
{
    public DimSentimientoCargador(IConexionFactory fabrica, IOptions<CargaDwOptions> opciones, ILogger<DimSentimientoCargador> logger)
        : base(fabrica, opciones, logger) { }

    public override string Dimension => "dim_sentimiento";
    protected override string Procedimiento => "sp_cargar_dim_sentimiento";
}

public sealed class DimFuenteCargador : MySqlDimensionCargadorBase
{
    public DimFuenteCargador(IConexionFactory fabrica, IOptions<CargaDwOptions> opciones, ILogger<DimFuenteCargador> logger)
        : base(fabrica, opciones, logger) { }

    public override string Dimension => "dim_fuente";
    protected override string Procedimiento => "sp_cargar_dim_fuente";
}

public sealed class DimProductoCargador : MySqlDimensionCargadorBase
{
    public DimProductoCargador(IConexionFactory fabrica, IOptions<CargaDwOptions> opciones, ILogger<DimProductoCargador> logger)
        : base(fabrica, opciones, logger) { }

    public override string Dimension => "dim_producto";
    protected override string Procedimiento => "sp_cargar_dim_producto";
}

public sealed class DimClienteCargador : MySqlDimensionCargadorBase
{
    public DimClienteCargador(IConexionFactory fabrica, IOptions<CargaDwOptions> opciones, ILogger<DimClienteCargador> logger)
        : base(fabrica, opciones, logger) { }

    public override string Dimension => "dim_cliente";
    protected override string Procedimiento => "sp_cargar_dim_cliente";
}

public sealed class DimTiempoCargador : MySqlDimensionCargadorBase
{
    private readonly CargaDwOptions _opciones;

    public DimTiempoCargador(IConexionFactory fabrica, IOptions<CargaDwOptions> opciones, ILogger<DimTiempoCargador> logger)
        : base(fabrica, opciones, logger)
    {
        _opciones = opciones.Value;
    }

    public override string Dimension => "dim_tiempo";
    protected override string Procedimiento => "sp_cargar_dim_tiempo";

    protected override void AgregarParametrosEntrada(DbCommand comando)
    {
        AgregarEntrada(comando, "p_desde", DbType.Date, ParsearFecha(_opciones.TiempoDesde, "CargaDw:TiempoDesde"));
        AgregarEntrada(comando, "p_hasta", DbType.Date, ParsearFecha(_opciones.TiempoHasta, "CargaDw:TiempoHasta"));
    }

    private static object? ParsearFecha(string? texto, string clave)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }
        if (!DateOnly.TryParseExact(texto.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            throw new FormatException($"La fecha configurada en '{clave}' ('{texto}') no cumple el formato AAAA-MM-DD.");
        }
        return fecha.ToDateTime(TimeOnly.MinValue);
    }
}