// Liquidación por período (semanal/mensual) y consulta de lotes cerrados/en rango.
// Partial de IndicadorEcuadorService.
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class IndicadorEcuadorService
{
    /// <summary>
    /// Liquidación por período: solo granjas que finalizaron lote (aves = 0) en el rango.
    /// Semanal (viernes) o Mensual (primeros días de la semana). Solo lotes cerrados cuya fecha de cierre está en [fechaInicio, fechaFin].
    /// </summary>
    public async Task<LiquidacionPeriodoDto> CalcularLiquidacionPeriodoAsync(
        DateTime fechaInicio,
        DateTime fechaFin,
        string tipoPeriodo,
        int? granjaId = null)
    {
        var request = new IndicadorEcuadorRequest(
            GranjaId: granjaId,
            FechaDesde: null,
            FechaHasta: null,
            TipoFiltroLotes: "aves_cero"
        );

        var indicadores = await CalcularIndicadoresAsync(request);
        var indicadoresList = indicadores.ToList();

        // Solo lotes que cerraron (último despacho) dentro del período
        var fechaFinInclusive = fechaFin.Date.AddDays(1).AddTicks(-1);
        var enPeriodo = indicadoresList
            .Where(i => i.FechaCierreLote.HasValue &&
                        i.FechaCierreLote.Value >= fechaInicio &&
                        i.FechaCierreLote.Value <= fechaFinInclusive)
            .ToList();

        return new LiquidacionPeriodoDto(
            fechaInicio,
            fechaFin,
            tipoPeriodo,
            enPeriodo.Select(i => i.GranjaId).Distinct().Count(),
            enPeriodo.Count,
            enPeriodo
        );
    }

    public async Task<IEnumerable<IndicadorEcuadorDto>> ObtenerLotesCerradosAsync(
        DateTime fechaDesde,
        DateTime fechaHasta,
        int? granjaId = null,
        bool soloCerrados = true)
    {
        if (soloCerrados)
        {
            // Solo lotes cerrados (aves = 0). La franja fecha inicio/fin aplica a FECHA DE CIERRE (último despacho),
            // no a fecha de encaset (misma idea que liquidación por período).
            var request = new IndicadorEcuadorRequest(
                GranjaId: granjaId,
                FechaDesde: null,
                FechaHasta: null,
                TipoFiltroLotes: "aves_cero"
            );

            var indicadores = (await CalcularIndicadoresAsync(request).ConfigureAwait(false)).ToList();
            var fechaFinInclusive = fechaHasta.Date.AddDays(1).AddTicks(-1);
            return indicadores.Where(i =>
                i.FechaCierreLote.HasValue &&
                i.FechaCierreLote.Value >= fechaDesde.Date &&
                i.FechaCierreLote.Value <= fechaFinInclusive);
        }

        // Franja por fecha de encaset; incluye lotes abiertos en ese rango
        var requestAbierto = new IndicadorEcuadorRequest(
            GranjaId: granjaId,
            FechaDesde: fechaDesde,
            FechaHasta: fechaHasta,
            TipoFiltroLotes: "todos"
        );

        return await CalcularIndicadoresAsync(requestAbierto).ConfigureAwait(false);
    }
}
