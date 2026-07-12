// MovimientoAves/Funciones/MovimientoAvesService.NavegacionCompleta.cs
// Lecturas con navegación completa (origen/destino enriquecidos): búsqueda paginada, detalle por id
// y resúmenes recientes para dashboard.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    /// <summary>
    /// Obtiene movimientos con navegación completa
    /// </summary>
    public async Task<ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoAvesCompletoDto>> SearchCompletoAsync(MovimientoAvesCompletoSearchRequest request)
    {
        try
        {
            var query = _context.MovimientoAves
                .AsNoTracking()
                .Where(m => m.DeletedAt == null);

            // Filtro de compañía
            if (_currentUser.CompanyId > 0)
            {
                query = query.Where(m => m.CompanyId == _currentUser.CompanyId);
            }

            // Aplicar filtros
            if (!string.IsNullOrEmpty(request.TipoMovimiento))
                query = query.Where(m => m.TipoMovimiento == request.TipoMovimiento);

            if (!string.IsNullOrEmpty(request.Estado))
                query = query.Where(m => m.Estado == request.Estado);

            if (request.FechaDesde.HasValue)
                query = query.Where(m => m.FechaMovimiento >= request.FechaDesde.Value);

            if (request.FechaHasta.HasValue)
                query = query.Where(m => m.FechaMovimiento <= request.FechaHasta.Value);

            // Filtros por origen
            if (request.LoteOrigenId.HasValue)
                query = query.Where(m => m.LoteOrigenId == request.LoteOrigenId.Value);

            if (request.GranjaOrigenId.HasValue)
                query = query.Where(m => m.GranjaOrigenId == request.GranjaOrigenId.Value);

            if (!string.IsNullOrEmpty(request.NucleoOrigenId))
                query = query.Where(m => m.NucleoOrigenId == request.NucleoOrigenId);

            if (!string.IsNullOrEmpty(request.GalponOrigenId))
                query = query.Where(m => m.GalponOrigenId == request.GalponOrigenId);

            // Filtros por destino
            if (request.LoteDestinoId.HasValue)
                query = query.Where(m => m.LoteDestinoId == request.LoteDestinoId.Value);

            if (request.GranjaDestinoId.HasValue)
                query = query.Where(m => m.GranjaDestinoId == request.GranjaDestinoId.Value);

            if (!string.IsNullOrEmpty(request.NucleoDestinoId))
                query = query.Where(m => m.NucleoDestinoId == request.NucleoDestinoId);

            if (!string.IsNullOrEmpty(request.GalponDestinoId))
                query = query.Where(m => m.GalponDestinoId == request.GalponDestinoId);

            // Filtro por usuario
            if (request.UsuarioMovimientoId.HasValue)
                query = query.Where(m => m.UsuarioMovimientoId == request.UsuarioMovimientoId.Value);

            var totalCount = await query.CountAsync();

            // Aplicar ordenamiento
            query = request.SortBy.ToLower() switch
            {
                "fecha_movimiento" => request.SortDesc ? query.OrderByDescending(m => m.FechaMovimiento) : query.OrderBy(m => m.FechaMovimiento),
                "numero_movimiento" => request.SortDesc ? query.OrderByDescending(m => m.NumeroMovimiento) : query.OrderBy(m => m.NumeroMovimiento),
                "estado" => request.SortDesc ? query.OrderByDescending(m => m.Estado) : query.OrderBy(m => m.Estado),
                "tipo_movimiento" => request.SortDesc ? query.OrderByDescending(m => m.TipoMovimiento) : query.OrderBy(m => m.TipoMovimiento),
                _ => query.OrderByDescending(m => m.FechaMovimiento)
            };

            var items = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(ToMovimientoCompletoDto)
                .ToListAsync();

            return new ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoAvesCompletoDto>
            {
                Items = items,
                Total = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en SearchCompletoAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Obtiene un movimiento específico con navegación completa
    /// </summary>
    public async Task<MovimientoAvesCompletoDto?> GetCompletoByIdAsync(int id)
    {
        try
        {
            var movimiento = await _context.MovimientoAves
                .AsNoTracking()
                .Where(m => m.Id == id && m.DeletedAt == null)
                .Select(ToMovimientoCompletoDto)
                .FirstOrDefaultAsync();

            return movimiento;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en GetCompletoByIdAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Obtiene resúmenes de traslados para dashboard
    /// </summary>
    public async Task<IEnumerable<ResumenTrasladoDto>> GetResumenesRecientesAsync(int dias = 7, int limite = 10)
    {
        try
        {
            var fechaDesde = DateTime.UtcNow.AddDays(-dias);

            var resumenes = await _context.MovimientoAves
                .AsNoTracking()
                .Where(m => m.DeletedAt == null && m.FechaMovimiento >= fechaDesde)
                .OrderByDescending(m => m.FechaMovimiento)
                .Take(limite)
                .Select(m => new ResumenTrasladoDto(
                    m.Id,
                    m.NumeroMovimiento,
                    m.FechaMovimiento,
                    m.Estado,
                    // Origen resumen
                    m.LoteOrigenId.HasValue ?
                        (m.LoteOrigen != null ? m.LoteOrigen.LoteNombre : $"Lote {m.LoteOrigenId}") +
                        (m.GranjaOrigen != null ? $" - {m.GranjaOrigen.Name}" : "") :
                        "Sin origen",
                    // Destino resumen
                    m.LoteDestinoId.HasValue ?
                        (m.LoteDestino != null ? m.LoteDestino.LoteNombre : $"Lote {m.LoteDestinoId}") +
                        (m.GranjaDestino != null ? $" - {m.GranjaDestino.Name}" : "") :
                        "Sin destino",
                    m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas,
                    m.UsuarioNombre
                ))
                .ToListAsync();

            return resumenes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en GetResumenesRecientesAsync: {ex.Message}");
            throw;
        }
    }
}
