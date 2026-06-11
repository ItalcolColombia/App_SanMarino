// src/ZooSanMarino.Application/Interfaces/ICorreccionAvesDisponiblesEngordeService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Validación y corrección de descuadres entre el maestro de aves (hembras_l/machos_l),
/// la disponibilidad y la realidad del seguimiento diario en lotes de pollo engorde.
/// Casos: ventas Pendientes vencidas sin confirmar, ventas Completadas que no descontaron
/// el maestro (bug histórico) y lotes Cerrados con disponibles fantasma.
/// </summary>
public interface ICorreccionAvesDisponiblesEngordeService
{
    /// <summary>Diagnóstico por nombre de lote; loteNombre null ⇒ todos los lotes engorde de la company.</summary>
    Task<IReadOnlyList<ValidacionAvesDisponiblesLoteDto>> ValidarPorNombreAsync(string? loteNombre, CancellationToken ct = default);

    /// <summary>
    /// Corrige los lotes con descuadre: (1) confirma ventas Pendientes vencidas vía el flujo
    /// real de la app, (2) re-sincroniza el maestro cuando ventas Completadas no descontaron
    /// (solo con evidencia exacta), (3) lleva a 0 los disponibles fantasma de lotes Cerrados.
    /// Nunca aumenta saldos; deja auditoría; idempotente. Con DryRun=true solo reporta.
    /// </summary>
    Task<CorreccionAvesDisponiblesResponse> CorregirPorNombreAsync(CorregirAvesDisponiblesRequest request, CancellationToken ct = default);
}
