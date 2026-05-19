using ZooSanMarino.Application.DTOs.Cliente;
using ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

public interface IClienteService
{
    Task<IEnumerable<ClienteDto>> GetAllAsync(CancellationToken ct = default);
    Task<ClienteDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PagedResult<ClienteDto>> SearchAsync(ClienteSearchRequest req, CancellationToken ct = default);
    Task<ClienteDto> CreateAsync(CreateClienteRequest dto, CancellationToken ct = default);
    Task<ClienteDto?> UpdateAsync(int id, UpdateClienteRequest dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
