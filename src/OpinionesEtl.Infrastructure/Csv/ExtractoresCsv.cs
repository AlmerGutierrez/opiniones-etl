using CsvHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Domain;
using OpinionesEtl.Infrastructure.Validacion;

namespace OpinionesEtl.Infrastructure.Csv;

public sealed class ClienteCsvExtractor : CsvExtractorBase<ClienteExtraido>
{
    public ClienteCsvExtractor(IOptions<FuentesOptions> opciones, ILogger<ClienteCsvExtractor> logger)
        : base("CSV: clientes", opciones.Value.CsvClientes, logger) { }

    protected override IReadOnlyList<string> ColumnasRequeridas { get; } = new[] { "IdCliente", "Nombre", "Email" };

    protected override FilaExtraida<ClienteExtraido> Mapear(int numeroFila, CsvReader csv)
    {
        var v = new ValidadorFila();
        var id = v.Requerido(Campo(csv, "IdCliente"), "IdCliente");
        var nombre = v.Requerido(Campo(csv, "Nombre"), "Nombre");
        var email = v.Requerido(Campo(csv, "Email"), "Email");
        if (email.Length > 0 && !email.Contains('@'))
        {
            v.Error($"Email con formato inválido ('{email}')");
        }

        return v.EsValida
            ? FilaExtraida<ClienteExtraido>.Valida(numeroFila, new ClienteExtraido(id, nombre, email))
            : FilaExtraida<ClienteExtraido>.Rechazada(numeroFila, v.Motivo);
    }
}

public sealed class ProductoCsvExtractor : CsvExtractorBase<ProductoExtraido>
{
    public ProductoCsvExtractor(IOptions<FuentesOptions> opciones, ILogger<ProductoCsvExtractor> logger)
        : base("CSV: productos", opciones.Value.CsvProductos, logger) { }

    protected override IReadOnlyList<string> ColumnasRequeridas { get; } = new[] { "IdProducto", "Nombre", "Categoría" };

    protected override FilaExtraida<ProductoExtraido> Mapear(int numeroFila, CsvReader csv)
    {
        var v = new ValidadorFila();
        var id = v.Requerido(Campo(csv, "IdProducto"), "IdProducto");
        var nombre = v.Requerido(Campo(csv, "Nombre"), "Nombre");
        var categoria = v.Requerido(Campo(csv, "Categoría"), "Categoría");

        return v.EsValida
            ? FilaExtraida<ProductoExtraido>.Valida(numeroFila, new ProductoExtraido(id, nombre, categoria))
            : FilaExtraida<ProductoExtraido>.Rechazada(numeroFila, v.Motivo);
    }
}

public sealed class EncuestaCsvExtractor : CsvExtractorBase<EncuestaExtraida>
{
    public EncuestaCsvExtractor(IOptions<FuentesOptions> opciones, ILogger<EncuestaCsvExtractor> logger)
        : base("CSV: encuestas internas", opciones.Value.CsvEncuestas, logger) { }

    protected override IReadOnlyList<string> ColumnasRequeridas { get; } = new[]
    {
        "IdOpinion", "IdCliente", "IdProducto", "Fecha", "Comentario", "Clasificación", "PuntajeSatisfacción", "Fuente",
    };

    protected override FilaExtraida<EncuestaExtraida> Mapear(int numeroFila, CsvReader csv)
    {
        var v = new ValidadorFila();
        var idOpinion = v.Requerido(Campo(csv, "IdOpinion"), "IdOpinion");
        var idCliente = v.Requerido(Campo(csv, "IdCliente"), "IdCliente");
        var idProducto = v.Requerido(Campo(csv, "IdProducto"), "IdProducto");
        var fecha = v.Fecha(Campo(csv, "Fecha"), "Fecha");
        var comentario = v.Requerido(Campo(csv, "Comentario"), "Comentario");
        var clasificacion = v.Catalogo(Campo(csv, "Clasificación"), "Clasificación", "Positiva", "Neutra", "Negativa");
        var puntaje = v.Entero(Campo(csv, "PuntajeSatisfacción"), "PuntajeSatisfacción", 1, 5);
        var fuente = v.Requerido(Campo(csv, "Fuente"), "Fuente");

        return v.EsValida
            ? FilaExtraida<EncuestaExtraida>.Valida(numeroFila,
                new EncuestaExtraida(idOpinion, idCliente, idProducto, fecha, comentario, clasificacion, puntaje, fuente))
            : FilaExtraida<EncuestaExtraida>.Rechazada(numeroFila, v.Motivo);
    }
}
