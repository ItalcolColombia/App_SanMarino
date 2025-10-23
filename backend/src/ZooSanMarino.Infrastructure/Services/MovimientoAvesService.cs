// src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class MovimientoAvesService : IMovimientoAvesService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IInventarioAvesService _inventarioService;
    private readonly IHistorialInventarioService _historialService;

    public MovimientoAvesService(
        ZooSanMarinoContext context, 
        ICurrentUser currentUser,
        IInventarioAvesService inventarioService,
        IHistorialInventarioService historialService)
    {
        _context = context;
        _currentUser = currentUser;
        _inventarioService = inventarioService;
        _historialService = historialService;
    }

    public async Task<MovimientoAvesDto> CreateAsync(CreateMovimientoAvesDto dto)
    {
        // Validar movimiento
        var esValido = await ValidarMovimientoAsync(dto);
        if (!esValido)
            throw new InvalidOperationException("El movimiento no es válido");

        var movimiento = new MovimientoAves
        {
            FechaMovimiento = dto.FechaMovimiento,
            TipoMovimiento = dto.TipoMovimiento,
            InventarioOrigenId = dto.InventarioOrigenId,
            LoteOrigenId = dto.LoteOrigenId,
            GranjaOrigenId = dto.GranjaOrigenId,
            NucleoOrigenId = dto.NucleoOrigenId,
            GalponOrigenId = dto.GalponOrigenId,
            InventarioDestinoId = dto.InventarioDestinoId,
            LoteDestinoId = dto.LoteDestinoId,
            GranjaDestinoId = dto.GranjaDestinoId,
            NucleoDestinoId = dto.NucleoDestinoId,
            GalponDestinoId = dto.GalponDestinoId,
            CantidadHembras = dto.CantidadHembras,
            CantidadMachos = dto.CantidadMachos,
            CantidadMixtas = dto.CantidadMixtas,
            MotivoMovimiento = dto.MotivoMovimiento,
            Observaciones = dto.Observaciones,
            Estado = "Pendiente",
            UsuarioMovimientoId = dto.UsuarioMovimientoId > 0 ? dto.UsuarioMovimientoId : _currentUser.UserId,
            CompanyId = _currentUser.CompanyId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        // Agregar al contexto y guardar para obtener el ID
        _context.MovimientoAves.Add(movimiento);
        await _context.SaveChangesAsync();

        // Generar número de movimiento con el ID obtenido
        movimiento.NumeroMovimiento = $"MOV-{DateTime.UtcNow:yyyyMMdd}-{movimiento.Id:D6}";
        await _context.SaveChangesAsync();

        return await GetByIdAsync(movimiento.Id) ?? throw new InvalidOperationException("Error al crear movimiento");
    }

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

    public async Task<ResultadoMovimientoDto> ProcesarMovimientoAsync(ProcesarMovimientoDto dto)
    {
        var movimiento = await _context.MovimientoAves
            .Where(m => m.Id == dto.MovimientoId && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (movimiento == null)
            return new ResultadoMovimientoDto(false, "Movimiento no encontrado", null, null, new List<string> { "Movimiento no encontrado" }, null);

        if (movimiento.Estado != "Pendiente")
            return new ResultadoMovimientoDto(false, "El movimiento ya fue procesado o cancelado", null, null, new List<string> { "Estado inválido" }, null);

        try
        {
            // Procesar el movimiento (implementación básica)
            movimiento.Procesar();
            if (!string.IsNullOrEmpty(dto.ObservacionesProcesamiento))
                movimiento.Observaciones = $"{movimiento.Observaciones} | {dto.ObservacionesProcesamiento}";

            await _context.SaveChangesAsync();

            var movimientoDto = await GetByIdAsync(movimiento.Id);
            return new ResultadoMovimientoDto(true, "Movimiento procesado exitosamente", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimientoDto);
        }
        catch (Exception ex)
        {
            return new ResultadoMovimientoDto(false, "Error al procesar movimiento", movimiento.Id, movimiento.NumeroMovimiento, new List<string> { ex.Message }, null);
        }
    }

    public async Task<ResultadoMovimientoDto> CancelarMovimientoAsync(CancelarMovimientoDto dto)
    {
        var movimiento = await _context.MovimientoAves
            .Where(m => m.Id == dto.MovimientoId && m.CompanyId == _currentUser.CompanyId && m.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (movimiento == null)
            return new ResultadoMovimientoDto(false, "Movimiento no encontrado", null, null, new List<string> { "Movimiento no encontrado" }, null);

        try
        {
            movimiento.Cancelar(dto.MotivoCancelacion);
            await _context.SaveChangesAsync();

            var movimientoDto = await GetByIdAsync(movimiento.Id);
            return new ResultadoMovimientoDto(true, "Movimiento cancelado exitosamente", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimientoDto);
        }
        catch (Exception ex)
        {
            return new ResultadoMovimientoDto(false, "Error al cancelar movimiento", movimiento.Id, movimiento.NumeroMovimiento, new List<string> { ex.Message }, null);
        }
    }

    public async Task<ResultadoMovimientoDto> TrasladoRapidoAsync(TrasladoRapidoDto dto)
    {
        try
        {
            // Implementación básica del traslado rápido
            var createDto = new CreateMovimientoAvesDto
            {
                FechaMovimiento = DateTime.UtcNow,
                TipoMovimiento = "Traslado",
                LoteOrigenId = dto.LoteId,
                GranjaOrigenId = dto.GranjaOrigenId,
                NucleoOrigenId = dto.NucleoOrigenId,
                GalponOrigenId = dto.GalponOrigenId,
                GranjaDestinoId = dto.GranjaDestinoId,
                NucleoDestinoId = dto.NucleoDestinoId,
                GalponDestinoId = dto.GalponDestinoId,
                CantidadHembras = dto.CantidadHembras ?? 0,
                CantidadMachos = dto.CantidadMachos ?? 0,
                CantidadMixtas = dto.CantidadMixtas ?? 0,
                MotivoMovimiento = dto.MotivoTraslado,
                Observaciones = dto.Observaciones,
                UsuarioMovimientoId = _currentUser.UserId
            };

            var movimiento = await CreateAsync(createDto);

            if (dto.ProcesarInmediatamente)
            {
                var procesarDto = new ProcesarMovimientoDto
                {
                    MovimientoId = movimiento.Id,
                    AutoCrearInventarioDestino = true
                };
                return await ProcesarMovimientoAsync(procesarDto);
            }

            return new ResultadoMovimientoDto(true, "Traslado creado exitosamente", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimiento);
        }
        catch (Exception ex)
        {
            return new ResultadoMovimientoDto(false, "Error en traslado rápido", null, null, new List<string> { ex.Message }, null);
        }
    }

    // Implementaciones básicas de los métodos restantes
    public Task<ResultadoMovimientoDto> TrasladarEntreGranjasAsync(int loteId, int granjaOrigenId, int granjaDestinoId, int hembras, int machos, int mixtas, string? motivo = null)  // Changed from string to int
    {
        throw new NotImplementedException("Método pendiente de implementación completa");
    }

    public Task<ResultadoMovimientoDto> TrasladarDentroGranjaAsync(int loteId, int granjaId, string? nucleoOrigenId, string? galponOrigenId, string? nucleoDestinoId, string? galponDestinoId, int hembras, int machos, int mixtas, string? motivo = null)  // Changed from string to int
    {
        throw new NotImplementedException("Método pendiente de implementación completa");
    }

    public Task<ResultadoMovimientoDto> DividirLoteAsync(int loteOrigenId, int loteDestinoId, int hembras, int machos, int mixtas, string? motivo = null)  // Changed from string to int
    {
        throw new NotImplementedException("Método pendiente de implementación completa");
    }

    public Task<ResultadoMovimientoDto> UnificarLotesAsync(int loteOrigenId, int loteDestinoId, string? motivo = null)  // Changed from string to int
    {
        throw new NotImplementedException("Método pendiente de implementación completa");
    }

    public async Task<bool> ValidarMovimientoAsync(CreateMovimientoAvesDto dto)
    {
        // Cantidades > 0
        var total = dto.CantidadHembras + dto.CantidadMachos + dto.CantidadMixtas;
        if (total <= 0) return false;

        // Debe existir un origen y un destino (inventario o lote)
        var tieneOrigen = dto.InventarioOrigenId.HasValue || dto.LoteOrigenId.HasValue;
        var tieneDestino = dto.InventarioDestinoId.HasValue || dto.LoteDestinoId.HasValue;
        if (!tieneOrigen || !tieneDestino) return false;

        // Origen y destino no pueden ser el mismo lote
        if (dto.LoteOrigenId.HasValue && dto.LoteDestinoId.HasValue &&
            dto.LoteOrigenId.Value == dto.LoteDestinoId.Value)
            return false;

        // No cantidades negativas
        if (dto.CantidadHembras < 0 || dto.CantidadMachos < 0 || dto.CantidadMixtas < 0)
            return false;

        // Normaliza tipo
        dto.TipoMovimiento = string.IsNullOrWhiteSpace(dto.TipoMovimiento)
            ? "Traslado"
            : char.ToUpper(dto.TipoMovimiento[0]) + dto.TipoMovimiento.Substring(1).ToLower();

        // Verifica existencia de lotes si llegan
        if (dto.LoteOrigenId.HasValue)
            if (!await _context.Lotes.AnyAsync(l => l.LoteId == dto.LoteOrigenId.Value && l.CompanyId == _currentUser.CompanyId)) 
                return false;

        if (dto.LoteDestinoId.HasValue)
            if (!await _context.Lotes.AnyAsync(l => l.LoteId == dto.LoteDestinoId.Value && l.CompanyId == _currentUser.CompanyId)) 
                return false;

        return true;
    }

    public async Task<List<string>> ValidarDisponibilidadAvesAsync(int inventarioOrigenId, int hembras, int machos, int mixtas)
    {
        var errores = new List<string>();
        
        var puedeRealizar = await _inventarioService.PuedeRealizarMovimientoAsync(inventarioOrigenId, hembras, machos, mixtas);
        if (!puedeRealizar)
            errores.Add("No hay suficientes aves disponibles para el movimiento");

        return errores;
    }

    public async Task<bool> ValidarUbicacionDestinoAsync(int granjaId, string? nucleoId, string? galponId)
    {
        // Validación básica - verificar que la granja existe
        return await _context.Farms
            .Where(f => f.Id == granjaId && f.CompanyId == _currentUser.CompanyId)
            .AnyAsync();
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

    private static System.Linq.Expressions.Expression<Func<MovimientoAves, MovimientoAvesDto>> ToDto =>
        m => new MovimientoAvesDto(
            m.Id,
            m.NumeroMovimiento,
            m.FechaMovimiento,
            m.TipoMovimiento,
            // Origen
            new UbicacionMovimientoDto(
                m.LoteOrigenId,
                m.LoteOrigen != null ? m.LoteOrigen.LoteNombre : null,
                m.GranjaOrigenId,
                m.GranjaOrigen != null ? m.GranjaOrigen.Name : null,
                m.NucleoOrigenId,
                null, // NucleoOrigen navigation property removed
                m.GalponOrigenId,
                null  // GalponOrigen navigation property removed
            ),
            // Destino
            new UbicacionMovimientoDto(
                m.LoteDestinoId,
                m.LoteDestino != null ? m.LoteDestino.LoteNombre : null,
                m.GranjaDestinoId,
                m.GranjaDestino != null ? m.GranjaDestino.Name : null,
                m.NucleoDestinoId,
                null, // NucleoDestino navigation property removed
                m.GalponDestinoId,
                null  // GalponDestino navigation property removed
            ),
            // Cantidades
            m.CantidadHembras,
            m.CantidadMachos,
            m.CantidadMixtas,
            m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas,
            // Estado e información
            m.Estado,
            m.MotivoMovimiento,
            m.Observaciones,
            // Usuario
            m.UsuarioMovimientoId,
            m.UsuarioNombre,
            // Fechas
            m.FechaProcesamiento,
            m.FechaCancelacion,
            m.CreatedAt
        );

    // =====================================================
    // MÉTODOS PARA NAVEGACIÓN COMPLETA
    // =====================================================

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

    // =====================================================
    // MÉTODOS PRIVADOS DE APOYO
    // =====================================================

    private static System.Linq.Expressions.Expression<Func<MovimientoAves, MovimientoAvesCompletoDto>> ToMovimientoCompletoDto =>
        m => new MovimientoAvesCompletoDto(
            m.Id,
            m.NumeroMovimiento,
            m.FechaMovimiento,
            m.TipoMovimiento,
            // Origen completo
            new UbicacionCompletaDto(
                m.LoteOrigenId,
                m.GranjaOrigenId,
                m.NucleoOrigenId,
                m.GalponOrigenId,
                m.CompanyId,
                m.LoteOrigen != null ? m.LoteOrigen.LoteNombre : null,
                m.GranjaOrigen != null ? m.GranjaOrigen.Name : null,
                null, // NucleoNombre - se obtendrá por separado
                null, // GalponNombre - se obtendrá por separado
                m.GranjaOrigen != null ? m.GranjaOrigen.CompanyId.ToString() : null,
                null, // Regional
                null, // Departamento
                null, // Municipio
                null, // TipoGalpon
                null, // AnchoGalpon
                null, // LargoGalpon
                m.LoteOrigen != null ? m.LoteOrigen.Raza : null,
                m.LoteOrigen != null ? m.LoteOrigen.Linea : null,
                m.LoteOrigen != null ? m.LoteOrigen.TipoLinea : null,
                m.LoteOrigen != null ? m.LoteOrigen.CodigoGuiaGenetica : null,
                m.LoteOrigen != null ? m.LoteOrigen.AnoTablaGenetica : null,
                m.LoteOrigen != null ? m.LoteOrigen.Tecnico : null,
                m.GranjaOrigen != null ? m.GranjaOrigen.Status : null,
                m.LoteOrigen != null ? m.LoteOrigen.FechaEncaset : null,
                m.LoteOrigen != null ? m.LoteOrigen.EdadInicial : null
            ),
            // Destino completo
            new UbicacionCompletaDto(
                m.LoteDestinoId,
                m.GranjaDestinoId,
                m.NucleoDestinoId,
                m.GalponDestinoId,
                m.CompanyId,
                m.LoteDestino != null ? m.LoteDestino.LoteNombre : null,
                m.GranjaDestino != null ? m.GranjaDestino.Name : null,
                null, // NucleoNombre - se obtendrá por separado
                null, // GalponNombre - se obtendrá por separado
                m.GranjaDestino != null ? m.GranjaDestino.CompanyId.ToString() : null,
                null, // Regional
                null, // Departamento
                null, // Municipio
                null, // TipoGalpon
                null, // AnchoGalpon
                null, // LargoGalpon
                m.LoteDestino != null ? m.LoteDestino.Raza : null,
                m.LoteDestino != null ? m.LoteDestino.Linea : null,
                m.LoteDestino != null ? m.LoteDestino.TipoLinea : null,
                m.LoteDestino != null ? m.LoteDestino.CodigoGuiaGenetica : null,
                m.LoteDestino != null ? m.LoteDestino.AnoTablaGenetica : null,
                m.LoteDestino != null ? m.LoteDestino.Tecnico : null,
                m.GranjaDestino != null ? m.GranjaDestino.Status : null,
                m.LoteDestino != null ? m.LoteDestino.FechaEncaset : null,
                m.LoteDestino != null ? m.LoteDestino.EdadInicial : null
            ),
            // Cantidades
            m.CantidadHembras,
            m.CantidadMachos,
            m.CantidadMixtas,
            m.TotalAves,
            // Estado e información
            m.Estado,
            m.MotivoMovimiento,
            m.Observaciones,
            // Usuario
            m.UsuarioMovimientoId,
            m.UsuarioNombre,
            // Fechas
            m.FechaProcesamiento,
            m.FechaCancelacion,
            m.CreatedAt,
            m.UpdatedAt,
            // Información calculada
            m.GranjaOrigenId == m.GranjaDestinoId,
            m.GranjaOrigenId != m.GranjaDestinoId,
            m.TipoMovimiento == "Traslado" ? "Traslado de Aves" :
            m.TipoMovimiento == "Ajuste" ? "Ajuste de Inventario" :
            m.TipoMovimiento == "Liquidacion" ? "Liquidación de Lote" :
            m.TipoMovimiento
        );

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
            .GroupBy(x => x.GranjaId.Value)
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
