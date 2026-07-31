// src/ZooSanMarino.Infrastructure/Services/LiquidacionCongeladaAplicador.cs
//
// Congela, anula y re-congela la liquidación de un lote de pollo engorde, y centraliza el
// RESUMEN de liquidación (una sola fórmula para los dos services de seguimiento y el cierre).
//
// POR QUÉ ESTÁTICO Y RECIBIENDO EL CONTEXTO
// Mismo patrón —y mismo motivo— que RetiroAvesEngordeAplicador y SaldoAlimentoEngordeAplicador:
// lo necesitan services que no se pueden inyectar entre sí sin ciclo de DI.
//
// POR QUÉ LA COPIA LA ESCRIBE UNA FUNCIÓN SQL Y NO C#
// El detalle son las 47 columnas del RETURNS TABLE de fn_seguimiento_diario_engorde × N días.
// `fn_congelar_liquidacion_engorde` hace el INSERT…SELECT desde la propia fn EN UN SOLO statement
// (CTEs modificantes = mismo snapshot): así la cabecera recién insertada no le tapa la lectura al
// detalle, y el tipado lo garantiza el motor — si el esquema deriva, falla el deploy, no producción.
// Serializar a DTO en C# y re-insertar sería una segunda forma de los mismos datos.
//
// QUÉ PROBLEMA RESUELVE
// `fn_seguimiento_diario_engorde` RECALCULA la tabla diaria en cada request; no es una foto. El
// 28-jul-2026 cambió la fórmula (v9→v12) y corridas CERRADAS hacía meses cambiaron solas, después
// de que Costos las había dado por cuadradas. Congelar al liquidar corta esa dependencia para los
// CINCO consumidores a la vez (tabla diaria, Reporte de Costos, Informe Semanal Panamá, Cuadre de
// alimento y saldo persistido), porque todos entran por la misma fn.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

internal static class LiquidacionCongeladaAplicador
{
    /// <summary>Versión de fórmula que estampan las copias nuevas (la fn también escribe 'v13').</summary>
    public const string FnVersionActual = "v13";

    // ── Ciclo de vida de la copia ────────────────────────────────────────────

    /// <summary>
    /// Congela la liquidación del lote: copia la tabla diaria completa (fn EN VIVO, ya con el
    /// estado 'Cerrado' aplicado) + cabecera con auditoría. Delega en
    /// <c>fn_congelar_liquidacion_engorde</c>.
    /// <para>
    /// ⚠️ Llamar DESPUÉS del SaveChanges que marca el lote 'Cerrado' (la fn fuerza el cierre en 0
    /// con ese estado; congelar antes guardaría una foto distinta a la que el usuario aprobó) y
    /// DENTRO de la transacción del cierre: si el congelado falla, la liquidación falla entera.
    /// </para>
    /// </summary>
    /// <returns>Id de la cabecera creada.</returns>
    public static async Task<long> CongelarAsync(
        ZooSanMarinoContext ctx, int loteAveEngordeId, string userId, string origen,
        CancellationToken ct = default)
    {
        return await ctx.Database
            .SqlQueryRaw<long>(
                @"SELECT fn_congelar_liquidacion_engorde({0}::int, {1}::text, {2}::text) AS ""Value""",
                loteAveEngordeId, userId, origen)
            .FirstAsync(ct);
    }

    /// <summary>
    /// Anula la copia vigente al reabrir (NO borra: queda el rastro de qué se había liquidado y con
    /// qué números — mismo criterio que el soft delete de LPP y `anulado` del histórico unificado).
    /// La fn v13 filtra <c>anulada_at IS NULL</c>, así que el lote vuelve al cálculo en vivo desde
    /// el instante de la reapertura. Va en la MISMA transacción que el cambio de estado.
    /// </summary>
    /// <returns>Cantidad de copias anuladas (0 o 1).</returns>
    public static Task<int> AnularAsync(
        ZooSanMarinoContext ctx, int loteAveEngordeId, string userId, string motivo,
        CancellationToken ct = default)
        => ctx.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE liquidacion_lote_engorde_congelada
   SET anulada_at = now(),
       anulada_por_user_id = {userId},
       anulada_motivo = {motivo}
 WHERE lote_ave_engorde_id = {loteAveEngordeId}
   AND anulada_at IS NULL", ct);

    /// <summary>
    /// Re-congela SIN reabrir: anula la copia vigente y crea una nueva con la fórmula de HOY, y
    /// refresca el resumen aprobado. Es el escape hatch para «se descubrió un bug en la fórmula
    /// después de congelar» (endpoint admin) y el cierre de la corrección de aves disponibles
    /// (<c>origen='correccion'</c>: la copia nunca queda mintiendo tras reparar el lote).
    /// </summary>
    /// <returns>Id de la copia nueva, o null si el lote no tenía copia vigente.</returns>
    public static async Task<long?> RecongelarYRefrescarResumenAsync(
        ZooSanMarinoContext ctx, int loteAveEngordeId, int companyId, string userId, string origen,
        CancellationToken ct = default)
    {
        var tieneVigente = await ctx.LiquidacionLoteEngordeCongelada.AsNoTracking()
            .AnyAsync(c => c.LoteAveEngordeId == loteAveEngordeId && c.AnuladaAt == null, ct);
        if (!tieneVigente) return null;

        var id = await ctx.Database
            .SqlQueryRaw<long>(
                @"SELECT fn_recongelar_liquidacion_engorde({0}::int, {1}::text, {2}::text) AS ""Value""",
                loteAveEngordeId, userId, origen)
            .FirstAsync(ct);

        // La columna persistida se alinea con la copia NUEVA antes de calcular el resumen,
        // que lee el saldo desde esa columna (idempotente: IS DISTINCT FROM).
        await SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync(ctx, loteAveEngordeId, ct);

        var resumen = await CalcularResumenVivoAsync(ctx, loteAveEngordeId, companyId, ct);
        if (resumen is not null)
            await ActualizarResumenCongeladoAsync(ctx, loteAveEngordeId, resumen, ct);
        return id;
    }

    // ── Resumen de liquidación (una sola fórmula) ────────────────────────────

    /// <summary>
    /// Resumen desde la COPIA congelada vigente, o <c>null</c> si no la hay o es de backfill
    /// (<c>total_aves_inicio IS NULL</c> ⇒ el resumen no se calculó al congelar → caller cae a vivo).
    /// </summary>
    public static async Task<LiquidacionLoteEngordeResumenDto?> LeerResumenCongeladoAsync(
        ZooSanMarinoContext ctx, int loteAveEngordeId, int companyId, CancellationToken ct = default)
    {
        return await ctx.LiquidacionLoteEngordeCongelada.AsNoTracking()
            .Where(c => c.LoteAveEngordeId == loteAveEngordeId
                     && c.CompanyId == companyId
                     && c.AnuladaAt == null
                     && c.TotalAvesInicio != null)
            .Select(c => new LiquidacionLoteEngordeResumenDto(
                c.LoteAveEngordeId,
                c.LoteNombre,
                c.EstadoOperativoLote,
                c.HembrasInicio,
                c.MachosInicio,
                c.MixtasInicio,
                c.TotalAvesInicio!.Value,
                c.VentasTotalHembras ?? 0,
                c.VentasTotalMachos ?? 0,
                c.VentasTotalMixtas ?? 0,
                c.AvesVivasActuales ?? 0,
                c.MovimientosVentaCount ?? 0,
                c.SaldoAlimentoKg,
                c.MermaUnidades,
                c.MermaKilos,
                c.CongeladaAt,
                c.FnVersion))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Cálculo EN VIVO del resumen de liquidación. Es el cuerpo que antes vivía DUPLICADO en
    /// <c>SeguimientoAvesEngordeService.Consultas</c> y
    /// <c>SeguimientoAvesEngordeEcuadorService.Consultas</c> (byte a byte): centralizado acá para
    /// que el cierre, el re-congelado y los dos services usen la MISMA fórmula
    /// (<see cref="LiquidacionEngordeCalculos"/>). Devuelve null si el lote no existe.
    /// </summary>
    public static async Task<LiquidacionLoteEngordeResumenDto?> CalcularResumenVivoAsync(
        ZooSanMarinoContext ctx, int loteId, int companyId, CancellationToken ct = default)
    {
        var lote = await ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new
            {
                l.LoteAveEngordeId,
                l.LoteNombre,
                l.EstadoOperativoLote,
                l.HembrasL,
                l.MachosL,
                l.Mixtas,
                l.AvesEncasetadas,
                l.MermaUnidades,
                l.MermaKilos,
                l.MortCajaH,
                l.MortCajaM
            })
            .SingleOrDefaultAsync(ct);
        if (lote is null) return null;

        var saldo = await ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderByDescending(s => s.Fecha)
            .Select(s => s.SaldoAlimentoKg)
            .FirstOrDefaultAsync(ct);

        var ventas = await ctx.LoteRegistroHistoricoUnificados.AsNoTracking()
            .Where(h => h.LoteAveEngordeId == loteId && h.CompanyId == companyId && !h.Anulado && h.TipoEvento == "VENTA_AVES")
            .ToListAsync(ct);

        var vh = ventas.Sum(v => v.CantidadHembras ?? 0);
        var vm = ventas.Sum(v => v.CantidadMachos ?? 0);
        var vx = ventas.Sum(v => v.CantidadMixtas ?? 0);

        // Encaset / inicio real: mismo criterio que historial_lote_pollo_engorde (Inicio al crear el lote).
        var ini = await ctx.HistorialLotePolloEngorde.AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId &&
                h.LoteAveEngordeId == loteId &&
                h.TipoLote == "LoteAveEngorde" &&
                h.TipoRegistro == "Inicio")
            .OrderBy(h => h.Id)
            .FirstOrDefaultAsync(ct);

        var (hInicio, mInicio, xInicio) = LiquidacionEngordeCalculos.CalcularAvesInicio(
            ini != null, ini?.AvesHembras ?? 0, ini?.AvesMachos ?? 0, ini?.AvesMixtas ?? 0,
            lote.HembrasL, lote.MachosL, lote.Mixtas, lote.AvesEncasetadas);

        var totalInicio = hInicio + mInicio + xInicio;

        // Aves vivas actuales: total inicio - (bajas acumuladas del seguimiento) - (ventas acumuladas)
        var bajas = await ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .Select(s =>
                (s.MortalidadHembras ?? 0) +
                (s.MortalidadMachos ?? 0) +
                (s.SelH ?? 0) +
                (s.SelM ?? 0) +
                (s.ErrorSexajeHembras ?? 0) +
                (s.ErrorSexajeMachos ?? 0))
            .SumAsync(ct);
        var mortCajaTotal = (lote.MortCajaH ?? 0) + (lote.MortCajaM ?? 0);
        var avesVivas = LiquidacionEngordeCalculos.CalcularAvesVivas(totalInicio, mortCajaTotal, bajas, vh + vm + vx);

        return new LiquidacionLoteEngordeResumenDto(
            lote.LoteAveEngordeId ?? loteId,
            lote.LoteNombre ?? "",
            lote.EstadoOperativoLote ?? "Abierto",
            hInicio,
            mInicio,
            xInicio,
            totalInicio,
            vh,
            vm,
            vx,
            avesVivas,
            ventas.Count,
            saldo,
            lote.MermaUnidades,
            lote.MermaKilos);
    }

    /// <summary>
    /// Escribe el resumen aprobado sobre la cabecera VIGENTE (los campos tipados que en el
    /// backfill quedaron NULL). Se llama dentro de la transacción del cierre/re-congelado.
    /// </summary>
    public static Task<int> ActualizarResumenCongeladoAsync(
        ZooSanMarinoContext ctx, int loteAveEngordeId, LiquidacionLoteEngordeResumenDto resumen,
        CancellationToken ct = default)
        => ctx.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE liquidacion_lote_engorde_congelada
   SET lote_nombre             = {resumen.LoteNombre},
       estado_operativo_lote   = {resumen.EstadoOperativoLote},
       hembras_inicio          = {resumen.HembrasInicio},
       machos_inicio           = {resumen.MachosInicio},
       mixtas_inicio           = {resumen.MixtasInicio},
       total_aves_inicio       = {resumen.TotalAvesInicio},
       ventas_total_hembras    = {resumen.VentasTotalHembras},
       ventas_total_machos     = {resumen.VentasTotalMachos},
       ventas_total_mixtas     = {resumen.VentasTotalMixtas},
       aves_vivas_actuales     = {resumen.AvesVivasActuales},
       movimientos_venta_count = {resumen.MovimientosVentaCount},
       saldo_alimento_kg       = {resumen.SaldoAlimentoKg},
       merma_unidades          = {resumen.MermaUnidades},
       merma_kilos             = {resumen.MermaKilos}
 WHERE lote_ave_engorde_id = {loteAveEngordeId}
   AND anulada_at IS NULL", ct);
}
