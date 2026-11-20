using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Domain;
using OpinionesEtl.Infrastructure.Validacion;

namespace OpinionesEtl.Infrastructure.Api;

/// <summary>
/// Consume la API REST de comentarios de redes sociales con IHttpClientFactory: recorre las páginas hasta agotarlas,
/// permite extracción incremental (parámetro "desde") y reintenta con espera exponencial ante fallos transitorios.
/// </summary>
public sealed class ComentarioSocialApiExtractor : IExtractor<ComentarioSocialExtraido>
{
    public const string NombreCliente = "opiniones-api";

    private readonly IHttpClientFactory _fabrica;
    private readonly FuenteApiOptions _opciones;
    private readonly ILogger<ComentarioSocialApiExtractor> _logger;

    public ComentarioSocialApiExtractor(IHttpClientFactory fabrica, IOptions<FuentesOptions> opciones,
                                        ILogger<ComentarioSocialApiExtractor> logger)
    {
        _fabrica = fabrica;
        _opciones = opciones.Value.ApiComentarios;
        _logger = logger;
    }

    public string Nombre => "API REST: comentarios sociales";

    public async IAsyncEnumerable<FilaExtraida<ComentarioSocialExtraido>> ExtraerAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var cliente = _fabrica.CreateClient(NombreCliente);
        var tamano = Math.Clamp(_opciones.TamanoPagina, 1, 500);
        var filtroDesde = string.IsNullOrWhiteSpace(_opciones.Desde) ? string.Empty : $"&desde={_opciones.Desde}";

        for (var pagina = 1; ; pagina++)
        {
            var url = $"api/v1/comentarios-sociales?pagina={pagina}&tamano={tamano}{filtroDesde}";
            var respuesta = await ObtenerPaginaAsync(cliente, url, cancellationToken);
            _logger.LogInformation("[{Fuente}] Página {Pagina}: {Cantidad} comentarios (total en la fuente: {Total})",
                Nombre, pagina, respuesta.Datos.Count, respuesta.Total);

            var indice = 0;
            foreach (var dto in respuesta.Datos)
            {
                indice++;
                yield return Mapear((pagina - 1) * tamano + indice, dto);
            }

            if (!respuesta.HayMas)
            {
                yield break;
            }
        }
    }

    private async Task<PaginaComentariosDto> ObtenerPaginaAsync(HttpClient cliente, string url, CancellationToken cancellationToken)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                using var respuesta = await cliente.GetAsync(url, cancellationToken);

                if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    // Un error de credenciales no se arregla reintentando.
                    throw new InvalidOperationException("La API rechazó la clave (HTTP 401/403). Revise Fuentes:ApiComentarios:ApiKey en los secretos.");
                }

                if ((int)respuesta.StatusCode >= 500 || respuesta.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new HttpRequestException($"La API respondió HTTP {(int)respuesta.StatusCode}.", null, respuesta.StatusCode);
                }

                respuesta.EnsureSuccessStatusCode();
                var pagina = await respuesta.Content.ReadFromJsonAsync<PaginaComentariosDto>(cancellationToken: cancellationToken);
                return pagina ?? throw new InvalidDataException("La API devolvió una respuesta vacía.");
            }
            catch (Exception ex) when (EsTransitorio(ex, cancellationToken) && intento <= _opciones.Reintentos)
            {
                var espera = TimeSpan.FromSeconds(Math.Pow(2, intento - 1));
                _logger.LogWarning("[{Fuente}] Intento {Intento} de {Total} falló ({Motivo}). Reintentando en {Espera} s",
                    Nombre, intento, _opciones.Reintentos + 1, ex.Message, espera.TotalSeconds);
                await Task.Delay(espera, cancellationToken);
            }
        }
    }

    private static bool EsTransitorio(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        return ex is TaskCanceledException
            || (ex is HttpRequestException http && (http.StatusCode is null || (int)http.StatusCode >= 500 || http.StatusCode == HttpStatusCode.TooManyRequests));
    }

    private static FilaExtraida<ComentarioSocialExtraido> Mapear(int numeroFila, ComentarioSocialDto dto)
    {
        var v = new ValidadorFila();
        var id = v.Requerido(dto.IdComentario, "id_comentario");
        var producto = v.Requerido(dto.IdProducto, "id_producto");
        var canal = v.Catalogo(dto.Canal, "canal", "Facebook", "Instagram", "Twitter");
        var fecha = v.Fecha(dto.Fecha, "fecha");
        var comentario = v.Requerido(dto.Comentario, "comentario");
        var cliente = string.IsNullOrWhiteSpace(dto.IdCliente) ? null : dto.IdCliente.Trim();   // sin cliente = comentario anónimo

        return v.EsValida
            ? FilaExtraida<ComentarioSocialExtraido>.Valida(numeroFila, new ComentarioSocialExtraido(id, cliente, producto, canal, fecha, comentario))
            : FilaExtraida<ComentarioSocialExtraido>.Rechazada(numeroFila, v.Motivo);
    }
}

internal sealed class ComentarioSocialDto
{
    [JsonPropertyName("id_comentario")] public string? IdComentario { get; set; }
    [JsonPropertyName("id_cliente")] public string? IdCliente { get; set; }
    [JsonPropertyName("id_producto")] public string? IdProducto { get; set; }
    [JsonPropertyName("canal")] public string? Canal { get; set; }
    [JsonPropertyName("fecha")] public string? Fecha { get; set; }
    [JsonPropertyName("comentario")] public string? Comentario { get; set; }
}

internal sealed class PaginaComentariosDto
{
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("pagina")] public int Pagina { get; set; }
    [JsonPropertyName("tamano")] public int Tamano { get; set; }
    [JsonPropertyName("hay_mas")] public bool HayMas { get; set; }
    [JsonPropertyName("datos")] public List<ComentarioSocialDto> Datos { get; set; } = new();
}
