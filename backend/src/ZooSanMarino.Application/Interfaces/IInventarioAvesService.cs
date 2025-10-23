// src/ZooSanMarino.Application/Interfaces/IInventarioAvesService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para gestión de inventario de aves
/// </summary>
public interface IInventarioAvesService
{
    // CRUD básico
    Task<InventarioAvesDto> CreateAsync(CreateInventarioAvesDto dto);
    Task<InventarioAvesDto?> GetByIdAsync(int id);
    Task<IEnumerable<InventarioAvesDto>> GetAllAsync();
    Task<InventarioAvesDto> UpdateAsync(UpdateInventarioAvesDto dto);
    Task<bool> DeleteAsync(int id);
    
    // Búsqueda y filtrado
    Task<ZooSanMarino.Application.DTOs.Common.PagedResult<InventarioAvesDto>> SearchAsync(InventarioAvesSearchRequest request);
    Task<IEnumerable<InventarioAvesDto>> GetByLoteIdAsync(int loteId);
    Task<IEnumerable<InventarioAvesDto>> GetByUbicacionAsync(int granjaId, string? nucleoId = null, string? galponId = null);
    
    // Operaciones de inventario
    Task<InventarioAvesDto> AjustarInventarioAsync(int inventarioId, int hembras, int machos, int mixtas, string motivo, string? observaciones = null);
    Task<ResultadoMovimientoDto> TrasladarInventarioAsync(int inventarioId, int granjaDestinoId, string? nucleoDestinoId, string? galponDestinoId, string? motivo = null);
    
    // Estado y resúmenes
    Task<EstadoLoteDto> GetEstadoLoteAsync(int loteId);
    Task<IEnumerable<ResumenInventarioDto>> GetResumenPorUbicacionAsync(int? granjaId = null);
    Task<IEnumerable<InventarioAvesDto>> GetInventariosActivosAsync();
    
    // Validaciones
    Task<bool> ExisteInventarioAsync(int loteId, int granjaId, string? nucleoId, string? galponId);
    Task<bool> PuedeRealizarMovimientoAsync(int inventarioId, int hembras, int machos, int mixtas);
    
    // Inicialización desde lote existente
    Task<InventarioAvesDto> InicializarDesdeLotelAsync(int loteId);
    Task<IEnumerable<InventarioAvesDto>> SincronizarInventariosAsync();
    
    // Debug
    Task<int> GetTotalCountAsync();
}
