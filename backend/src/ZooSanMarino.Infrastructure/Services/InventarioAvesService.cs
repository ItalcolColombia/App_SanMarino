// src/ZooSanMarino.Infrastructure/Services/InventarioAvesService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class InventarioAvesService : IInventarioAvesService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IHistorialInventarioService _historialService;
    private readonly ICompanyResolver _companyResolver;

    public InventarioAvesService(
        ZooSanMarinoContext context, 
        ICurrentUser currentUser,
        IHistorialInventarioService historialService,
        ICompanyResolver companyResolver)
    {
        _context = context;
        _currentUser = currentUser;
        _historialService = historialService;
        _companyResolver = companyResolver;
    }

    /// <summary>
    /// Obtiene el CompanyId efectivo basado en el header o token JWT
    /// </summary>
    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        // Si hay una empresa activa especificada en el header, usarla
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var companyId = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName);
            if (companyId.HasValue)
            {
                return companyId.Value;
            }
        }

        // Fallback al CompanyId del token JWT
        return _currentUser.CompanyId;
    }

    public async Task<InventarioAvesDto> CreateAsync(CreateInventarioAvesDto dto)
    {
        // Obtener la empresa efectiva del usuario
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();

        // Validar que el lote existe y pertenece a la compañía
        var lote = await _context.Lotes
            .Where(l => l.LoteId == dto.LoteId && l.CompanyId == effectiveCompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no encontrado o no pertenece a la compañía.");

        // Validar que no existe ya un inventario activo en esa ubicación
        var existeInventario = await ExisteInventarioAsync(dto.LoteId, dto.GranjaId, dto.NucleoId, dto.GalponId);
        if (existeInventario)
            throw new InvalidOperationException("Ya existe un inventario activo para este lote en la ubicación especificada.");

        var inventario = new InventarioAves
        {
            LoteId = dto.LoteId,
            GranjaId = dto.GranjaId,
            NucleoId = dto.NucleoId,
            GalponId = dto.GalponId,
            CantidadHembras = dto.CantidadHembras,
            CantidadMachos = dto.CantidadMachos,
            CantidadMixtas = dto.CantidadMixtas,
            FechaActualizacion = DateTime.UtcNow,
            Estado = "Activo",
            Observaciones = dto.Observaciones,
            CompanyId = effectiveCompanyId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.InventarioAves.Add(inventario);
        await _context.SaveChangesAsync();

        // Registrar en historial
        await _historialService.RegistrarCambioAsync(
            inventario.Id, "Entrada", null,
            0, 0, 0,
            dto.CantidadHembras, dto.CantidadMachos, dto.CantidadMixtas,
            "Creación de inventario inicial", dto.Observaciones
        );

        return await GetByIdAsync(inventario.Id) ?? throw new InvalidOperationException("Error al crear inventario");
    }

    public async Task<InventarioAvesDto?> GetByIdAsync(int id)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        return await _context.InventarioAves
            .AsNoTracking()
            .Where(i => i.Id == id && i.CompanyId == effectiveCompanyId && i.DeletedAt == null)
            .Select(ToDto)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<InventarioAvesDto>> GetAllAsync()
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        return await _context.InventarioAves
            .AsNoTracking()
            .Where(i => i.CompanyId == effectiveCompanyId && i.DeletedAt == null)
            .OrderBy(i => i.LoteId)
            .ThenBy(i => i.GranjaId)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<InventarioAvesDto> UpdateAsync(UpdateInventarioAvesDto dto)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var inventario = await _context.InventarioAves
            .Where(i => i.Id == dto.Id && i.CompanyId == effectiveCompanyId && i.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (inventario == null)
            throw new InvalidOperationException($"Inventario con ID {dto.Id} no encontrado.");

        // Guardar valores anteriores para historial
        var hembrasAnterior = inventario.CantidadHembras;
        var machosAnterior = inventario.CantidadMachos;
        var mixtasAnterior = inventario.CantidadMixtas;

        // Actualizar campos
        if (dto.GranjaId.HasValue) inventario.GranjaId = dto.GranjaId.Value;
        if (dto.NucleoId != null) inventario.NucleoId = dto.NucleoId;
        if (dto.GalponId != null) inventario.GalponId = dto.GalponId;
        if (dto.CantidadHembras.HasValue) inventario.CantidadHembras = dto.CantidadHembras.Value;
        if (dto.CantidadMachos.HasValue) inventario.CantidadMachos = dto.CantidadMachos.Value;
        if (dto.CantidadMixtas.HasValue) inventario.CantidadMixtas = dto.CantidadMixtas.Value;
        if (dto.Estado != null) inventario.Estado = dto.Estado;
        if (dto.Observaciones != null) inventario.Observaciones = dto.Observaciones;

        inventario.FechaActualizacion = DateTime.UtcNow;
        inventario.UpdatedByUserId = _currentUser.UserId;
        inventario.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Registrar cambio en historial si cambió la cantidad
        if (hembrasAnterior != inventario.CantidadHembras || 
            machosAnterior != inventario.CantidadMachos || 
            mixtasAnterior != inventario.CantidadMixtas)
        {
            await _historialService.RegistrarCambioAsync(
                inventario.Id, "Ajuste", null,
                hembrasAnterior, machosAnterior, mixtasAnterior,
                inventario.CantidadHembras, inventario.CantidadMachos, inventario.CantidadMixtas,
                "Actualización manual de inventario", dto.Observaciones
            );
        }

        return await GetByIdAsync(inventario.Id) ?? throw new InvalidOperationException("Error al actualizar inventario");
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var inventario = await _context.InventarioAves
            .Where(i => i.Id == id && i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (inventario == null) return false;

        // Soft delete
        inventario.DeletedAt = DateTime.UtcNow;
        inventario.Estado = "Eliminado";
        await _context.SaveChangesAsync();

        // Registrar en historial
        await _historialService.RegistrarCambioAsync(
            inventario.Id, "Salida", null,
            inventario.CantidadHembras, inventario.CantidadMachos, inventario.CantidadMixtas,
            0, 0, 0,
            "Eliminación de inventario"
        );

        return true;
    }

    public async Task<ZooSanMarino.Application.DTOs.Common.PagedResult<InventarioAvesDto>> SearchAsync(InventarioAvesSearchRequest request)
    {
        try
        {
            // Consultar movimiento_aves en lugar de inventario_aves
            var query = _context.MovimientoAves
                .AsNoTracking()
                .Where(m => m.DeletedAt == null);

            // Filtro de compañía
            if (_currentUser.CompanyId > 0)
            {
                query = query.Where(m => m.CompanyId == _currentUser.CompanyId);
            }

            // Aplicar filtros básicos solo si se proporcionan
            if (request.LoteId.HasValue)
                query = query.Where(m => m.LoteOrigenId == request.LoteId.Value || m.LoteDestinoId == request.LoteId.Value);

            if (request.GranjaId.HasValue)
                query = query.Where(m => m.GranjaOrigenId == request.GranjaId.Value || m.GranjaDestinoId == request.GranjaId.Value);

            if (!string.IsNullOrEmpty(request.NucleoId))
                query = query.Where(m => m.NucleoOrigenId == request.NucleoId || m.NucleoDestinoId == request.NucleoId);

            if (!string.IsNullOrEmpty(request.GalponId))
                query = query.Where(m => m.GalponOrigenId == request.GalponId || m.GalponDestinoId == request.GalponId);

            if (!string.IsNullOrEmpty(request.Estado))
                query = query.Where(m => m.Estado == request.Estado);

            if (request.FechaDesde.HasValue)
                query = query.Where(m => m.FechaMovimiento >= request.FechaDesde.Value);

            if (request.FechaHasta.HasValue)
                query = query.Where(m => m.FechaMovimiento <= request.FechaHasta.Value);

            // Filtro de activos simplificado
            if (request.SoloActivos == true)
            {
                query = query.Where(m => m.Estado == "Pendiente" || m.Estado == "Completado");
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(m => m.FechaMovimiento)
                .ThenBy(m => m.LoteOrigenId)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(ToMovimientoDto)
                .ToListAsync();

            return new ZooSanMarino.Application.DTOs.Common.PagedResult<InventarioAvesDto>
            {
                Items = items,
                Total = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        catch (Exception ex)
        {
            // Log del error para debug
            Console.WriteLine($"Error en SearchAsync: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<IEnumerable<InventarioAvesDto>> GetByLoteIdAsync(int loteId)
    {
        return await _context.InventarioAves
            .AsNoTracking()
            .Where(i => i.LoteId == loteId && i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null)
            .OrderBy(i => i.GranjaId)
            .ThenBy(i => i.NucleoId)
            .ThenBy(i => i.GalponId)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventarioAvesDto>> GetByUbicacionAsync(int granjaId, string? nucleoId = null, string? galponId = null)
    {
        var query = _context.InventarioAves
            .AsNoTracking()
            .Where(i => i.GranjaId == granjaId && i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null);

        if (nucleoId != null)
            query = query.Where(i => i.NucleoId == nucleoId);

        if (galponId != null)
            query = query.Where(i => i.GalponId == galponId);

        return await query
            .OrderBy(i => i.LoteId)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<InventarioAvesDto> AjustarInventarioAsync(int inventarioId, int hembras, int machos, int mixtas, string motivo, string? observaciones = null)
    {
        var inventario = await _context.InventarioAves
            .Where(i => i.Id == inventarioId && i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (inventario == null)
            throw new InvalidOperationException($"Inventario con ID {inventarioId} no encontrado.");

        // Guardar valores anteriores
        var hembrasAnterior = inventario.CantidadHembras;
        var machosAnterior = inventario.CantidadMachos;
        var mixtasAnterior = inventario.CantidadMixtas;

        // Aplicar ajuste
        inventario.CantidadHembras = hembras;
        inventario.CantidadMachos = machos;
        inventario.CantidadMixtas = mixtas;
        inventario.FechaActualizacion = DateTime.UtcNow;
        inventario.UpdatedByUserId = _currentUser.UserId;
        inventario.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Registrar en historial
        await _historialService.RegistrarCambioAsync(
            inventario.Id, "Ajuste", null,
            hembrasAnterior, machosAnterior, mixtasAnterior,
            hembras, machos, mixtas,
            motivo, observaciones
        );

        return await GetByIdAsync(inventario.Id) ?? throw new InvalidOperationException("Error al ajustar inventario");
    }

    public async Task<ResultadoMovimientoDto> TrasladarInventarioAsync(int inventarioId, int granjaDestinoId, string? nucleoDestinoId, string? galponDestinoId, string? motivo = null)
    {
        var inventario = await _context.InventarioAves
            .Where(i => i.Id == inventarioId && i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (inventario == null)
            return new ResultadoMovimientoDto(false, "Inventario no encontrado", null, null, new List<string> { "Inventario no encontrado" }, null);

        try
        {
            // Cambiar ubicación
            inventario.CambiarUbicacion(granjaDestinoId, nucleoDestinoId, galponDestinoId);
            inventario.UpdatedByUserId = _currentUser.UserId;
            inventario.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new ResultadoMovimientoDto(true, "Traslado completado exitosamente", null, null, new List<string>(), null);
        }
        catch (Exception ex)
        {
            return new ResultadoMovimientoDto(false, "Error al realizar traslado", null, null, new List<string> { ex.Message }, null);
        }
    }

    public async Task<EstadoLoteDto> GetEstadoLoteAsync(int loteId)
    {
        var inventarios = await GetByLoteIdAsync(loteId);
        var lote = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null)
            throw new InvalidOperationException($"Lote '{loteId}' no encontrado.");

        var ubicaciones = inventarios.Select(i => new UbicacionLoteDto(
            i.Id,
            i.GranjaId,
            i.GranjaNombre,
            i.NucleoId,
            i.NucleoNombre,
            i.GalponId,
            i.GalponNombre,
            i.CantidadHembras,
            i.CantidadMachos,
            i.CantidadMixtas,
            i.TotalAves,
            i.Estado
        )).ToList();

        return new EstadoLoteDto(
            loteId,
            lote.LoteNombre,
            ubicaciones,
            ubicaciones.Sum(u => u.CantidadHembras),
            ubicaciones.Sum(u => u.CantidadMachos),
            ubicaciones.Sum(u => u.CantidadMixtas),
            ubicaciones.Sum(u => u.TotalAves),
            inventarios.Any() ? inventarios.Max(i => i.FechaActualizacion) : DateTime.MinValue
        );
    }

    public async Task<IEnumerable<ResumenInventarioDto>> GetResumenPorUbicacionAsync(int? granjaId = null)
    {
        var query = _context.InventarioAves
            .AsNoTracking()
            .Where(i => i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null && i.Estado == "Activo");

        if (granjaId.HasValue)
            query = query.Where(i => i.GranjaId == granjaId.Value);

        // Materializar primero para poder usar expresiones lambda con cuerpo
        var grupos = await query
            .GroupBy(i => new { i.GranjaId, i.NucleoId, i.GalponId })
            .ToListAsync();

        var resumen = grupos.Select(g => {
            var first = g.First();
            return new ResumenInventarioDto(
                g.Key.GranjaId,
                first.Granja != null ? first.Granja.Name : string.Empty,
                g.Key.NucleoId,
                first.Nucleo != null ? first.Nucleo.NucleoNombre : null,
                g.Key.GalponId,
                first.Galpon != null ? first.Galpon.GalponNombre : null,
                g.Count(),
                g.Sum(i => i.CantidadHembras),
                g.Sum(i => i.CantidadMachos),
                g.Sum(i => i.CantidadMixtas),
                g.Sum(i => i.CantidadHembras + i.CantidadMachos + i.CantidadMixtas),
                g.Max(i => i.FechaActualizacion)
            );
        }).ToList();

        return resumen;
    }

    public async Task<IEnumerable<InventarioAvesDto>> GetInventariosActivosAsync()
    {
        return await _context.InventarioAves
            .AsNoTracking()
            .Where(i => i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null && i.Estado == "Activo")
            .OrderBy(i => i.LoteId)
            .ThenBy(i => i.GranjaId)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<bool> ExisteInventarioAsync(int loteId, int granjaId, string? nucleoId, string? galponId)
    {
        return await _context.InventarioAves
            .Where(i => i.LoteId == loteId && 
                       i.GranjaId == granjaId && 
                       i.NucleoId == nucleoId && 
                       i.GalponId == galponId &&
                       i.CompanyId == _currentUser.CompanyId && 
                       i.DeletedAt == null &&
                       i.Estado == "Activo")
            .AnyAsync();
    }

    public async Task<bool> PuedeRealizarMovimientoAsync(int inventarioId, int hembras, int machos, int mixtas)
    {
        var inventario = await _context.InventarioAves
            .AsNoTracking()
            .Where(i => i.Id == inventarioId && i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null)
            .FirstOrDefaultAsync();

        return inventario?.PuedeRealizarMovimiento(hembras, machos, mixtas) ?? false;
    }

    public async Task<InventarioAvesDto> InicializarDesdeLotelAsync(int loteId)
    {
        var lote = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null)
            throw new InvalidOperationException($"Lote '{loteId}' no encontrado.");

        // Verificar si ya existe inventario para este lote
        var existeInventario = await _context.InventarioAves
            .Where(i => i.LoteId == loteId && i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null)
            .AnyAsync();

        if (existeInventario)
            throw new InvalidOperationException($"Ya existe inventario para el lote '{loteId}'.");

        var createDto = new CreateInventarioAvesDto
        {
            LoteId = loteId,
            GranjaId = lote.GranjaId,
            NucleoId = lote.NucleoId,
            GalponId = lote.GalponId,
            CantidadHembras = lote.HembrasL ?? 0,
            CantidadMachos = lote.MachosL ?? 0,
            CantidadMixtas = lote.Mixtas ?? 0,
            Observaciones = "Inventario inicializado desde datos del lote"
        };

        return await CreateAsync(createDto);
    }

    public async Task<IEnumerable<InventarioAvesDto>> SincronizarInventariosAsync()
    {
        var lotesConInventario = await _context.InventarioAves
            .Where(i => i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null)
            .Select(i => i.LoteId)
            .Distinct()
            .ToListAsync();

        var lotesSinInventario = await _context.Lotes
            .Where(l => l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null && 
                       !lotesConInventario.Contains(l.LoteId ?? 0) &&
                       (l.HembrasL > 0 || l.MachosL > 0 || l.Mixtas > 0))
            .ToListAsync();

        var inventariosCreados = new List<InventarioAvesDto>();

        foreach (var lote in lotesSinInventario)
        {
            try
            {
                var inventario = await InicializarDesdeLotelAsync(lote.LoteId ?? 0);
                inventariosCreados.Add(inventario);
            }
            catch (Exception)
            {
                // Continuar con el siguiente lote si hay error
                continue;
            }
        }

        return inventariosCreados;
    }

    /// <summary>
    /// Método de debug para obtener el total de registros sin filtros
    /// </summary>
    public async Task<int> GetTotalCountAsync()
    {
        try
        {
            var count = await _context.MovimientoAves
                .Where(m => m.DeletedAt == null)
                .CountAsync();
            
            Console.WriteLine($"Debug: Total registros en movimiento_aves = {count}");
            return count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en GetTotalCountAsync: {ex.Message}");
            throw;
        }
    }

        private static System.Linq.Expressions.Expression<Func<InventarioAves, InventarioAvesDto>> ToDto =>
            i => new InventarioAvesDto(
                i.Id,
                i.LoteId,
                string.Empty, // LoteNombre - no navigation to avoid JOIN issues
                i.GranjaId,
                string.Empty, // GranjaName - no navigation to avoid JOIN issues
                i.NucleoId,
                null, // NucleoNombre - no navigation to avoid EF issues
                i.GalponId,
                null, // GalponNombre - no navigation to avoid EF issues
                i.CantidadHembras,
                i.CantidadMachos,
                i.CantidadMixtas,
                i.CantidadHembras + i.CantidadMachos + i.CantidadMixtas,
                i.FechaActualizacion,
                i.Estado,
                i.Observaciones,
                i.CreatedAt,
                i.UpdatedAt
            );

        private static System.Linq.Expressions.Expression<Func<MovimientoAves, InventarioAvesDto>> ToMovimientoDto =>
            m => new InventarioAvesDto(
                m.Id,
                m.LoteOrigenId ?? m.LoteDestinoId ?? 0, // Usar lote origen o destino
                string.Empty, // LoteNombre - no navigation to avoid JOIN issues
                m.GranjaOrigenId ?? m.GranjaDestinoId ?? 0, // Usar granja origen o destino
                string.Empty, // GranjaName - no navigation to avoid JOIN issues
                m.NucleoOrigenId ?? m.NucleoDestinoId, // Usar nucleo origen o destino
                null, // NucleoNombre - no navigation to avoid EF issues
                m.GalponOrigenId ?? m.GalponDestinoId, // Usar galpon origen o destino
                null, // GalponNombre - no navigation to avoid EF issues
                m.CantidadHembras,
                m.CantidadMachos,
                m.CantidadMixtas,
                m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas,
                m.FechaMovimiento, // Usar fecha de movimiento
                m.Estado,
                m.Observaciones,
                m.CreatedAt,
                m.UpdatedAt
            );
}
