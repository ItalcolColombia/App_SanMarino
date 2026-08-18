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
}
