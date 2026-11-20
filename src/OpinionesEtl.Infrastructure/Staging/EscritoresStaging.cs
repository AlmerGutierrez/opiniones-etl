using Microsoft.Extensions.Logging;
using OpinionesEtl.Domain;
using OpinionesEtl.Infrastructure.Datos;

namespace OpinionesEtl.Infrastructure.Staging;

public sealed class ClienteStagingWriter : MySqlStagingWriterBase<ClienteExtraido>
{
    public ClienteStagingWriter(IConexionFactory fabrica, ILogger<ClienteStagingWriter> logger) : base(fabrica, logger) { }
    protected override string Tabla => "stg_cliente";
    protected override string[] Columnas { get; } = { "id_cliente", "nombre", "email" };
    protected override object?[] Valores(ClienteExtraido r) => new object?[] { r.IdCliente, r.Nombre, r.Email };
}

public sealed class ProductoStagingWriter : MySqlStagingWriterBase<ProductoExtraido>
{
    public ProductoStagingWriter(IConexionFactory fabrica, ILogger<ProductoStagingWriter> logger) : base(fabrica, logger) { }
    protected override string Tabla => "stg_producto";
    protected override string[] Columnas { get; } = { "id_producto", "nombre", "categoria" };
    protected override object?[] Valores(ProductoExtraido r) => new object?[] { r.IdProducto, r.Nombre, r.Categoria };
}

public sealed class EncuestaStagingWriter : MySqlStagingWriterBase<EncuestaExtraida>
{
    public EncuestaStagingWriter(IConexionFactory fabrica, ILogger<EncuestaStagingWriter> logger) : base(fabrica, logger) { }
    protected override string Tabla => "stg_encuesta";
    protected override string[] Columnas { get; } =
        { "id_opinion", "id_cliente", "id_producto", "fecha", "comentario", "clasificacion", "puntaje", "fuente" };
    protected override object?[] Valores(EncuestaExtraida r) => new object?[]
        { r.IdOpinion, r.IdCliente, r.IdProducto, r.Fecha.ToDateTime(TimeOnly.MinValue), r.Comentario, r.Clasificacion, r.PuntajeSatisfaccion, r.Fuente };
}

public sealed class ResenaStagingWriter : MySqlStagingWriterBase<ResenaExtraida>
{
    public ResenaStagingWriter(IConexionFactory fabrica, ILogger<ResenaStagingWriter> logger) : base(fabrica, logger) { }
    protected override string Tabla => "stg_resena";
    protected override string[] Columnas { get; } = { "id_resena", "id_cliente", "id_producto", "fecha", "comentario", "rating" };
    protected override object?[] Valores(ResenaExtraida r) => new object?[]
        { r.IdResena, r.IdCliente, r.IdProducto, r.Fecha.ToDateTime(TimeOnly.MinValue), r.Comentario, r.Rating };
}

public sealed class ComentarioSocialStagingWriter : MySqlStagingWriterBase<ComentarioSocialExtraido>
{
    public ComentarioSocialStagingWriter(IConexionFactory fabrica, ILogger<ComentarioSocialStagingWriter> logger) : base(fabrica, logger) { }
    protected override string Tabla => "stg_comentario_social";
    protected override string[] Columnas { get; } = { "id_comentario", "id_cliente", "id_producto", "canal", "fecha", "comentario" };
    protected override object?[] Valores(ComentarioSocialExtraido r) => new object?[]
        { r.IdComentario, r.IdCliente, r.IdProducto, r.Canal, r.Fecha.ToDateTime(TimeOnly.MinValue), r.Comentario };
}
