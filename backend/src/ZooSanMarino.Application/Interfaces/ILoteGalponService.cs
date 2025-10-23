// src/ZooSanMarino.Application/Interfaces/ILoteGalponService.cs
using ZooSanMarino.Application.DTOs;
namespace ZooSanMarino.Application.Interfaces;
public interface ILoteGalponService
{
    Task<IEnumerable<LoteGalponDto>> GetAllAsync();
    Task<LoteGalponDto?>            GetByIdAsync(int loteId, string repId, string galponId);  // Changed from string to int
    Task<LoteGalponDto>             CreateAsync(CreateLoteGalponDto dto);
    Task<LoteGalponDto?>            UpdateAsync(UpdateLoteGalponDto dto);
    Task<bool>                      DeleteAsync(int loteId, string repId, string galponId);  // Changed from string to int
}
