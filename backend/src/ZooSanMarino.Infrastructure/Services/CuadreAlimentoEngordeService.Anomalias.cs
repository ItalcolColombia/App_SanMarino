// src/ZooSanMarino.Infrastructure/Services/CuadreAlimentoEngordeService.Anomalias.cs
//
// Señalamiento de la anomalía R2: lotes ya liquidados que congelaron su liquidación con alimento en el
// galpón. La regla operativa es que al liquidar el galpón queda en CERO y el sobrante se traslada; el
// sistema no lo impide ni lo compensa — lo SEÑALA, que es lo que pidió el dueño del producto.
//
// El saldo NO se recalcula: sale de la copia congelada, que es lo que se aprobó al liquidar. La
// clasificación vive en AnomaliaAlimentoLiquidadoCalculos (puro y testeado).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class CuadreAlimentoEngordeService
{
    /// <summary>Piso para considerar que una liquidación congeló CON saldo (no es la tolerancia del cuadre).</summary>
    private const decimal EpsilonSaldoKg = 0.001m;

    public async Task<AnomaliaAlimentoLiquidadoDto> ObtenerLiquidadosConAlimentoAsync(
        bool soloAnomalias = false, CancellationToken ct = default)
    {
        var companyId = await ResolverCompanyIdAsync();
        if (companyId <= 0)
            return new AnomaliaAlimentoLiquidadoDto(0, 0, 0, 0, 0, 0m, 0m, []);

        // 1. Copias congeladas VIGENTES. Reabrir un lote anula la copia (trigger
        //    trg_lote_ave_engorde_anula_congelada) ⇒ la fila sale sola del reporte, sin lógica extra.
        var congeladas = await _db.LiquidacionLoteEngordeCongelada.AsNoTracking()
            .Where(q => q.CompanyId == companyId && q.AnuladaAt == null)
            .Select(q => new { q.LoteAveEngordeId, q.LoteNombre, q.LiquidadoAt, q.SaldoAlimentoKg })
            .ToListAsync(ct);

        var totalLiquidados  = congeladas.Count;
        // Copias de backfill: el saldo quedó en NULL a propósito (replicar la aritmética en SQL habría
        // sido una segunda implementación del mismo cálculo). No se les inventa un número.
        var sinDatoCongelado = congeladas.Count(c => c.SaldoAlimentoKg == null);

        var objetivo = congeladas.Where(c => (c.SaldoAlimentoKg ?? 0m) > EpsilonSaldoKg).ToList();
        if (objetivo.Count == 0)
            return new AnomaliaAlimentoLiquidadoDto(totalLiquidados, 0, sinDatoCongelado, 0, 0, 0m, 0m, []);

        var loteIds = objetivo.Select(c => (int?)c.LoteAveEngordeId).Distinct().ToList();

        // 2. Ubicación y último día de seguimiento de cada lote liquidado. El MAX lo resuelve la BD.
        var ubicacion = await _db.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && loteIds.Contains(l.LoteAveEngordeId))
            .Select(l => new
            {
                LoteId = l.LoteAveEngordeId,
                l.GranjaId,
                Nucleo = l.NucleoId == null ? "" : l.NucleoId.Trim(),
                Galpon = l.GalponId == null ? "" : l.GalponId.Trim(),
                l.FechaEncaset,
                UltimoSeguimiento = _db.SeguimientoDiarioAvesEngorde
                    .Where(s => s.LoteAveEngordeId == l.LoteAveEngordeId)
                    .Max(s => (DateTime?)s.Fecha)
            })
            .ToListAsync(ct);

        var ubicacionPorLote = ubicacion
            .Where(u => u.LoteId.HasValue)
            .ToDictionary(u => u.LoteId!.Value);

        var granjaIds = ubicacion.Select(u => u.GranjaId).Distinct().ToList();

        var granjas = await _db.Farms.AsNoTracking()
            .Where(f => granjaIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(ct);
        var nombreGranja = granjas.ToDictionary(g => g.Id, g => g.Name);

        // 3. Stock de alimento por ubicación (la BD agrupa; el backend solo orquesta).
        var stockCrudo = await (
            from s in _db.InventarioGestionStock.AsNoTracking()
            join i in _db.ItemInventario.AsNoTracking() on s.ItemInventarioEcuadorId equals i.Id
            where s.CompanyId == companyId
               && granjaIds.Contains(s.FarmId)
               && i.TipoItem.ToLower().StartsWith("alimento")
            group s by new
            {
                s.FarmId,
                Nucleo = s.NucleoId == null ? "" : s.NucleoId.Trim(),
                Galpon = s.GalponId == null ? "" : s.GalponId.Trim()
            } into g
            select new { g.Key.FarmId, g.Key.Nucleo, g.Key.Galpon, Kg = g.Sum(x => x.Quantity) }
        ).ToListAsync(ct);

        var stockPorUbicacion = stockCrudo
            .ToDictionary(x => (x.FarmId, x.Nucleo, x.Galpon), x => x.Kg);

        // 4. Traslados de SALIDA del galpón. Se filtra por UBICACIÓN y no por empresa, igual que
        //    `mov_post` en fn_cuadre_alimento_engorde: hay movimientos históricos con company_id = 0 y
        //    el cuadre igual los cuenta. Dos criterios distintos darían dos números para lo mismo.
        var salidas = await _db.LoteRegistroHistoricoUnificados.AsNoTracking()
            .Where(h => !h.Anulado
                     && h.TipoEvento == "INV_TRASLADO_SALIDA"
                     && granjaIds.Contains(h.FarmId)
                     && h.CantidadKg != null)
            .Select(h => new
            {
                h.FarmId,
                Nucleo = h.NucleoId == null ? "" : h.NucleoId.Trim(),
                Galpon = h.GalponId == null ? "" : h.GalponId.Trim(),
                h.FechaOperacion,
                Kg = h.CantidadKg ?? 0m
            })
            .ToListAsync(ct);

        // 5. Ciclos del galpón, para decir quién ocupa hoy la ubicación.
        var ciclos = await _db.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null
                     && granjaIds.Contains(l.GranjaId) && l.FechaEncaset != null)
            .Select(l => new
            {
                LoteId = l.LoteAveEngordeId,
                l.LoteNombre,
                l.GranjaId,
                Nucleo = l.NucleoId == null ? "" : l.NucleoId.Trim(),
                Galpon = l.GalponId == null ? "" : l.GalponId.Trim(),
                l.FechaEncaset
            })
            .ToListAsync(ct);

        var filas = new List<AnomaliaAlimentoLiquidadoFilaDto>(objetivo.Count);

        foreach (var liq in objetivo.OrderByDescending(c => c.SaldoAlimentoKg))
        {
            if (!ubicacionPorLote.TryGetValue(liq.LoteAveEngordeId, out var u)) continue;

            var clave = (u.GranjaId, u.Nucleo, u.Galpon);
            stockPorUbicacion.TryGetValue(clave, out var stockKg);

            // El corte es el último día de seguimiento: lo que se movió DESPUÉS no cabe en la foto
            // congelada. Sin seguimiento no hay foto que contradecir, así que se usa la liquidación.
            var corte = (u.UltimoSeguimiento ?? liq.LiquidadoAt).Date;

            var salidasPostKg = salidas
                .Where(s => s.FarmId == u.GranjaId && s.Nucleo == u.Nucleo && s.Galpon == u.Galpon
                         && s.FechaOperacion.Date > corte)
                .Sum(s => Math.Abs(s.Kg));

            var saldoKg = liq.SaldoAlimentoKg ?? 0m;

            var siguiente = ciclos
                .Where(c => c.GranjaId == u.GranjaId && c.Nucleo == u.Nucleo && c.Galpon == u.Galpon
                         && c.LoteId != liq.LoteAveEngordeId
                         && u.FechaEncaset != null && c.FechaEncaset > u.FechaEncaset)
                .OrderBy(c => c.FechaEncaset)
                .FirstOrDefault();

            filas.Add(new AnomaliaAlimentoLiquidadoFilaDto(
                CompanyId:            companyId,
                GranjaId:             u.GranjaId,
                Granja:               nombreGranja.TryGetValue(u.GranjaId, out var gn) ? gn : $"#{u.GranjaId}",
                NucleoId:             u.Nucleo,
                GalponId:             u.Galpon,
                LoteAveEngordeId:     liq.LoteAveEngordeId,
                LoteNombre:           liq.LoteNombre,
                LiquidadoAt:          liq.LiquidadoAt,
                UltimoSeguimiento:    u.UltimoSeguimiento,
                SaldoCongeladoKg:     saldoKg,
                SalidasPostKg:        salidasPostKg,
                StockGalponKg:        stockKg,
                KgSinTrasladar:       AnomaliaAlimentoLiquidadoCalculos.KgSinTrasladar(saldoKg, salidasPostKg),
                KgSinRespaldo:        AnomaliaAlimentoLiquidadoCalculos.KgSinRespaldo(saldoKg, salidasPostKg, stockKg),
                Estado:               AnomaliaAlimentoLiquidadoCalculos.Clasificar(saldoKg, salidasPostKg, stockKg),
                Detalle:              AnomaliaAlimentoLiquidadoCalculos.Describir(saldoKg, salidasPostKg, stockKg),
                LoteSiguienteId:      siguiente?.LoteId,
                LoteSiguienteNombre:  siguiente?.LoteNombre,
                LoteSiguienteEncaset: siguiente?.FechaEncaset));
        }

        // El resumen se calcula SIEMPRE sobre el total; el filtro solo recorta el detalle.
        var ordenadas = filas
            .OrderByDescending(f => (int)f.Estado)
            .ThenByDescending(f => f.KgSinTrasladar)
            .ToList();

        return new AnomaliaAlimentoLiquidadoDto(
            TotalLiquidados:    totalLiquidados,
            ConSaldo:           objetivo.Count,
            SinDatoCongelado:   sinDatoCongelado,
            PendientesEnGalpon: filas.Count(f => f.Estado == EstadoAlimentoLiquidado.PendienteEnGalpon),
            SinRespaldoFisico:  filas.Count(f => f.Estado == EstadoAlimentoLiquidado.SinRespaldoFisico),
            KgSinTrasladar:     filas.Sum(f => f.KgSinTrasladar),
            KgSinRespaldo:      filas.Sum(f => f.KgSinRespaldo),
            Lotes:              soloAnomalias
                                    ? ordenadas.Where(f => f.Estado != EstadoAlimentoLiquidado.Trasladado).ToList()
                                    : ordenadas);
    }
}
