using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using OpinionesEtl.Application.Configuracion;
using OpinionesEtl.Application.Contratos;
using OpinionesEtl.Domain;

namespace OpinionesEtl.Infrastructure.Csv;

/// <summary>
/// Lectura de un CSV en flujo, fila por fila. Valida que el archivo exista y traiga las columnas esperadas;
/// cada clase hija solo define qué columnas necesita y cómo convertir una fila en un registro.
/// </summary>
public abstract class CsvExtractorBase<T> : IExtractor<T> where T : class
{
    private readonly FuenteCsvOptions _opciones;
    private readonly ILogger _logger;

    protected CsvExtractorBase(string nombre, FuenteCsvOptions opciones, ILogger logger)
    {
        Nombre = nombre;
        _opciones = opciones;
        _logger = logger;
    }

    public string Nombre { get; }

    protected abstract IReadOnlyList<string> ColumnasRequeridas { get; }

    protected abstract FilaExtraida<T> Mapear(int numeroFila, CsvReader csv);

    protected static string Campo(CsvReader csv, string columna) => csv.GetField(columna) ?? string.Empty;

    public async IAsyncEnumerable<FilaExtraida<T>> ExtraerAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ruta = RutaSolucion.Resolver(_opciones.Ruta);
        if (!File.Exists(ruta))
        {
            throw new FileNotFoundException($"No se encontró el archivo CSV de '{Nombre}'.", ruta);
        }

        _logger.LogInformation("[{Fuente}] Leyendo {Ruta}", Nombre, ruta);
        using var lector = new StreamReader(ruta, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var configuracion = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        };
        using var csv = new CsvReader(lector, configuracion);

        if (!await csv.ReadAsync())
        {
            yield break;
        }
        csv.ReadHeader();

        var encabezado = csv.HeaderRecord ?? Array.Empty<string>();
        var faltantes = ColumnasRequeridas.Where(c => !encabezado.Contains(c)).ToList();
        if (faltantes.Count > 0)
        {
            throw new InvalidDataException($"Estructura inválida en '{Nombre}': faltan las columnas {string.Join(", ", faltantes)}.");
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Mapear(csv.Parser.Row, csv);
        }
    }
}
