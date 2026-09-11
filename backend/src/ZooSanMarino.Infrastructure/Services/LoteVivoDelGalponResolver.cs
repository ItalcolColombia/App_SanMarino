// src/ZooSanMarino.Infrastructure/Services/LoteVivoDelGalponResolver.cs
// El lote de engorde al que el TRIGGER le va a atribuir un movimiento de inventario.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Pregunta a <c>fn_lote_ave_engorde_id_desde_ubicacion</c> —la MISMA función que usa el trigger
/// <c>trg_lote_hist_desde_inventario_gestion</c>— qué lote de engorde va a quedar atribuido a un
/// movimiento escrito en ese galpón, o <c>null</c> si no queda ninguno vivo.
///
/// <para>
/// <b>Por qué esto existe como un solo lugar.</b> Los dos escritores de
/// <c>AjusteCuadreTablaEntrada</c>/<c>Salida</c> —«Cuadrar galpón» y la eliminación de un registro
/// de stock— necesitan saber, ANTES de escribir, si hay un ciclo vivo al que corregirle la tabla
/// diaria. Repetir el criterio (<c>deleted_at IS NULL</c> + <c>estado_operativo_lote &lt;&gt;
/// 'Cerrado'</c> + el último id) en LINQ, en cada uno, es dos definiciones de «lote vivo» que se
/// separan en la primera vez que alguien toque una: el movimiento se decidiría con un criterio y se
/// atribuiría con otro. Una sola fórmula por número.
/// </para>
///
/// <para>
/// El defecto que motivó centralizarlo: ticket 10-sep-2026, DOÑA MARIA / núcleo C / galpón 2. Ver
/// <c>fase_de_desarrollo/apertura_engorde_ajuste_cuadre_huerfano_plan.md</c>.
/// </para>
/// </summary>
public static class LoteVivoDelGalponResolver
{
    /// <summary>
    /// El lote vivo del galpón según el criterio del trigger, o <c>null</c> si no hay ninguno.
    /// </summary>
    public static async Task<int?> ResolverAsync(
        ZooSanMarinoContext db, int farmId, string? nucleoId, string? galponId, CancellationToken ct)
    {
        // El alias `Value` es la convención de EF para `SqlQueryRaw<T>` sobre un escalar; es el mismo
        // patrón que usa LiquidacionCongeladaAplicador.
        var ids = await db.Database
            .SqlQueryRaw<int?>(
                "SELECT public.fn_lote_ave_engorde_id_desde_ubicacion({0}::int, {1}::varchar, {2}::varchar) AS \"Value\"",
                farmId, (object?)nucleoId ?? DBNull.Value, (object?)galponId ?? DBNull.Value)
            .ToListAsync(ct);

        return ids.Count > 0 ? ids[0] : null;
    }
}
