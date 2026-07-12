// MovimientoAves/Funciones/MovimientoAvesService.Consultas.cs
// Lecturas del movimiento: por id/número, listados, búsqueda paginada y filtros por lote/usuario.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    public async Task<MovimientoAvesDto?> GetByIdAsync(int id)
    {
        return await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.Id == id && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .Select(ToDto)
            .FirstOrDefaultAsync();
    }

    public async Task<MovimientoAvesDto?> GetByNumeroMovimientoAsync(string numeroMovimiento)
    {
        return await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.NumeroMovimiento == numeroMovimiento && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .Select(ToDto)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<MovimientoAvesDto>> GetAllAsync()
    {
        return await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .OrderByDescending(m => m.FechaMovimiento)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoAvesDto>> SearchAsync(MovimientoAvesSearchRequest request)
    {
        var query = _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null);

        // Aplicar filtros
        if (!string.IsNullOrEmpty(request.NumeroMovimiento))
            query = query.Where(m => m.NumeroMovimiento.Contains(request.NumeroMovimiento));

        if (!string.IsNullOrEmpty(request.TipoMovimiento))
            query = query.Where(m => m.TipoMovimiento == request.TipoMovimiento);

        if (!string.IsNullOrEmpty(request.Estado))
            query = query.Where(m => m.Estado == request.Estado);

        if (request.LoteOrigenId.HasValue)  // Changed from string.IsNullOrEmpty check
            query = query.Where(m => m.LoteOrigenId == request.LoteOrigenId.Value);  // Changed from request.LoteOrigenId

        if (request.LoteDestinoId.HasValue)  // Changed from string.IsNullOrEmpty check
            query = query.Where(m => m.LoteDestinoId == request.LoteDestinoId.Value);  // Changed from request.LoteDestinoId

        if (request.GranjaOrigenId.HasValue)
            query = query.Where(m => m.GranjaOrigenId == request.GranjaOrigenId.Value);

        if (request.GranjaDestinoId.HasValue)
            query = query.Where(m => m.GranjaDestinoId == request.GranjaDestinoId.Value);

        if (request.FechaDesde.HasValue)
            query = query.Where(m => m.FechaMovimiento >= request.FechaDesde.Value);

        if (request.FechaHasta.HasValue)
            query = query.Where(m => m.FechaMovimiento <= request.FechaHasta.Value);

        if (request.UsuarioMovimientoId.HasValue)
            query = query.Where(m => m.UsuarioMovimientoId == request.UsuarioMovimientoId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.FechaMovimiento)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ToDto)
            .ToListAsync();

        return new ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoAvesDto>
        {
            Items = items,
            Total = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<IEnumerable<MovimientoAvesDto>> GetMovimientosPendientesAsync()
    {
        return await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.Estado == "Pendiente" && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .OrderBy(m => m.FechaMovimiento)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<IEnumerable<MovimientoAvesDto>> GetMovimientosByLoteAsync(int loteId)  // Changed from string to int
    {
        return await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => (m.LoteOrigenId == loteId || m.LoteDestinoId == loteId) &&  // Changed from loteId
                       m.CompanyId == _currentUser.CompanyId &&
                       m.DeletedAt == null)
            .OrderByDescending(m => m.FechaMovimiento)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<IEnumerable<MovimientoAvesDto>> GetMovimientosByUsuarioAsync(int usuarioId)
    {
        return await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.UsuarioMovimientoId == usuarioId && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .OrderByDescending(m => m.FechaMovimiento)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<IEnumerable<MovimientoAvesDto>> GetMovimientosRecientesAsync(int dias = 7)
    {
        var fechaDesde = DateTime.UtcNow.AddDays(-dias);
        return await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.FechaMovimiento >= fechaDesde && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .OrderByDescending(m => m.FechaMovimiento)
            .Take(50)
            .Select(ToDto)
            .ToListAsync();
    }
}
