using System.Globalization;

namespace OpinionesEtl.Infrastructure.Validacion;

/// <summary>Acumula los errores de formato, obligatoriedad y tipo de una fila. Si no hay errores, la fila es válida.</summary>
internal sealed class ValidadorFila
{
    private readonly List<string> _errores = new();

    public bool EsValida => _errores.Count == 0;

    public string Motivo => string.Join("; ", _errores);

    public void Error(string mensaje) => _errores.Add(mensaje);

    public string Requerido(string? valor, string columna)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length == 0)
        {
            _errores.Add($"{columna} vacío");
        }
        return limpio;
    }

    public DateOnly Fecha(string? valor, string columna)
    {
        if (DateOnly.TryParseExact((valor ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out var fecha))
        {
            return fecha;
        }
        _errores.Add($"{columna} no tiene el formato AAAA-MM-DD ('{valor}')");
        return default;
    }

    public int Entero(string? valor, string columna, int minimo, int maximo)
    {
        if (!int.TryParse((valor ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero))
        {
            _errores.Add($"{columna} no es un entero ('{valor}')");
            return 0;
        }
        return Rango(numero, columna, minimo, maximo);
    }

    public int Rango(int numero, string columna, int minimo, int maximo)
    {
        if (numero < minimo || numero > maximo)
        {
            _errores.Add($"{columna} fuera de rango ({numero}; permitido {minimo} a {maximo})");
        }
        return numero;
    }

    public string Catalogo(string? valor, string columna, params string[] permitidos)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (!permitidos.Contains(limpio))
        {
            _errores.Add($"{columna} no permitido ('{limpio}'; valores: {string.Join(", ", permitidos)})");
        }
        return limpio;
    }
}
