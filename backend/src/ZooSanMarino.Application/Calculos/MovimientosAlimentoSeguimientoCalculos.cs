namespace ZooSanMarino.Application.Calculos;

/// <summary>Reglas puras para acotar los movimientos a la fase y al filtro visible.</summary>
public static class MovimientosAlimentoSeguimientoCalculos
{
    public sealed record RangoFechas(DateTime Desde, DateTime Hasta);

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
