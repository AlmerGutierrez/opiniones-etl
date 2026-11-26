namespace OpinionesEtl.Application.Configuracion;

public sealed class EjecucionOptions
{
    public string Modo { get; set; } = "UnaVez";            // UnaVez o Periodico
    public int IntervaloMinutos { get; set; } = 60;
    public int MaxParalelismo { get; set; } = 3;             // fuentes extraídas al mismo tiempo
    public int TamanoLote { get; set; } = 500;               // filas por inserción en staging
    public bool LimpiarStagingAlIniciar { get; set; } = true;
}

public sealed class FuenteCsvOptions
{
    public bool Habilitada { get; set; } = true;
    public string Ruta { get; set; } = string.Empty;         // relativa a la raíz de la solución, o absoluta
}

public sealed class FuenteBaseDatosOptions
{
    public bool Habilitada { get; set; } = true;
}

public sealed class FuenteApiOptions
{
    public bool Habilitada { get; set; } = true;
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiKey { get; set; } = string.Empty;       // nunca en appsettings.json: va en user-secrets o variable de entorno
    public int TamanoPagina { get; set; } = 200;
    public string? Desde { get; set; }                       // AAAA-MM-DD: extracción incremental
    public int TimeoutSegundos { get; set; } = 30;
    public int Reintentos { get; set; } = 3;
}

public sealed class FuentesOptions
{
    public FuenteCsvOptions CsvClientes { get; set; } = new();
    public FuenteCsvOptions CsvProductos { get; set; } = new();
    public FuenteCsvOptions CsvEncuestas { get; set; } = new();
    public FuenteBaseDatosOptions BaseDatosResenas { get; set; } = new();
    public FuenteApiOptions ApiComentarios { get; set; } = new();
}

public sealed class CargaDwOptions
{
    public bool Habilitada { get; set; } = true;
    public string? TiempoDesde { get; set; }                 // AAAA-MM-DD o null para deducir de staging
    public string? TiempoHasta { get; set; }                 // AAAA-MM-DD o null para deducir de staging
    public int TimeoutSegundos { get; set; } = 60;
}