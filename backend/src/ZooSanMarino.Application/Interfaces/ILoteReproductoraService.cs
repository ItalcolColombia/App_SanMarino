using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ILoteReproductoraService
{
    Task<IEnumerable<LoteReproductoraDto>> GetAllAsync(string? loteId = null);  // Cambiado a string para coincidir con BD
    Task<LoteReproductoraDto?>             GetByIdAsync(string loteId, string repId);  // Cambiado a string
    Task<LoteReproductoraDto>              CreateAsync(CreateLoteReproductoraDto dto);
    Task<IEnumerable<LoteReproductoraDto>> CreateBulkAsync(IEnumerable<CreateLoteReproductoraDto> dtos);
    Task<LoteReproductoraDto?>             UpdateAsync(UpdateLoteReproductoraDto dto);
    Task<bool>                             DeleteAsync(string loteId, string repId);  // Cambiado a string
    Task<AvesDisponiblesDto?>              GetAvesDisponiblesAsync(string loteId);  // Cambiado a string
}
