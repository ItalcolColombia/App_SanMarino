using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ILoteReproductoraService
{
    Task<IEnumerable<LoteReproductoraDto>> GetAllAsync(int? loteId = null);  // Changed from string? to int?
    Task<LoteReproductoraDto?>             GetByIdAsync(int loteId, string repId);  // Changed from string to int
    Task<LoteReproductoraDto>              CreateAsync(CreateLoteReproductoraDto dto);
    Task<IEnumerable<LoteReproductoraDto>> CreateBulkAsync(IEnumerable<CreateLoteReproductoraDto> dtos); // 👈 nuevo
    Task<LoteReproductoraDto?>             UpdateAsync(UpdateLoteReproductoraDto dto);
    Task<bool>                             DeleteAsync(int loteId, string repId);  // Changed from string to int
}
