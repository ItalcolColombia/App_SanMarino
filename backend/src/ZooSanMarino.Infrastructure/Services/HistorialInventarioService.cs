// src/ZooSanMarino.Infrastructure/Services/HistorialInventarioService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class HistorialInventarioService : IHistorialInventarioService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public HistorialInventarioService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ZooSanMarino.Application.DTOs.Common.PagedResult<HistorialInventarioDto>> SearchAsync(HistorialInventarioSearchRequest request)
    {
        var query = _context.HistorialInventario
            .AsNoTracking()
            .Where(h => h.CompanyId == _currentUser.CompanyId && h.DeletedAt == null);

        // Aplicar filtros
        if (request.InventarioId.HasValue)
            query = query.Where(h => h.InventarioId == request.InventarioId.Value);

        if (request.LoteId.HasValue)
            query = query.Where(h => h.LoteId == request.LoteId.Value);

        if (!string.IsNullOrEmpty(request.TipoCambio))
            query = query.Where(h => h.TipoCambio == request.TipoCambio);

        if (request.MovimientoId.HasValue)
            query = query.Where(h => h.MovimientoId == request.MovimientoId.Value);

        if (request.GranjaId.HasValue)
            query = query.Where(h => h.GranjaId == request.GranjaId.Value);

        if (!string.IsNullOrEmpty(request.NucleoId))
            query = query.Where(h => h.NucleoId == request.NucleoId);

        if (!string.IsNullOrEmpty(request.GalponId))
            query = query.Where(h => h.GalponId == request.GalponId);

        if (request.FechaDesde.HasValue)
            query = query.Where(h => h.FechaCambio >= request.FechaDesde.Value);

        if (request.FechaHasta.HasValue)
            query = query.Where(h => h.FechaCambio <= request.FechaHasta.Value);

        if (request.UsuarioCambioId.HasValue)
            query = query.Where(h => h.UsuarioCambioId == request.UsuarioCambioId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(h => h.FechaCambio)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ToDto)
            .ToListAsync();

        return new ZooSanMarino.Application.DTOs.Common.PagedResult<HistorialInventarioDto>
        {
            Items = items,
            Total = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<IEnumerable<HistorialInventarioDto>> GetByInventarioIdAsync(int inventarioId)
    {
        return await _context.HistorialInventario
            .AsNoTracking()
            .Where(h => h.InventarioId == inventarioId && h.CompanyId == _currentUser.CompanyId && h.DeletedAt == null)
            .OrderByDescending(h => h.FechaCambio)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<IEnumerable<HistorialInventarioDto>> GetByLoteIdAsync(int loteId)
    {
        return await _context.HistorialInventario
            .AsNoTracking()
            .Where(h => h.LoteId == loteId && h.CompanyId == _currentUser.CompanyId && h.DeletedAt == null)
            .OrderByDescending(h => h.FechaCambio)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<IEnumerable<HistorialInventarioDto>> GetByMovimientoIdAsync(int movimientoId)
    {
        return await _context.HistorialInventario
            .AsNoTracking()
            .Where(h => h.MovimientoId == movimientoId && h.CompanyId == _currentUser.CompanyId && h.DeletedAt == null)
            .OrderByDescending(h => h.FechaCambio)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<TrazabilidadLoteDto> GetTrazabilidadLoteAsync(int loteId)
    {
        var historial = await GetByLoteIdAsync(loteId);
        var eventos = historial.Select(h => new EventoTrazabilidadDto(
            h.FechaCambio,
            h.TipoCambio,
            $"{h.TipoCambio} - {Math.Abs(h.DiferenciaTotal)} aves",
            null, // UbicacionAnterior - se puede implementar más tarde
            h.Ubicacion,
            h.TotalAnterior,
            h.TotalNuevo,
            h.DiferenciaTotal,
            h.UsuarioNombre,
            h.Observaciones
        )).ToList();

        // Obtener estado actual (implementación básica)
        var estadoActual = new EstadoActualTrazabilidadDto(
            new List<UbicacionLoteDto>(),
            0,
            DateTime.UtcNow,
            "Activo"
        );

        return new TrazabilidadLoteDto(
            loteId,
            loteId.ToString(), // Convert int to string for LoteNombre
            historial.Any() ? historial.Min(h => h.FechaCambio) : DateTime.UtcNow,
            null, // FechaFin
            eventos,
            estadoActual
        );
    }

    public async Task<IEnumerable<EventoTrazabilidadDto>> GetEventosLoteAsync(int loteId, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        var query = _context.HistorialInventario
            .AsNoTracking()
            .Where(h => h.LoteId == loteId && h.CompanyId == _currentUser.CompanyId && h.DeletedAt == null);

        if (fechaDesde.HasValue)
            query = query.Where(h => h.FechaCambio >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(h => h.FechaCambio <= fechaHasta.Value);

        var historial = await query
            .OrderByDescending(h => h.FechaCambio)
            .Select(ToDto)
            .ToListAsync();

        return historial.Select(h => new EventoTrazabilidadDto(
            h.FechaCambio,
            h.TipoCambio,
            $"{h.TipoCambio} - {Math.Abs(h.DiferenciaTotal)} aves",
            null,
            h.Ubicacion,
            h.TotalAnterior,
            h.TotalNuevo,
            h.DiferenciaTotal,
            h.UsuarioNombre,
            h.Observaciones
        ));
    }

    public async Task<ResumenCambiosDto> GetResumenCambiosAsync(DateTime fechaDesde, DateTime fechaHasta, int? granjaId = null)
    {
        var query = _context.HistorialInventario
            .AsNoTracking()
            .Where(h => h.CompanyId == _currentUser.CompanyId && 
                       h.DeletedAt == null &&
                       h.FechaCambio >= fechaDesde && 
                       h.FechaCambio <= fechaHasta);

        if (granjaId.HasValue)
            query = query.Where(h => h.GranjaId == granjaId.Value);

        var cambios = await query.ToListAsync();

        var totalCambios = cambios.Count;
        var totalEntradas = cambios.Count(c => c.DiferenciaTotal > 0);
        var totalSalidas = cambios.Count(c => c.DiferenciaTotal < 0);
        var totalAjustes = cambios.Count(c => c.TipoCambio == "Ajuste");

        var avesEntradasTotal = cambios.Where(c => c.DiferenciaTotal > 0).Sum(c => c.DiferenciaTotal);
        var avesSalidasTotal = Math.Abs(cambios.Where(c => c.DiferenciaTotal < 0).Sum(c => c.DiferenciaTotal));
        var diferenciaNeta = avesEntradasTotal - avesSalidasTotal;

        var cambiosPorTipo = cambios
            .GroupBy(c => c.TipoCambio)
            .Select(g => new ResumenCambioPorTipoDto(
                g.Key,
                g.Count(),
                g.Sum(c => Math.Abs(c.DiferenciaTotal)),
                totalCambios > 0 ? (decimal)g.Count() / totalCambios * 100 : 0
            ))
            .ToList();

        return new ResumenCambiosDto(
            fechaDesde,
            fechaHasta,
            totalCambios,
            totalEntradas,
            totalSalidas,
            totalAjustes,
            avesEntradasTotal,
            avesSalidasTotal,
            diferenciaNeta,
            cambiosPorTipo
        );
    }

    public async Task<IEnumerable<HistorialInventarioDto>> GetCambiosRecientesAsync(int dias = 7)
    {
        var fechaDesde = DateTime.UtcNow.AddDays(-dias);
        return await _context.HistorialInventario
            .AsNoTracking()
            .Where(h => h.CompanyId == _currentUser.CompanyId && 
                       h.DeletedAt == null &&
                       h.FechaCambio >= fechaDesde)
            .OrderByDescending(h => h.FechaCambio)
            .Take(50) // Limitar a 50 registros recientes
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<IEnumerable<HistorialInventarioDto>> GetCambiosPorUsuarioAsync(int usuarioId, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        var query = _context.HistorialInventario
            .AsNoTracking()
            .Where(h => h.UsuarioCambioId == usuarioId && h.CompanyId == _currentUser.CompanyId && h.DeletedAt == null);

        if (fechaDesde.HasValue)
            query = query.Where(h => h.FechaCambio >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(h => h.FechaCambio <= fechaHasta.Value);

        return await query
            .OrderByDescending(h => h.FechaCambio)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<IEnumerable<HistorialInventarioDto>> GetAjustesInventarioAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        var query = _context.HistorialInventario
            .AsNoTracking()
            .Where(h => h.TipoCambio == "Ajuste" && h.CompanyId == _currentUser.CompanyId && h.DeletedAt == null);

        if (fechaDesde.HasValue)
            query = query.Where(h => h.FechaCambio >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(h => h.FechaCambio <= fechaHasta.Value);

        return await query
            .OrderByDescending(h => h.FechaCambio)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<IEnumerable<HistorialInventarioDto>> GetMovimientosGrandesAsync(int minimoAves = 1000)
    {
        return await _context.HistorialInventario
            .AsNoTracking()
            .Where(h => Math.Abs(h.DiferenciaTotal) >= minimoAves && 
                       h.CompanyId == _currentUser.CompanyId && 
                       h.DeletedAt == null)
            .OrderByDescending(h => h.FechaCambio)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task RegistrarCambioAsync(int inventarioId, string tipoCambio, int? movimientoId, 
        int hembrasAnterior, int machosAnterior, int mixtasAnterior,
        int hembrasNueva, int machosNueva, int mixtasNueva,
        string? motivo = null, string? observaciones = null)
    {
        // Obtener información del inventario
        var inventario = await _context.InventarioAves
            .AsNoTracking()
            .Where(i => i.Id == inventarioId)
            .FirstOrDefaultAsync();

        if (inventario == null) return;

        var historial = new HistorialInventario
        {
            InventarioId = inventarioId,
            LoteId = inventario.LoteId,
            FechaCambio = DateTime.UtcNow,
            TipoCambio = tipoCambio,
            MovimientoId = movimientoId,
            CantidadHembrasAnterior = hembrasAnterior,
            CantidadMachosAnterior = machosAnterior,
            CantidadMixtasAnterior = mixtasAnterior,
            CantidadHembrasNueva = hembrasNueva,
            CantidadMachosNueva = machosNueva,
            CantidadMixtasNueva = mixtasNueva,
            GranjaId = inventario.GranjaId,
            NucleoId = inventario.NucleoId,
            GalponId = inventario.GalponId,
            UsuarioCambioId = _currentUser.UserId,
            UsuarioNombre = null, // Se puede obtener del usuario actual
            Motivo = motivo,
            Observaciones = observaciones,
            CompanyId = _currentUser.CompanyId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.HistorialInventario.Add(historial);
        await _context.SaveChangesAsync();
    }

    private static System.Linq.Expressions.Expression<Func<HistorialInventario, HistorialInventarioDto>> ToDto =>
        h => new HistorialInventarioDto(
            h.Id,
            h.InventarioId,
            h.LoteId,
            h.Lote.LoteNombre,
            h.FechaCambio,
            h.TipoCambio,
            h.MovimientoId,
            h.Movimiento != null ? h.Movimiento.NumeroMovimiento : null,
            h.CantidadHembrasAnterior,
            h.CantidadMachosAnterior,
            h.CantidadMixtasAnterior,
            h.CantidadHembrasAnterior + h.CantidadMachosAnterior + h.CantidadMixtasAnterior,
            h.CantidadHembrasNueva,
            h.CantidadMachosNueva,
            h.CantidadMixtasNueva,
            h.CantidadHembrasNueva + h.CantidadMachosNueva + h.CantidadMixtasNueva,
            h.DiferenciaHembras,
            h.DiferenciaMachos,
            h.DiferenciaMixtas,
            h.DiferenciaTotal,
            new UbicacionMovimientoDto(
                h.LoteId,
                h.Lote.LoteNombre,
                h.GranjaId,
                h.Granja.Name,
                h.NucleoId,
                h.Nucleo != null ? h.Nucleo.NucleoNombre : null,
                h.GalponId,
                h.Galpon != null ? h.Galpon.GalponNombre : null
            ),
            h.UsuarioCambioId,
            h.UsuarioNombre,
            h.Motivo,
            h.Observaciones
        );
}
