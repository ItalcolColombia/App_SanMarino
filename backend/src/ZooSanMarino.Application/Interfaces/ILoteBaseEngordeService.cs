// src/ZooSanMarino.Application/Interfaces/ILoteBaseEngordeService.cs
using ZooSanMarino.Application.DTOs.LoteBaseEngorde;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Catálogo de lotes base de pollo engorde (agrupador global por empresa).
/// La asignación a lote_ave_engorde es opcional; sirve para reportes por granja.
/// </summary>
public interface ILoteBaseEngordeService
{
    /// <summary>Lista los lotes base vivos de la empresa efectiva (con conteo de lotes amarrados).</summary>
    Task<IReadOnlyList<LoteBaseEngordeDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Crea un lote base; nombre único por empresa (case-insensitive entre vivos).</summary>
    Task<LoteBaseEngordeDto> CreateAsync(CreateLoteBaseEngordeDto dto, CancellationToken ct = default);

    Task<LoteBaseEngordeDto?> UpdateAsync(UpdateLoteBaseEngordeDto dto, CancellationToken ct = default);

    /// <summary>Soft-delete. Falla si tiene lotes de engorde vivos amarrados.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
