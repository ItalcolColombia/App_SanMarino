using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Consulta compartida por Levante y Producción. La BD filtra empresa, ubicación, fechas y tipo de
/// ítem; el backend sólo proyecta el contrato de lectura.
/// </summary>
internal static class MovimientosAlimentoSeguimientoConsultas
{
    private static readonly string[] TiposVisibles =
    [
        "INV_INGRESO",
        "INV_TRASLADO_ENTRADA",
        "INV_TRASLADO_SALIDA"
    ];

    public static async Task<IReadOnlyList<MovimientoAlimentoSeguimientoDto>> ConsultarAsync(
        ZooSanMarinoContext context,
        int companyId,
        int farmId,
        string? nucleoId,
        string? galponId,
        MovimientosAlimentoSeguimientoCalculos.RangoFechas rango,
        CancellationToken ct = default)
    {
        var nucleo = (nucleoId ?? string.Empty).Trim();
        var galpon = (galponId ?? string.Empty).Trim();
        var hastaExclusivo = rango.Hasta.Date.AddDays(1);

        return await (
            from historico in context.LoteRegistroHistoricoUnificados.AsNoTracking()
            join item in context.ItemInventario.AsNoTracking()
                on historico.ItemInventarioEcuadorId equals item.Id
            where historico.CompanyId == companyId
                && item.CompanyId == companyId
                && item.TipoItem.ToLower() == "alimento"
                && historico.FarmId == farmId
                && (historico.NucleoId == null ? string.Empty : historico.NucleoId.Trim()) == nucleo
                && (historico.GalponId == null ? string.Empty : historico.GalponId.Trim()) == galpon
                && TiposVisibles.Contains(historico.TipoEvento)
                && !historico.Anulado
                && !historico.ParaProximoCiclo
                && historico.CantidadKg != null
                && historico.CantidadKg > 0
                && historico.FechaOperacion >= rango.Desde.Date
                && historico.FechaOperacion < hastaExclusivo
                && !(historico.Referencia != null
                    && (historico.Referencia.Contains("devolución por eliminación")
                        || historico.Referencia.Contains("devolucion por eliminacion")))
            orderby historico.FechaOperacion, historico.Id
            select new MovimientoAlimentoSeguimientoDto(
                historico.Id,
                historico.FechaOperacion,
                historico.TipoEvento,
                historico.CantidadKg ?? 0m,
                historico.ItemResumen,
                historico.Referencia,
                historico.NumeroDocumento))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
