using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ILotePosturaBaseService
{
    Task<IEnumerable<LotePosturaBaseDto>> GetAllAsync();
    Task<LotePosturaBaseDto?> GetByIdAsync(int id);
    Task<LotePosturaBaseDto> CreateAsync(CreateLotePosturaBaseDto dto);
    Task<LotePosturaBaseDto> UpdateAsync(int id, UpdateLotePosturaBaseDto dto);
    Task DeleteAsync(int id);
}

