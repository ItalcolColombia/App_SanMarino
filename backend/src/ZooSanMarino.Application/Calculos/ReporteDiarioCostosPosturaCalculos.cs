// src/ZooSanMarino.Application/Calculos/ReporteDiarioCostosPosturaCalculos.cs
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosPostura;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO del Reporte Diario Área de Costos de POSTURA (sin EF ni estado).
///
/// Es el ÚNICO dueño de la clasificación de huevo fértil/comercial/inservible (decisión D1 del
/// 07-ago-2026): la fn SQL devuelve las 11 categorías CRUDAS justamente para que esta fórmula no
/// tenga una segunda implementación. Si costos cambia el criterio, se cambia acá y los tests lo
/// cazan.
///
/// ⚠️ Es POSTURA, no engorde: no comparte aritmética con <see cref="ReporteDiarioCostosEngordeCalculos"/>.
/// </summary>
public static class ReporteDiarioCostosPosturaCalculos
{
    public const string FaseLevante = "Levante";
    public const string FaseProduccion = "Produccion";

    /// <summary>Redondeo estándar de kg del reporte (3 decimales, half away from zero).</summary>
    public static double RedondearKg(double valor) => Math.Round(valor, 3, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Normaliza la fase pedida por el usuario. Acepta "levante"/"Levante" y
    /// "produccion"/"Producción"/"PRODUCCION". Cualquier otra cosa (incluido "ambas",
    /// vacío o null) ⇒ <c>null</c> = las dos fases.
    /// </summary>
    public static string? NormalizarFase(string? fase)
    {
        if (string.IsNullOrWhiteSpace(fase)) return null;
        var f = fase.Trim().ToLowerInvariant();
        if (f.StartsWith("lev", StringComparison.Ordinal)) return FaseLevante;
        if (f.StartsWith("produc", StringComparison.Ordinal)) return FaseProduccion;
        return null;
    }

    /// <summary>Etiqueta "lote : galpón" que pide el diseño del reporte.</summary>
    public static string EtiquetaLoteGalpon(string? loteNombre, string? galponNombre)
    {
        var lote = string.IsNullOrWhiteSpace(loteNombre) ? "(sin lote)" : loteNombre.Trim();
        var galpon = string.IsNullOrWhiteSpace(galponNombre) ? "(sin galpón)" : galponNombre.Trim();
        return $"{lote} : {galpon}";
    }

    /// <summary>
    /// Clasificación de huevo — DECISIÓN D1 (07-ago-2026, confirmada por el usuario):
    /// <list type="bullet">
    ///   <item><c>fértil</c>     = huevo incubable (<c>huevo_inc</c>, que la BD guarda como limpio + tratado)</item>
    ///   <item><c>comercial</c>  = sucio + deforme + blanco + doble yema + piso + pequeño</item>
    ///   <item><c>inservible</c> = roto + desecho + otro</item>
    /// </list>
    /// Los tres SUMAN EXACTO <c>huevo_tot</c>: el invariante de la BD es
    /// <c>huevo_tot = limpio+tratado+sucio+deforme+blanco+doble_yema+piso+pequeño+roto+desecho+otro</c>
    /// y <c>huevo_inc = limpio + tratado</c> (verificado en S-369B: 7.799 = 7.799 el 15-may-2026).
    ///
    /// No se "cuadra" a la fuerza: si la fila viene inconsistente, el total sigue siendo el
    /// registrado y <c>ParticionCuadra</c> queda en false para que se vea.
    /// </summary>
    public static ReporteDiarioCostosPosturaHuevoDto ClasificarHuevo(
        HuevoCrudo crudo,
        int venta = 0,
        int trasladoPlanta = 0)
    {
        var fertil = crudo.Inc;
        var comercial = crudo.Sucio + crudo.Deforme + crudo.Blanco + crudo.DobleYema + crudo.Piso + crudo.Pequeno;
        var inservible = crudo.Roto + crudo.Desecho + crudo.Otro;

        return new ReporteDiarioCostosPosturaHuevoDto(
            Fertil: fertil,
            Comercial: comercial,
            Inservible: inservible,
            Total: crudo.Tot,
            Venta: venta,
            TrasladoPlanta: trasladoPlanta);
    }

    /// <summary>Footer de la pestaña Aves: suma de las 4 categorías por sexo.</summary>
    public static ReporteDiarioCostosPosturaTotalesAvesDto TotalesAves(
        IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
    {
        var mortH = filas.Sum(f => f.MortalidadH);
        var mortM = filas.Sum(f => f.MortalidadM);
        var selH = filas.Sum(f => f.SeleccionH);
        var selM = filas.Sum(f => f.SeleccionM);
        var errH = filas.Sum(f => f.ErrorSexajeH);
        var errM = filas.Sum(f => f.ErrorSexajeM);
        var venH = filas.Sum(f => f.VentaAvesH);
        var venM = filas.Sum(f => f.VentaAvesM);

        var totalH = mortH + selH + errH + venH;
        var totalM = mortM + selM + errM + venM;

        return new ReporteDiarioCostosPosturaTotalesAvesDto(
            mortH, mortM, selH, selM, errH, errM, venH, venM,
            totalH, totalM, totalH + totalM);
    }

    /// <summary>
    /// Footer de la pestaña Alimento: agrupa los ítems por (sexo, nombre) en todo el rango.
    /// El nombre se compara sin distinguir mayúsculas; se conserva la primera grafía vista.
    /// </summary>
    public static IReadOnlyList<ReporteDiarioCostosPosturaTotalAlimentoDto> TotalesAlimento(
        IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
    {
        return filas
            .SelectMany(f => f.Alimentos)
            .GroupBy(a => (a.Sexo, Nombre: a.Nombre), SexoNombreComparer.Instance)
            .Select(g => new ReporteDiarioCostosPosturaTotalAlimentoDto(
                g.First().Sexo,
                g.First().Nombre,
                RedondearKg(g.Sum(a => a.CantidadKg))))
            .OrderBy(a => a.Sexo, StringComparer.Ordinal)
            .ThenBy(a => a.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Footer de la pestaña Huevos: suma de los grupos ya clasificados y de los movimientos.</summary>
    public static ReporteDiarioCostosPosturaHuevoDto TotalesHuevo(
        IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
        => new(
            Fertil: filas.Sum(f => f.Huevo.Fertil),
            Comercial: filas.Sum(f => f.Huevo.Comercial),
            Inservible: filas.Sum(f => f.Huevo.Inservible),
            Total: filas.Sum(f => f.Huevo.Total),
            Venta: filas.Sum(f => f.Huevo.Venta),
            TrasladoPlanta: filas.Sum(f => f.Huevo.TrasladoPlanta));

    /// <summary>Footer completo del reporte.</summary>
    public static ReporteDiarioCostosPosturaTotalesDto ConstruirTotales(
        IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
    {
        var consumoH = RedondearKg(filas.Sum(f => f.ConsumoKgH));
        var consumoM = RedondearKg(filas.Sum(f => f.ConsumoKgM));

        return new ReporteDiarioCostosPosturaTotalesDto(
            Aves: TotalesAves(filas),
            ConsumoKgH: consumoH,
            ConsumoKgM: consumoM,
            ConsumoKgTotal: RedondearKg(consumoH + consumoM),
            Alimentos: TotalesAlimento(filas),
            Huevo: TotalesHuevo(filas));
    }

    /// <summary>Fases realmente presentes en el resultado, en orden de ciclo (Levante antes que Producción).</summary>
    public static IReadOnlyList<string> FasesPresentes(IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
    {
        var fases = new List<string>();
        if (filas.Any(f => f.Fase == FaseLevante)) fases.Add(FaseLevante);
        if (filas.Any(f => f.Fase == FaseProduccion)) fases.Add(FaseProduccion);
        return fases;
    }

    /// <summary>Compara (sexo, nombre) con el nombre case-insensitive — el sexo es un código, siempre 'H'/'M'.</summary>
    private sealed class SexoNombreComparer : IEqualityComparer<(string Sexo, string Nombre)>
    {
        public static readonly SexoNombreComparer Instance = new();

        public bool Equals((string Sexo, string Nombre) a, (string Sexo, string Nombre) b)
            => string.Equals(a.Sexo, b.Sexo, StringComparison.Ordinal)
            && string.Equals(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Sexo, string Nombre) v)
            => HashCode.Combine(v.Sexo, v.Nombre.ToLowerInvariant());
    }
}
