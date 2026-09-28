using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;
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
        int loteId,
        int farmId,
        string? nucleoId,
        string? galponId,
        MovimientosAlimentoSeguimientoCalculos.RangoFechas rango,
        CancellationToken ct = default)
    {
        var nucleo = (nucleoId ?? string.Empty).Trim();
        var galpon = (galponId ?? string.Empty).Trim();
        var hastaExclusivo = rango.Hasta.Date.AddDays(1);

        var configuracion = await context.Farms.AsNoTracking()
            .Where(f => f.Id == farmId && f.CompanyId == companyId && f.DeletedAt == null)
            .Join(
                context.Set<Company>().AsNoTracking().Where(c => c.Id == companyId),
                f => f.CompanyId,
                c => c.Id,
                (f, c) => new
                {
                    FarmPorGalpon = f.ManejaAlimentoPorGalpon,
                    CompanyPorGalpon = c.ManejaAlimentoPorGalpon,
                    c.ManejaInventarioPorSilo
                })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (configuracion is null)
            return [];

        var alcance = MovimientosAlimentoSeguimientoCalculos.ResolverAlcance(
            configuracion.ManejaInventarioPorSilo,
            configuracion.FarmPorGalpon,
            configuracion.CompanyPorGalpon);

        var query =
            from historico in context.LoteRegistroHistoricoUnificados.AsNoTracking()
            join item in context.ItemInventario.AsNoTracking()
                on historico.ItemInventarioEcuadorId equals item.Id
            where historico.CompanyId == companyId
                && item.CompanyId == companyId
                && item.TipoItem.ToLower() == "alimento"
                && historico.FarmId == farmId
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
            select historico;

        if (alcance == MovimientosAlimentoSeguimientoCalculos.AlcanceInventarioAlimento.Silo)
        {
            if (loteId <= 0)
                return [];

            var silosDelLote =
                from loteSilo in context.LoteSilos.AsNoTracking()
                join silo in context.FarmSilos.AsNoTracking()
                    on loteSilo.FarmSiloId equals silo.Id
                where loteSilo.CompanyId == companyId
                    && loteSilo.LoteId == loteId
                    && loteSilo.Activo
                    && silo.CompanyId == companyId
                    && silo.GranjaId == farmId
                    && silo.Activo
                    && silo.DeletedAt == null
                select silo.Id;

            query = query.Where(h => h.SiloId.HasValue && silosDelLote.Contains(h.SiloId.Value));
        }
        else if (alcance == MovimientosAlimentoSeguimientoCalculos.AlcanceInventarioAlimento.Galpon)
        {
            query = query.Where(h =>
                (h.NucleoId == null ? string.Empty : h.NucleoId.Trim()) == nucleo
                && (h.GalponId == null ? string.Empty : h.GalponId.Trim()) == galpon);
        }

        return await query
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .Select(h => new MovimientoAlimentoSeguimientoDto(
                h.Id,
                h.FechaOperacion,
                h.TipoEvento,
                h.CantidadKg ?? 0m,
                h.ItemResumen,
                h.Referencia,
                h.NumeroDocumento))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
