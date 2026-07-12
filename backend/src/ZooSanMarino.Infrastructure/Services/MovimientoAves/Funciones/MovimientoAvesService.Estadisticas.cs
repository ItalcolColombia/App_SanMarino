// MovimientoAves/Funciones/MovimientoAvesService.Estadisticas.cs
// Conteos y estadísticas de movimientos: pendientes/completados y agregados por granja y por tipo.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    public async Task<int> GetTotalMovimientosPendientesAsync()
    {
        return await _context.MovimientoAves
            .Where(m => m.Estado == "Pendiente" && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .CountAsync();
    }

    public async Task<int> GetTotalMovimientosCompletadosAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        var query = _context.MovimientoAves
            .Where(m => m.Estado == "Completado" && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null);

        if (fechaDesde.HasValue)
            query = query.Where(m => m.FechaProcesamiento >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(m => m.FechaProcesamiento <= fechaHasta.Value);

        return await query.CountAsync();
    }

    /// <summary>
    /// Obtiene estadísticas de traslados
    /// </summary>
    public async Task<EstadisticasTrasladoDto> GetEstadisticasCompletasAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        try
        {
            var query = _context.MovimientoAves
                .AsNoTracking()
                .Where(m => m.DeletedAt == null);

            if (fechaDesde.HasValue)
                query = query.Where(m => m.FechaMovimiento >= fechaDesde.Value);

            if (fechaHasta.HasValue)
                query = query.Where(m => m.FechaMovimiento <= fechaHasta.Value);

            var movimientos = await query.ToListAsync();

            var estadisticas = new EstadisticasTrasladoDto(
                movimientos.Count,
                movimientos.Count(m => m.Estado == "Pendiente"),
                movimientos.Count(m => m.Estado == "Completado"),
                movimientos.Count(m => m.Estado == "Cancelado"),
                movimientos.Sum(m => m.TotalAves),
                movimientos.Count(m => m.GranjaOrigenId == m.GranjaDestinoId),
                movimientos.Count(m => m.GranjaOrigenId != m.GranjaDestinoId),
                fechaDesde,
                fechaHasta,
                await GetEstadisticasPorGranjaAsync(movimientos),
                GetEstadisticasPorTipo(movimientos)
            );

            return estadisticas;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en GetEstadisticasCompletasAsync: {ex.Message}");
            throw;
        }
    }

    private Task<List<EstadisticaPorGranjaDto>> GetEstadisticasPorGranjaAsync(List<MovimientoAves> movimientos)
    {
        var estadisticas = movimientos
            .GroupBy(m => new { m.GranjaOrigenId, m.GranjaDestinoId })
            .SelectMany(g => new[]
            {
                new { GranjaId = g.Key.GranjaOrigenId, Tipo = "Salida", Movimiento = g.First() },
                new { GranjaId = g.Key.GranjaDestinoId, Tipo = "Entrada", Movimiento = g.First() }
            })
            .Where(x => x.GranjaId.HasValue)
            .GroupBy(x => x.GranjaId!.Value)
            .Select(g => new EstadisticaPorGranjaDto(
                g.Key,
                $"Granja {g.Key}", // Se podría mejorar obteniendo el nombre real
                g.Count(),
                g.Sum(x => x.Movimiento.TotalAves),
                g.Count(x => x.Tipo == "Entrada"),
                g.Count(x => x.Tipo == "Salida")
            ))
            .ToList();

        return Task.FromResult(estadisticas);
    }

    private List<EstadisticaPorTipoDto> GetEstadisticasPorTipo(List<MovimientoAves> movimientos)
    {
        var total = movimientos.Count;

        return movimientos
            .GroupBy(m => m.TipoMovimiento)
            .Select(g => new EstadisticaPorTipoDto(
                g.Key,
                g.Count(),
                g.Sum(m => m.TotalAves),
                total > 0 ? (double)g.Count() / total * 100 : 0
            ))
            .ToList();
    }
}
