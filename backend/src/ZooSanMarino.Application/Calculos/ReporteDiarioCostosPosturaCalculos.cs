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

    /// <summary>
    /// Footer completo del reporte. Las filas marcadas <c>ExcluidoDelTotal</c> se muestran en la
    /// tabla pero NO suman acá (ver <see cref="MarcarDuplicados"/>). Sin filas marcadas el resultado
    /// es idéntico al de siempre.
    /// </summary>
    public static ReporteDiarioCostosPosturaTotalesDto ConstruirTotales(
        IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
    {
        var contables = filas.Any(f => f.ExcluidoDelTotal)
            ? filas.Where(f => !f.ExcluidoDelTotal).ToList()
            : filas;

        var consumoH = RedondearKg(contables.Sum(f => f.ConsumoKgH));
        var consumoM = RedondearKg(contables.Sum(f => f.ConsumoKgM));

        return new ReporteDiarioCostosPosturaTotalesDto(
            Aves: TotalesAves(contables),
            ConsumoKgH: consumoH,
            ConsumoKgM: consumoM,
            ConsumoKgTotal: RedondearKg(consumoH + consumoM),
            Alimentos: TotalesAlimento(contables),
            Huevo: TotalesHuevo(contables));
    }

    /// <summary>
    /// Marca los días que un lote tiene registrados en las DOS etapas y decide cuáles no pueden sumar.
    ///
    /// <para>NO reimplementa la regla: el dueño del corte de etapa es
    /// <see cref="CorteEtapaPosturaCalculos.HayDobleConteo"/> — hay doble conteo solo cuando las dos
    /// filas del día <b>aportan consumo o bajas</b>. Una fila de producción de solo huevos (el arrastre
    /// legítimo del levante) o una fila vacía no chocan con nada.</para>
    ///
    /// <para>Cuando sí chocan, la que queda fuera del total es la de <b>levante</b>: la de producción
    /// sale de <c>fn_seguimiento_diario_produccion</c>, la fn diaria canónica, y era la única que el
    /// reporte contaba antes de que el levante se arreglara ⇒ esos días no cambian de número.</para>
    ///
    /// <para>Caso real que motiva esto: el lote K345 tiene 15 días de 2025 con fila en las dos etapas;
    /// en 14 de ellos el consumo y la mortalidad están duplicados exactos (~16.952 kg y 10 aves), y en
    /// el restante la fila de levante viene vacía — la regla no lo marca, y así debe ser.</para>
    /// </summary>
    public static IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> MarcarDuplicados(
        IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
    {
        // Un solo barrido: aporte acumulado de cada etapa por (lote, día).
        var aporteProduccion = new Dictionary<(int LoteId, DateTime Dia), CorteEtapaPosturaCalculos.AporteDia>();
        var aporteLevante = new Dictionary<(int LoteId, DateTime Dia), CorteEtapaPosturaCalculos.AporteDia>();

        foreach (var f in filas)
        {
            var destino = f.Fase switch
            {
                FaseProduccion => aporteProduccion,
                FaseLevante => aporteLevante,
                _ => null
            };
            if (destino is null) continue;

            var clave = (f.LoteId, f.Fecha.Date);
            destino[clave] = destino.TryGetValue(clave, out var previo)
                ? Sumar(previo, Aporte(f))
                : Aporte(f);
        }

        if (aporteProduccion.Count == 0 || aporteLevante.Count == 0) return filas;

        var marcadas = new List<ReporteDiarioCostosPosturaFilaDto>(filas.Count);
        foreach (var f in filas)
        {
            var clave = (f.LoteId, f.Fecha.Date);

            if (!aporteProduccion.TryGetValue(clave, out var prod)
                || !aporteLevante.TryGetValue(clave, out var lev))
            {
                marcadas.Add(f);   // el día vive en una sola etapa: nada que decidir
                continue;
            }

            marcadas.Add(f.Fase switch
            {
                // Producción siempre suma; se marca solo para que la UI muestre el par.
                FaseProduccion => f with { DiaEnAmbasEtapas = true },
                FaseLevante => f with
                {
                    DiaEnAmbasEtapas = true,
                    ExcluidoDelTotal = CorteEtapaPosturaCalculos.HayDobleConteo(lev, prod)
                },
                _ => f
            });
        }

        return marcadas;
    }

    /// <summary>Días que se muestran pero no suman (ver <see cref="MarcarDuplicados"/>).</summary>
    public static int ContarDuplicados(IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
        => filas.Count(f => f.ExcluidoDelTotal);

    /// <summary>
    /// Cuánto quedó FUERA del total por el traslape, para poder decirlo en pantalla en vez de que
    /// desaparezca en silencio. El duplicado no siempre es simétrico: en K345 la fila de levante trae
    /// 133 machos de selección que la de producción no registra. <c>null</c> si no se excluyó nada.
    /// </summary>
    public static ReporteDiarioCostosPosturaTotalesDto? TotalesExcluidos(
        IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
    {
        var excluidas = filas.Where(f => f.ExcluidoDelTotal).ToList();
        if (excluidas.Count == 0) return null;

        // Se totalizan como si fueran filas normales: el flag solo importa en ConstruirTotales.
        return ConstruirTotales(
            excluidas.Select(f => f with { ExcluidoDelTotal = false }).ToList());
    }

    /// <summary>
    /// Dónde ocurrió cada fase: una entrada por (fase, granja, lote base). Es lo que permite leer
    /// «el levante fue en NIZA III y la producción en NIZA I» sin recorrer el detalle día a día.
    /// Orden de ciclo: Levante antes que Producción; dentro de cada fase, por granja.
    /// </summary>
    public static IReadOnlyList<ReporteDiarioCostosPosturaUbicacionFaseDto> Ubicaciones(
        IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> filas)
        => filas
            .GroupBy(f => (f.Fase, f.GranjaId, f.GranjaNombre, f.LoteBaseNombre))
            .Select(g => new ReporteDiarioCostosPosturaUbicacionFaseDto(
                Fase: g.Key.Fase,
                GranjaId: g.Key.GranjaId,
                GranjaNombre: g.Key.GranjaNombre,
                LoteBaseNombre: g.Key.LoteBaseNombre,
                Lotes: g.Select(f => f.LoteId).Distinct().Count(),
                Desde: g.Min(f => f.Fecha),
                Hasta: g.Max(f => f.Fecha),
                Dias: g.Select(f => f.Fecha.Date).Distinct().Count()))
            .OrderBy(u => u.Fase == FaseLevante ? 0 : 1)
            .ThenBy(u => u.GranjaNombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.LoteBaseNombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Aporte de la fila según la regla del corte: alimento y aves, nunca huevos.</summary>
    private static CorteEtapaPosturaCalculos.AporteDia Aporte(ReporteDiarioCostosPosturaFilaDto f)
        => new(
            ConsumoKg: (decimal)(f.ConsumoKgH + f.ConsumoKgM),
            Mortalidad: f.MortalidadH + f.MortalidadM,
            Seleccion: f.SeleccionH + f.SeleccionM);

    private static CorteEtapaPosturaCalculos.AporteDia Sumar(
        CorteEtapaPosturaCalculos.AporteDia a, CorteEtapaPosturaCalculos.AporteDia b)
        => new(a.ConsumoKg + b.ConsumoKg, a.Mortalidad + b.Mortalidad, a.Seleccion + b.Seleccion);

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
