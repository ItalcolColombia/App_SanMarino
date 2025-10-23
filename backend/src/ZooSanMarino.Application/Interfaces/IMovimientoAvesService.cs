// src/ZooSanMarino.Application/Interfaces/IMovimientoAvesService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para gestión de movimientos y traslados de aves
/// </summary>
public interface IMovimientoAvesService
{
    // CRUD básico
    Task<MovimientoAvesDto> CreateAsync(CreateMovimientoAvesDto dto);
    Task<MovimientoAvesDto?> GetByIdAsync(int id);
    Task<MovimientoAvesDto?> GetByNumeroMovimientoAsync(string numeroMovimiento);
    Task<IEnumerable<MovimientoAvesDto>> GetAllAsync();
    
    // Búsqueda y filtrado
    Task<ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoAvesDto>> SearchAsync(MovimientoAvesSearchRequest request);
    Task<IEnumerable<MovimientoAvesDto>> GetMovimientosPendientesAsync();
    Task<IEnumerable<MovimientoAvesDto>> GetMovimientosByLoteAsync(int loteId);  // Changed from string to int
    Task<IEnumerable<MovimientoAvesDto>> GetMovimientosByUsuarioAsync(int usuarioId);
    
    // Operaciones de movimiento
    Task<ResultadoMovimientoDto> ProcesarMovimientoAsync(ProcesarMovimientoDto dto);
    Task<ResultadoMovimientoDto> CancelarMovimientoAsync(CancelarMovimientoDto dto);
    Task<ResultadoMovimientoDto> TrasladoRapidoAsync(TrasladoRapidoDto dto);
    
    // Traslados específicos
    Task<ResultadoMovimientoDto> TrasladarEntreGranjasAsync(int loteId, int granjaOrigenId, int granjaDestinoId, int hembras, int machos, int mixtas, string? motivo = null);  // Changed from string to int
    Task<ResultadoMovimientoDto> TrasladarDentroGranjaAsync(int loteId, int granjaId, string? nucleoOrigenId, string? galponOrigenId, string? nucleoDestinoId, string? galponDestinoId, int hembras, int machos, int mixtas, string? motivo = null);  // Changed from string to int
    Task<ResultadoMovimientoDto> DividirLoteAsync(int loteOrigenId, int loteDestinoId, int hembras, int machos, int mixtas, string? motivo = null);  // Changed from string to int
    Task<ResultadoMovimientoDto> UnificarLotesAsync(int loteOrigenId, int loteDestinoId, string? motivo = null);  // Changed from string to int
    
    // Validaciones
    Task<bool> ValidarMovimientoAsync(CreateMovimientoAvesDto dto);
    Task<List<string>> ValidarDisponibilidadAvesAsync(int inventarioOrigenId, int hembras, int machos, int mixtas);
    Task<bool> ValidarUbicacionDestinoAsync(int granjaId, string? nucleoId, string? galponId);
    
    // Estadísticas
    Task<IEnumerable<MovimientoAvesDto>> GetMovimientosRecientesAsync(int dias = 7);
    Task<int> GetTotalMovimientosPendientesAsync();
    Task<int> GetTotalMovimientosCompletadosAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null);
    
    // Navegación completa
    Task<ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoAvesCompletoDto>> SearchCompletoAsync(MovimientoAvesCompletoSearchRequest request);
    Task<MovimientoAvesCompletoDto?> GetCompletoByIdAsync(int id);
    Task<IEnumerable<ResumenTrasladoDto>> GetResumenesRecientesAsync(int dias = 7, int limite = 10);
    Task<EstadisticasTrasladoDto> GetEstadisticasCompletasAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null);
}
