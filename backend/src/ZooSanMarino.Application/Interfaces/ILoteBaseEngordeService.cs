// src/ZooSanMarino.Application/Interfaces/ILoteBaseEngordeService.cs
using ZooSanMarino.Application.DTOs.LoteBaseEngorde;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Catálogo de lotes base de pollo engorde (agrupador global por empresa).
/// La asignación a lote_ave_engorde es opcional; sirve para reportes por granja.
/// </summary>
public interface ILoteBaseEngordeService
{
    /// <summary>
    /// Lista los lotes base vivos de la empresa efectiva (con conteo de lotes amarrados).
    /// Con <paramref name="soloVigentes"/>: solo activos y del año en curso (o sin fecha de
    /// activación) — es lo que consume el selector de crear-lote en Panamá.
    /// </summary>
    Task<IReadOnlyList<LoteBaseEngordeDto>> GetAllAsync(bool soloVigentes = false, CancellationToken ct = default);

    /// <summary>Crea un lote base; nombre único por empresa (case-insensitive entre vivos).</summary>
    Task<LoteBaseEngordeDto> CreateAsync(CreateLoteBaseEngordeDto dto, CancellationToken ct = default);

    Task<LoteBaseEngordeDto?> UpdateAsync(UpdateLoteBaseEngordeDto dto, CancellationToken ct = default);

    /// <summary>Activa/desactiva manualmente (inactivo no aparece en el selector de crear-lote).</summary>
    Task<LoteBaseEngordeDto?> SetActivoAsync(int id, bool activo, CancellationToken ct = default);

    /// <summary>Soft-delete. Falla si tiene lotes de engorde vivos amarrados.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
