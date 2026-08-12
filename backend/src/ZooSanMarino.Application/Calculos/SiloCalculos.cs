// src/ZooSanMarino.Application/Calculos/SiloCalculos.cs
// Decisiones PURAS del catálogo de silos (sin EF, sin estado): validación de tipo, nombre por
// defecto y expansión del rango de la lista maestra. El service solo resuelve datos y delega.
namespace ZooSanMarino.Application.Calculos;

public static class SiloCalculos
{
    public const string TipoSilo = "Silo";
    public const string TipoBodega = "Bodega";

    /// <summary>Valor legacy de la carga inicial de Santa Reyes; equivale a <see cref="TipoBodega"/>.</summary>
    public const string TipoInsumosLegacy = "Insumos";

    /// <summary>Rango admitido de numeración de la lista maestra.</summary>
    public const int NumeroMinimo = 1;
    public const int NumeroMaximo = 999;

    /// <summary>Tope de silos que se pueden generar en una sola llamada (evita un rango absurdo).</summary>
    public const int MaximoPorRango = 500;

    private const string PatronNombreDefault = "Silo {n}";

    /// <summary>
    /// Normaliza el tipo a uno de los dos válidos. <c>Insumos</c> (legacy) se lee como
    /// <c>Bodega</c>: es la misma ubicación, solo que ahora también guarda alimento.
    /// Devuelve <c>null</c> si el texto no corresponde a ningún tipo conocido.
    /// </summary>
    public static string? NormalizarTipo(string? tipo)
    {
        var t = (tipo ?? string.Empty).Trim();
        if (t.Equals(TipoSilo, StringComparison.OrdinalIgnoreCase)) return TipoSilo;
        if (t.Equals(TipoBodega, StringComparison.OrdinalIgnoreCase)) return TipoBodega;
        if (t.Equals(TipoInsumosLegacy, StringComparison.OrdinalIgnoreCase)) return TipoBodega;
        return null;
    }

    /// <summary>¿El tipo corresponde a una bodega (incluyendo el legacy <c>Insumos</c>)?</summary>
    public static bool EsBodega(string? tipo) => NormalizarTipo(tipo) == TipoBodega;

    /// <summary>Nombre por defecto de una entrada de la lista maestra ("Silo 4").</summary>
    public static string NombreDeCatalogo(int numero, string? patron = null)
    {
        var p = string.IsNullOrWhiteSpace(patron) ? PatronNombreDefault : patron!.Trim();
        return p.Replace("{n}", numero.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Valida un número de la lista maestra. Devuelve el mensaje de error o <c>null</c> si es válido.
    /// </summary>
    public static string? ValidarNumero(int numero) =>
        numero < NumeroMinimo || numero > NumeroMaximo
            ? $"El número de silo debe estar entre {NumeroMinimo} y {NumeroMaximo} (recibido {numero})."
            : null;

    /// <summary>
    /// Expande un rango de la lista maestra descartando los números que ya existen (idempotencia).
    /// Devuelve el mensaje de error en <paramref name="error"/> y una lista vacía si el rango no es válido.
    /// </summary>
    public static IReadOnlyList<int> ExpandirRango(int desde, int hasta, IReadOnlyCollection<int> yaExistentes, out string? error)
    {
        error = null;

        if (desde > hasta)
        {
            error = $"El rango es inválido: 'desde' ({desde}) no puede ser mayor que 'hasta' ({hasta}).";
            return Array.Empty<int>();
        }

        var errDesde = ValidarNumero(desde);
        if (errDesde is not null) { error = errDesde; return Array.Empty<int>(); }

        var errHasta = ValidarNumero(hasta);
        if (errHasta is not null) { error = errHasta; return Array.Empty<int>(); }

        var total = hasta - desde + 1;
        if (total > MaximoPorRango)
        {
            error = $"No se pueden generar {total} silos de una vez (máximo {MaximoPorRango}).";
            return Array.Empty<int>();
        }

        var existentes = new HashSet<int>(yaExistentes);
        var nuevos = new List<int>(total);
        for (var n = desde; n <= hasta; n++)
            if (!existentes.Contains(n))
                nuevos.Add(n);

        return nuevos;
    }

    /// <summary>
    /// Valida los datos de alta de un silo/bodega de granja. Devuelve el mensaje de error o
    /// <c>null</c>. Regla: un <c>Silo</c> sale SIEMPRE del catálogo (para que su número y su nombre
    /// sean los mismos en toda la empresa); una <c>Bodega</c> se nombra a mano y no tiene catálogo.
    /// </summary>
    public static string? ValidarAltaFarmSilo(string? tipo, int? siloCatalogoId, string? nombre)
    {
        var t = NormalizarTipo(tipo);
        if (t is null)
            return $"Tipo de ubicación inválido: '{tipo}'. Valores admitidos: '{TipoSilo}' o '{TipoBodega}'.";

        if (t == TipoSilo)
        {
            if (siloCatalogoId is null or <= 0)
                return "Un silo debe salir de la lista maestra: indique el silo del catálogo.";
            return null;
        }

        // Bodega
        if (string.IsNullOrWhiteSpace(nombre))
            return "La bodega necesita un nombre.";
        if (siloCatalogoId is > 0)
            return "Una bodega no sale de la lista maestra de silos.";
        return null;
    }

    /// <summary>
    /// Orden de presentación: primero las bodegas (son la ubicación «general» de la granja) y luego
    /// los silos por número. Sin número, alfabético. Devuelve la clave de orden.
    /// </summary>
    public static (int Grupo, int Numero, string Nombre) ClaveOrden(string? tipo, int? numero, string? nombre) =>
        (EsBodega(tipo) ? 0 : 1, numero ?? int.MaxValue, nombre ?? string.Empty);
}
