// src/ZooSanMarino.Infrastructure/Services/SaldoAlimentoEngordeAplicador.cs
// Mantiene al día la columna `seguimiento_diario_aves_engorde.saldo_alimento_kg` cuando cambia el
// INVENTARIO del galpón. Vive acá, como estático que recibe el contexto, por el mismo motivo que
// RetiroAvesEngordeAplicador: lo necesitan módulos que no se pueden inyectar entre sí sin ciclo
// (InventarioGestionService no puede depender de los services de seguimiento, y al revés tampoco).
//
// EL PROBLEMA QUE RESUELVE (jul-2026)
// `RecalcularSaldoAlimentoPorLoteAsync` solo corría al crear o editar un seguimiento diario, así que
// un ingreso o traslado registrado DESPUÉS del último día cargado nunca actualizaba la columna. La
// grilla no se veía afectada —`fn_seguimiento_diario_engorde` recalcula en vivo— pero la columna
// alimenta la liquidación y «Cuadrar Saldos», y ahí el número quedaba viejo: Kilometro 61 / G0037
// mostraba 2.360 kg contra 12.360 de stock real porque los 10.000 kg del documento 63020 entraron el
// mismo día del último seguimiento y nadie volvió a tocar el registro.
//
// POR QUÉ RECALCULA DESDE LA FUNCIÓN SQL Y NO EN C#
// Había TRES implementaciones del saldo (la fn, SeguimientoAvesEngordeService para la carga masiva y
// SeguimientoAvesEngordeEcuadorService para el formulario diario) y su divergencia fue justamente la
// causa de que el dato guardado y la pantalla mostraran números distintos. La fn es la fuente de
// verdad validada contra el stock físico, así que el saldo persistido se escribe DESDE ella: la
// columna pasa a ser, por construcción, idéntica a lo que ve el usuario. Es el mismo SQL que corre la
// migración 20260730091000_RecalcularSaldoAlimentoEngordeV11.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

internal static class SaldoAlimentoEngordeAplicador
{
    /// <summary>
    /// Reescribe <c>saldo_alimento_kg</c> de todos los registros diarios de UN lote con el valor que
    /// devuelve <c>fn_seguimiento_diario_engorde</c>.
    /// <para>
    /// Idempotente: el <c>IS DISTINCT FROM</c> hace que una segunda pasada no toque ninguna fila (y
    /// de paso evita despertar triggers de <c>updated_at</c> sin necesidad).
    /// </para>
    /// <para>
    /// Debe llamarse DESPUÉS del <c>SaveChangesAsync</c> que persiste el movimiento: la fila del
    /// histórico la escribe el trigger <c>trg_inventario_gestion_movimiento_lote_hist</c> en el INSERT,
    /// así que recién ahí la fn puede verla.
    /// </para>
    /// </summary>
    /// <returns>Cantidad de filas efectivamente modificadas.</returns>
    public static Task<int> RecalcularPorLoteAsync(
        ZooSanMarinoContext ctx, int loteAveEngordeId, CancellationToken ct = default)
        => ctx.Database.ExecuteSqlInterpolatedAsync($@"
WITH nuevos AS (
    SELECT f.seg_id,
           ROUND(f.saldo_alimento_kg::numeric, 3) AS saldo
      FROM fn_seguimiento_diario_engorde({loteAveEngordeId}::int) f
     WHERE f.seg_id IS NOT NULL
)
UPDATE seguimiento_diario_aves_engorde s
   SET saldo_alimento_kg = n.saldo
  FROM nuevos n
 WHERE s.id = n.seg_id
   AND s.saldo_alimento_kg IS DISTINCT FROM n.saldo", ct);

    /// <summary>
    /// Recalcula el saldo de TODOS los lotes de pollo engorde de un galpón tras un movimiento de
    /// inventario.
    /// <para>
    /// Recalcula el galpón entero, no solo el lote vigente, por dos razones: la bodega es compartida
    /// entre los lotes que conviven (v10), y un movimiento puede venir fechado hacia atrás y caer
    /// dentro del rango de un ciclo anterior. Son 1 a 4 lotes por galpón, así que el costo es del
    /// orden de unas pocas llamadas a la fn.
    /// </para>
    /// <para>
    /// <b>Nivel granja:</b> si el movimiento no tiene galpón (bodega de granja) no afecta el saldo de
    /// ningún lote —la fn filtra por galpón— y este método no hace nada.
    /// </para>
    /// <para>
    /// <b>Nunca tumba la operación de inventario.</b> El saldo persistido es una proyección de la fn:
    /// si el recálculo falla, la pantalla sigue mostrando el número correcto porque recalcula en vivo,
    /// y lo único que pasa es que la columna queda vieja — exactamente el estado previo a este
    /// aplicador. Hacer fallar un ingreso ya guardado por no poder refrescar una proyección sería
    /// peor. El error se registra para que no pase inadvertido.
    /// </para>
    /// </summary>
    /// <returns>Cantidad de filas modificadas en total.</returns>
    public static async Task<int> RecalcularPorUbicacionAsync(
        ZooSanMarinoContext ctx,
        int companyId,
        int farmId,
        string? nucleoId,
        string? galponId,
        Action<Exception>? onError = null,
        CancellationToken ct = default)
    {
        var nucleo = (nucleoId ?? "").Trim();
        var galpon = (galponId ?? "").Trim();
        if (galpon.Length == 0)
            return 0;

        List<int> lotes;
        try
        {
            lotes = await ctx.LoteAveEngorde.AsNoTracking()
                .Where(l => l.CompanyId == companyId
                         && l.DeletedAt == null
                         && l.GranjaId == farmId
                         && (l.NucleoId == null ? "" : l.NucleoId.Trim()) == nucleo
                         && (l.GalponId == null ? "" : l.GalponId.Trim()) == galpon
                         && l.LoteAveEngordeId != null
                         // Sin seguimiento no hay nada que recalcular.
                         && ctx.SeguimientoDiarioAvesEngorde.Any(s => s.LoteAveEngordeId == l.LoteAveEngordeId))
                .Select(l => l.LoteAveEngordeId!.Value)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (onError is not null)
        {
            onError(ex);
            return 0;
        }

        var filas = 0;
        foreach (var loteId in lotes)
        {
            try
            {
                filas += await RecalcularPorLoteAsync(ctx, loteId, ct);
            }
            catch (Exception ex) when (onError is not null)
            {
                // Un lote con datos corruptos no puede bloquear el inventario del galpón entero.
                onError(ex);
            }
        }
        return filas;
    }
}
