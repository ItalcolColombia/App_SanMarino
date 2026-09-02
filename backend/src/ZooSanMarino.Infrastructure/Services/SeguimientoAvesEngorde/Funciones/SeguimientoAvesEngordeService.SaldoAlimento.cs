// Recálculo del saldo de alimento (kg) por registro de seguimiento del lote.
// Partial de SeguimientoAvesEngordeService (camino de CARGA MASIVA).
//
// UNA SOLA IMPLEMENTACIÓN (jul-2026)
// Antes había TRES aritméticas del mismo saldo: `fn_seguimiento_diario_engorde`, este service y
// SeguimientoDiarioEngordeService. Divergieron —ventana previa al encaset, piso en 0, exclusión
// del ciclo anterior— y esa divergencia fue la causa directa de que el dato guardado y la pantalla
// mostraran números distintos: Kilometro 22 / G0036 tenía 11.380 kg guardados contra 3.420 en pantalla.
//
// Ahora los dos services delegan en SaldoAlimentoEngordeAplicador, que escribe la columna DESDE la fn.
// La columna pasa a ser idéntica a lo que ve el usuario por construcción, y no quedan dos fórmulas que
// puedan volver a separarse.
//
// La aritmética en C# no desapareció: SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento
// se conserva como ESPECIFICACIÓN EJECUTABLE de la fórmula. Sus tests son el contrato que la fn tiene
// que cumplir. No es código muerto: es el oráculo contra el que se valida el SQL.
using Microsoft.EntityFrameworkCore;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeService
{
    /// <summary>
    /// Recalcula y persiste <see cref="Domain.Entities.SeguimientoDiarioAvesEngorde.SaldoAlimentoKg"/>
    /// de todos los registros diarios del lote, tomando el valor de
    /// <c>fn_seguimiento_diario_engorde</c> — la misma fuente que pinta la tabla diaria.
    /// <para>
    /// Escribe por SQL, no por entidades rastreadas. Los llamadores ya hacen
    /// <c>Entry(ent).ReloadAsync()</c> antes de mapear la respuesta, así que el DTO sale con el valor
    /// nuevo.
    /// </para>
    /// </summary>
    private async Task RecalcularSaldoAlimentoPorLoteAsync(int loteId, int companyId, CancellationToken ct = default)
    {
        // Se conserva el alcance por empresa del comportamiento previo: un lote de otra empresa no se toca.
        var propio = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId
                        && l.CompanyId == companyId
                        && l.DeletedAt == null, ct);
        if (!propio)
            return;

        await SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync(_ctx, loteId, ct);
    }
}
