// src/ZooSanMarino.Application/Interfaces/ILoteBaseEngordeService.cs
using ZooSanMarino.Application.DTOs.LoteBaseEngorde;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Catálogo de lotes base de pollo engorde (agrupador global por empresa).
/// La visibilidad al crear lote se parametriza por granja: cada lote base trae sus
/// <c>GranjaIds</c> asignados y solo aparece en el selector de esas granjas.
/// </summary>
public interface ILoteBaseEngordeService
{
    /// <summary>
    /// Lista los lotes base vivos de la empresa efectiva, con conteo de lotes amarrados,
    /// granjas asignadas (<c>GranjaIds</c>) y nombre del creador.
    /// </summary>
    Task<IReadOnlyList<LoteBaseEngordeDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Crea un lote base (solo nombre; fecha de activación y usuario automáticos).</summary>
    Task<LoteBaseEngordeDto> CreateAsync(CreateLoteBaseEngordeDto dto, CancellationToken ct = default);

    /// <summary>Renombra el lote base (nombre único por empresa entre vivos).</summary>
    Task<LoteBaseEngordeDto?> UpdateAsync(UpdateLoteBaseEngordeDto dto, CancellationToken ct = default);

    /// <summary>Activa/desactiva manualmente (inactivo no aparece en ningún crear-lote).</summary>
    Task<LoteBaseEngordeDto?> SetActivoAsync(int id, bool activo, CancellationToken ct = default);

    /// <summary>Soft-delete. Falla si tiene lotes de engorde vivos amarrados; limpia las asignaciones.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Granjas asignadas a un lote base (visibilidad al crear lote).</summary>
    Task<IReadOnlyList<LoteBaseEngordeGranjaDto>> GetGranjasAsync(int loteBaseId, CancellationToken ct = default);

    /// <summary>Asigna una granja al lote base (idempotente). Valida empresa efectiva.</summary>
    Task<LoteBaseEngordeGranjaDto?> AssignGranjaAsync(int loteBaseId, int farmId, CancellationToken ct = default);

    /// <summary>Quita una granja del lote base.</summary>
    Task<bool> UnassignGranjaAsync(int loteBaseId, int farmId, CancellationToken ct = default);
}
