using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ILoteReproductoraAveEngordeService
{
    Task<IEnumerable<LoteReproductoraAveEngordeDto>> GetAllAsync(int? loteAveEngordeId = null);
    Task<LoteReproductoraAveEngordeDto?> GetByIdAsync(int id);
    Task<LoteReproductoraAveEngordeDto> CreateAsync(CreateLoteReproductoraAveEngordeDto dto);
    Task<IEnumerable<LoteReproductoraAveEngordeDto>> CreateBulkAsync(IEnumerable<CreateLoteReproductoraAveEngordeDto> dtos);
    Task<LoteReproductoraAveEngordeDto?> UpdateAsync(int id, UpdateLoteReproductoraAveEngordeDto dto);
    Task<bool> DeleteAsync(int id);
    Task<AvesDisponiblesDto?> GetAvesDisponiblesAsync(int loteAveEngordeId);
    /// <summary>Código único para nuevo registro: prefijo LR- + 10 dígitos aleatorios, sin repetirse en el lote ni en exclude.</summary>
    Task<string> GetNewReproductoraCodeAsync(int loteAveEngordeId, IEnumerable<string>? exclude = null);
}
