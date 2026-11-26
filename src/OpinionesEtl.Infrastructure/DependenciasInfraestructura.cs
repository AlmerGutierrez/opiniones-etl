using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Application.Servicios;
using OpinionesEtl.Domain;
using OpinionesEtl.Infrastructure.Api;
using OpinionesEtl.Infrastructure.Carga;
using OpinionesEtl.Infrastructure.Csv;
using OpinionesEtl.Infrastructure.Datos;
using OpinionesEtl.Infrastructure.Staging;

namespace OpinionesEtl.Infrastructure;

public static class DependenciasInfraestructura
{
    /// <summary>
    /// Registra el pipeline según las fuentes habilitadas en appsettings.json. Agregar una fuente nueva es
    /// escribir su IExtractor, su IStagingWriter y añadir una línea de Registrar aquí.
    /// </summary>
    public static IServiceCollection AddInfraestructura(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.Configure<EjecucionOptions>(configuracion.GetSection("Ejecucion"));
        servicios.Configure<FuentesOptions>(configuracion.GetSection("Fuentes"));
        servicios.Configure<CargaDwOptions>(configuracion.GetSection("CargaDw"));
        var fuentes = configuracion.GetSection("Fuentes").Get<FuentesOptions>() ?? new FuentesOptions();

        servicios.AddSingleton<IConexionFactory, MySqlConexionFactory>();
        servicios.AddSingleton<IStagingMantenimiento, MySqlStagingMantenimiento>();
        servicios.AddSingleton<OrquestadorExtraccion>();
        servicios.AddSingleton<OrquestadorCarga>();

        servicios.AddSingleton<ICargadorDimension, DimSentimientoCargador>();
        servicios.AddSingleton<ICargadorDimension, DimFuenteCargador>();
        servicios.AddSingleton<ICargadorDimension, DimTiempoCargador>();
        servicios.AddSingleton<ICargadorDimension, DimProductoCargador>();
        servicios.AddSingleton<ICargadorDimension, DimClienteCargador>();

        if (fuentes.CsvClientes.Habilitada) Registrar<ClienteExtraido, ClienteCsvExtractor, ClienteStagingWriter>(servicios);
        if (fuentes.CsvProductos.Habilitada) Registrar<ProductoExtraido, ProductoCsvExtractor, ProductoStagingWriter>(servicios);
        if (fuentes.CsvEncuestas.Habilitada) Registrar<EncuestaExtraida, EncuestaCsvExtractor, EncuestaStagingWriter>(servicios);
        if (fuentes.BaseDatosResenas.Habilitada) Registrar<ResenaExtraida, ResenaDatabaseExtractor, ResenaStagingWriter>(servicios);
        if (fuentes.ApiComentarios.Habilitada)
        {
            Registrar<ComentarioSocialExtraido, ComentarioSocialApiExtractor, ComentarioSocialStagingWriter>(servicios);
            servicios.AddHttpClient(ComentarioSocialApiExtractor.NombreCliente, (proveedor, cliente) =>
            {
                var opciones = proveedor.GetRequiredService<IOptions<FuentesOptions>>().Value.ApiComentarios;
                cliente.BaseAddress = new Uri(opciones.BaseUrl.TrimEnd('/') + "/");
                cliente.Timeout = TimeSpan.FromSeconds(opciones.TimeoutSegundos);
                if (!string.IsNullOrWhiteSpace(opciones.ApiKey))
                {
                    cliente.DefaultRequestHeaders.Add("X-API-Key", opciones.ApiKey);
                }
            });
        }

        return servicios;
    }

    private static void Registrar<T, TExtractor, TWriter>(IServiceCollection servicios)
        where T : class
        where TExtractor : class, IExtractor<T>
        where TWriter : class, IStagingWriter<T>
    {
        servicios.AddSingleton<IExtractor<T>, TExtractor>();
        servicios.AddSingleton<IStagingWriter<T>, TWriter>();
        servicios.AddSingleton<ITareaExtraccion, TareaExtraccion<T>>();
    }
}