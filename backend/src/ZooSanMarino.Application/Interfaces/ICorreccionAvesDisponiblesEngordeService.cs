// src/ZooSanMarino.Application/Interfaces/ICorreccionAvesDisponiblesEngordeService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Validación y corrección del descuadre entre las aves disponibles del maestro
/// (hembras_l/machos_l − bajas del seguimiento) y la realidad del seguimiento diario
/// en lotes de pollo engorde. Caso típico: lote Cerrado/liquidado que aún reporta
/// disponibles porque las últimas bajas/ventas se registraron con el género contrario.
/// </summary>
public interface ICorreccionAvesDisponiblesEngordeService
{
    /// <summary>Diagnóstico por nombre de lote (todos los lotes de la company con ese nombre).</summary>
    Task<IReadOnlyList<ValidacionAvesDisponiblesLoteDto>> ValidarPorNombreAsync(string loteNombre, CancellationToken ct = default);

    /// <summary>
    /// Corrige los lotes CERRADOS con disponibles fantasma: descuenta el sobrante de
    /// hembras_l/machos_l (nunca aumenta) y deja auditoría en historial_lote_pollo_engorde
    /// (TipoRegistro="Ajuste"). Idempotente. Con DryRun=true solo reporta.
    /// </summary>
    Task<CorreccionAvesDisponiblesResponse> CorregirPorNombreAsync(CorregirAvesDisponiblesRequest request, CancellationToken ct = default);
}
