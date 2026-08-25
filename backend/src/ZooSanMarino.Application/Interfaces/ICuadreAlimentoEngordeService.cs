// src/ZooSanMarino.Application/Interfaces/ICuadreAlimentoEngordeService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Verifica el invariante del alimento de pollo engorde:
/// <c>saldo del ciclo activo == stock físico − movimientos posteriores al último seguimiento</c>.
/// <para>
/// El descuadre que originó el trabajo de jul-2026 lo detectó un humano de operación, semanas después
/// de producirse; nada en el sistema lo verificaba. Esto lo pone a la vista el mismo día.
/// </para>
/// </summary>
public interface ICuadreAlimentoEngordeService
{
    /// <summary>Cuadre de todos los galpones de la empresa activa.</summary>
    Task<CuadreAlimentoEngordeDto> ObtenerAsync(bool soloConProblemas = false, CancellationToken ct = default);

    /// <summary>
    /// Lotes ya liquidados que congelaron su liquidación con alimento en el galpón (anomalía R2).
    /// <para>
    /// La regla operativa es que al liquidar el galpón queda en cero y el sobrante se traslada. Esto
    /// no bloquea nada: SEÑALA lo que quedó, que es lo que pidió el dueño del producto.
    /// </para>
    /// </summary>
    /// <param name="soloAnomalias">
    /// Si es <c>true</c>, el detalle deja fuera los lotes cuyo sobrante sí se trasladó. El resumen se
    /// calcula siempre sobre el total.
    /// </param>
    Task<AnomaliaAlimentoLiquidadoDto> ObtenerLiquidadosConAlimentoAsync(
        bool soloAnomalias = false, CancellationToken ct = default);

    /// <summary>
    /// Cierra el descuadre de un galpón: el operador declara los kilos que realmente hay y el sistema
    /// escribe lo que falta de cada lado.
    ///
    /// <para>
    /// <b>El saldo de la tabla diaria no es un campo, es un derivado</b> —
    /// <c>apertura + Σ(ingresos y traslados) − Σ(consumo del seguimiento)</c>—, así que no se puede
    /// «editar»: se corrige el insumo que está mal. Y hay dos, con arreglos opuestos: si sobra stock
    /// se escribe un <c>AjusteStock</c> (que la tabla no ve, y está bien porque la tabla ya tenía
    /// razón); si sobra tabla se escribe un <c>AjusteCuadreTabla*</c> (que el stock no ve, por lo
    /// mismo del otro lado). Lo normal es que solo uno de los dos se mueva.
    /// </para>
    ///
    /// <para>
    /// Los <b>movimientos posteriores</b> al último seguimiento no se tocan nunca: son alimento real,
    /// bien registrado, que todavía no tiene día donde reflejarse en la tabla.
    /// </para>
    /// </summary>
    Task<CuadrarGalponAlimentoResultDto> CuadrarGalponAsync(
        CuadrarGalponAlimentoRequest req, CancellationToken ct = default);
}
