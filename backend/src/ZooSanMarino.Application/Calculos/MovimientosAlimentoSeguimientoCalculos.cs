namespace ZooSanMarino.Application.Calculos;

/// <summary>Reglas puras para acotar los movimientos a la fase y al filtro visible.</summary>
public static class MovimientosAlimentoSeguimientoCalculos
{
    public enum AlcanceInventarioAlimento
    {
        Granja,
        Galpon,
        Silo
    }

    public sealed record RangoFechas(DateTime Desde, DateTime Hasta);

    /// <summary>
    /// Resuelve dónde vive físicamente el alimento. El silo tiene precedencia porque, con ese flag,
    /// núcleo y galpón se persisten en NULL por diseño. Sin silo se conserva la jerarquía canónica
    /// granja → empresa de <see cref="AlimentoNivelResolver"/>.
    /// </summary>
    public static AlcanceInventarioAlimento ResolverAlcance(
        bool manejaInventarioPorSilo,
        bool? granjaManejaAlimentoPorGalpon,
        bool empresaManejaAlimentoPorGalpon)
    {
        if (manejaInventarioPorSilo)
            return AlcanceInventarioAlimento.Silo;

        return AlimentoNivelResolver.ManejaPorGalpon(
            granjaManejaAlimentoPorGalpon,
            empresaManejaAlimentoPorGalpon)
            ? AlcanceInventarioAlimento.Galpon
            : AlcanceInventarioAlimento.Granja;
    }

    public static RangoFechas? ResolverRango(
        DateTime? inicioFase,
        DateTime? finFase,
        DateTime? primerSeguimiento,
        DateTime? ultimoSeguimiento,
        bool faseCerrada,
        DateTime hoy,
        DateTime? filtroDesde = null,
        DateTime? filtroHasta = null)
    {
        var desde = (inicioFase ?? primerSeguimiento)?.Date;
        if (!desde.HasValue)
            return null;

        var hasta = (finFase
            ?? (faseCerrada ? ultimoSeguimiento : hoy)
            ?? ultimoSeguimiento)?.Date;
        if (!hasta.HasValue)
            return null;

        if (filtroDesde.HasValue && filtroDesde.Value.Date > desde.Value)
            desde = filtroDesde.Value.Date;
        if (filtroHasta.HasValue && filtroHasta.Value.Date < hasta.Value)
            hasta = filtroHasta.Value.Date;

        return desde.Value <= hasta.Value ? new RangoFechas(desde.Value, hasta.Value) : null;
    }
}
