using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Cascada que hay que correr cuando cambia la FECHA o la HORA de encasetamiento de un lote de pollo
/// engorde (o de uno de sus lotes reproductora).
///
/// <para>
/// <b>Por qué existe.</b> Hasta el 11-sep-2026 el <c>PUT</c> del lote guardaba la fecha nueva y nada
/// más: el cruce reproductora → pollo engorde solo se re-corre con el trigger de la tabla de
/// seguimiento reproductora, así que las filas del cruce quedaban fechadas con el encasetamiento
/// VIEJO y el saldo de alimento calculado sobre una ventana que ya no existía. Corregir una fecha
/// terminaba siempre en un ticket para desarrollo (pedido de operación de Panamá).
/// </para>
///
/// <para>
/// <b>El orden importa y no es intercambiable:</b>
/// <list type="number">
///   <item>el cruce re-fecha sus filas (las borra y las reinserta desde la reproductora);</item>
///   <item>recién entonces las bajas de esas filas pueden bajar al maestro de aves del lote;</item>
///   <item>y el saldo de alimento se reescribe al final, porque el cruce dejó la columna en
///         <c>NULL</c> al reinsertar y la fn del saldo necesita ver las filas ya re-fechadas.</item>
/// </list>
/// </para>
///
/// <para>
/// Debe llamarse DESPUÉS del <c>SaveChangesAsync</c> que persiste la fecha nueva: la fn de BD lee
/// <c>lote_ave_engorde.fecha_encaset</c> en vivo y con el cambio todavía en el ChangeTracker
/// recalcularía contra la fecha vieja.
/// </para>
/// </summary>
internal static class RecalculoEncasetamientoEngordeAplicador
{
    /// <summary>
    /// Re-corre el cruce del lote, sincroniza sus bajas con el maestro de aves y reescribe el saldo
    /// de alimento de toda la serie.
    /// <para>
    /// Idempotente: correrlo dos veces seguidas deja exactamente el mismo estado.
    /// </para>
    /// </summary>
    public static async Task AplicarAsync(
        ZooSanMarinoContext ctx, int companyId, int loteAveEngordeId, CancellationToken ct = default)
    {
        if (loteAveEngordeId <= 0) return;

        // 1) El cruce, con la fecha/hora nuevas. Un lote sin lotes reproductora entra igual: la fn
        //    limpia el cruce previo y sale, que es exactamente lo que corresponde.
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT fn_cruce_reproductora_a_engorde({loteAveEngordeId}::int)", ct);

        // 2) Las bajas del cruce al maestro de aves del lote (hembras_l/machos_l). Sin esto el lote
        //    queda con más aves de las que tiene vivas y el sistema deja despachar aves muertas.
        await RetiroAvesEngordeAplicador.SincronizarCruceAsync(ctx, companyId, loteAveEngordeId);

        // 3) El saldo de alimento de toda la serie diaria, desde fn_seguimiento_diario_engorde.
        await SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync(ctx, loteAveEngordeId, ct);
    }
}
