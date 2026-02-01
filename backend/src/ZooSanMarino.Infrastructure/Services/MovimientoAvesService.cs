// src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<MovimientoAvesService> _logger;

    public MovimientoAvesService(
        ZooSanMarinoContext context, 
        ICurrentUser currentUser,
        IInventarioAvesService inventarioService,
        IHistorialInventarioService historialService,
        ILogger<MovimientoAvesService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _inventarioService = inventarioService;
        _historialService = historialService;
        _logger = logger;
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
            PlantaDestino = dto.PlantaDestino,
            CantidadHembras = dto.CantidadHembras,
            CantidadMachos = dto.CantidadMachos,
            CantidadMixtas = dto.CantidadMixtas,
            MotivoMovimiento = dto.MotivoMovimiento,
            Descripcion = dto.Descripcion,
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

        // Procesar automáticamente el movimiento (aplicar cambios en inventario y seguimiento diario)
        try
        {
            await ProcesarMovimientoAsync(new ProcesarMovimientoDto
            {
                MovimientoId = movimiento.Id,
                AutoCrearInventarioDestino = true
            });
        }
        catch (Exception ex)
        {
            // Si falla el procesamiento, marcar el movimiento como pendiente para que se pueda procesar manualmente
            _logger.LogError(ex, "Error al procesar automáticamente el movimiento {MovimientoId}", movimiento.Id);
            // No lanzar excepción, dejar el movimiento en estado Pendiente
        }

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

            // Actualizar inventario del lote al procesar el movimiento
            await ActualizarInventarioPorMovimientoAsync(movimiento, dto.AutoCrearInventarioDestino);

            // Si es un traslado de aves, aplicar descuento según la etapa del lote (levante o producción)
            if (movimiento.LoteOrigenId.HasValue && 
                (movimiento.TipoMovimiento == "Traslado" || movimiento.TipoMovimiento == "Venta"))
            {
                // Aplicar descuento en producción (semana 26+) - RESTAR del origen
                await AplicarDescuentoEnProduccionDiariaAvesAsync(movimiento);
                
                // Aplicar descuento en levante (semana < 26) - RESTAR del origen
                await AplicarDescuentoEnLevanteDiariaAvesAsync(movimiento);
            }

            // Si es un traslado entre lotes, crear registro de entrada en el lote destino
            if (movimiento.LoteDestinoId.HasValue && movimiento.TipoMovimiento == "Traslado")
            {
                // Crear registro de entrada en seguimiento diario del lote destino
                await CrearRegistroEntradaEnLoteDestinoAsync(movimiento);
            }

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

        // Si ya está cancelado, no hacer nada
        if (movimiento.Estado == "Cancelado")
        {
            var movimientoDto = await GetByIdAsync(movimiento.Id);
            return new ResultadoMovimientoDto(true, "Movimiento ya estaba cancelado", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimientoDto);
        }

        try
        {
            // Si el movimiento está en estado "Completado", significa que ya se aplicó el descuento
            // Necesitamos devolver las aves al inventario antes de cancelar
            if (movimiento.Estado == "Completado")
            {
                // Devolver las aves al inventario antes de cancelar
                await DevolverAvesAlInventarioAsync(movimiento);
            }
            // Si está en "Pendiente", también puede haber descuento aplicado (si se procesó automáticamente)
            else if (movimiento.Estado == "Pendiente")
            {
                // Verificar si hay descuento aplicado y devolver las aves
                await DevolverAvesAlInventarioAsync(movimiento);
            }

            // Cancelar el movimiento
            if (movimiento.Estado == "Completado")
            {
                // Permitir cancelar movimientos completados para devolver aves
                movimiento.Estado = "Cancelado";
                movimiento.FechaCancelacion = DateTime.UtcNow;
                var motivoCompleto = $"Cancelado (movimiento completado): {dto.MotivoCancelacion}";
                movimiento.Observaciones = string.IsNullOrEmpty(movimiento.Observaciones)
                    ? motivoCompleto
                    : $"{movimiento.Observaciones} | {motivoCompleto}";
            }
            else
            {
                // Usar el método de dominio para cancelar movimientos pendientes
                movimiento.Cancelar(dto.MotivoCancelacion);
            }

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

        // Debe existir un origen (inventario o lote)
        var tieneOrigen = dto.InventarioOrigenId.HasValue || dto.LoteOrigenId.HasValue;
        if (!tieneOrigen) return false;

        // Para retiros y ventas, no se requiere destino; para otros tipos (traslados), sí
        var esRetiro = dto.TipoMovimiento?.Equals("Retiro", StringComparison.OrdinalIgnoreCase) == true;
        var esVenta = dto.TipoMovimiento?.Equals("Venta", StringComparison.OrdinalIgnoreCase) == true;
        var noRequiereDestino = esRetiro || esVenta;
        
        if (!noRequiereDestino)
        {
            var tieneDestino = dto.InventarioDestinoId.HasValue || dto.LoteDestinoId.HasValue || !string.IsNullOrWhiteSpace(dto.PlantaDestino);
            if (!tieneDestino) return false;

            // Origen y destino no pueden ser el mismo lote (excepto para retiros y ventas)
            if (dto.LoteOrigenId.HasValue && dto.LoteDestinoId.HasValue &&
                dto.LoteOrigenId.Value == dto.LoteDestinoId.Value)
                return false;
        }

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

    /// <summary>
    /// Registra un retiro de aves automáticamente desde seguimiento diario (levante o producción)
    /// </summary>
    public async Task<ResultadoMovimientoDto> RegistrarRetiroDesdeSeguimientoAsync(
        int loteId,
        int hembrasRetiradas,
        int machosRetirados,
        int mixtasRetiradas,
        DateTime fechaMovimiento,
        string fuenteSeguimiento,
        string? observaciones = null)
    {
        try
        {
            // Si no hay aves retiradas, no hacer nada
            if (hembrasRetiradas == 0 && machosRetirados == 0 && mixtasRetiradas == 0)
                return new ResultadoMovimientoDto(true, "No hay aves retiradas para registrar", null, null, new List<string>(), null);

            // Obtener información del lote
            var lote = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (lote == null)
                return new ResultadoMovimientoDto(false, $"Lote '{loteId}' no encontrado", null, null, new List<string> { "Lote no encontrado" }, null);

            // Buscar inventario del lote en su ubicación
            var inventario = await _context.InventarioAves
                .Where(i => i.LoteId == loteId && 
                           i.CompanyId == _currentUser.CompanyId && 
                           i.DeletedAt == null &&
                           i.Estado == "Activo")
                .OrderByDescending(i => i.FechaActualizacion)
                .FirstOrDefaultAsync();

            // Si no existe inventario, intentar crearlo con cantidades iniciales del lote (si existen)
            if (inventario == null)
            {
                // Verificar disponibilidad: las aves retiradas no pueden ser más que las disponibles
                int hembrasDisponibles = lote.HembrasL ?? 0;
                int machosDisponibles = lote.MachosL ?? 0;

                if (hembrasRetiradas > hembrasDisponibles || machosRetirados > machosDisponibles)
                    return new ResultadoMovimientoDto(
                        false,
                        "No hay suficientes aves en el lote para el retiro registrado",
                        null,
                        null,
                        new List<string> { $"Hembras disponibles: {hembrasDisponibles}, solicitadas: {hembrasRetiradas} | Machos disponibles: {machosDisponibles}, solicitados: {machosRetirados}" },
                        null);

                // Crear inventario básico si no existe (solo para registrar el retiro)
                inventario = new InventarioAves
                {
                    LoteId = loteId,
                    GranjaId = lote.GranjaId,
                    NucleoId = lote.NucleoId,
                    GalponId = lote.GalponId,
                    CantidadHembras = hembrasDisponibles,
                    CantidadMachos = machosDisponibles,
                    CantidadMixtas = lote.Mixtas ?? 0,
                    FechaActualizacion = DateTime.UtcNow,
                    Estado = "Activo",
                    CompanyId = _currentUser.CompanyId,
                    CreatedByUserId = _currentUser.UserId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.InventarioAves.Add(inventario);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Validar que hay suficientes aves disponibles en el inventario
                if (hembrasRetiradas > inventario.CantidadHembras || 
                    machosRetirados > inventario.CantidadMachos || 
                    mixtasRetiradas > inventario.CantidadMixtas)
                    return new ResultadoMovimientoDto(
                        false,
                        "No hay suficientes aves en el inventario para el retiro registrado",
                        null,
                        null,
                        new List<string> { 
                            $"Hembras disponibles: {inventario.CantidadHembras}, solicitadas: {hembrasRetiradas} | " +
                            $"Machos disponibles: {inventario.CantidadMachos}, solicitados: {machosRetirados} | " +
                            $"Mixtas disponibles: {inventario.CantidadMixtas}, solicitadas: {mixtasRetiradas}"
                        },
                        null);
            }

            // Crear movimiento de retiro
            var movimientoDto = new CreateMovimientoAvesDto
            {
                FechaMovimiento = fechaMovimiento,
                TipoMovimiento = "Retiro",
                LoteOrigenId = loteId,
                GranjaOrigenId = lote.GranjaId,
                NucleoOrigenId = lote.NucleoId,
                GalponOrigenId = lote.GalponId,
                InventarioOrigenId = inventario.Id,
                // No hay destino en un retiro
                CantidadHembras = hembrasRetiradas,
                CantidadMachos = machosRetirados,
                CantidadMixtas = mixtasRetiradas,
                MotivoMovimiento = $"Retiro automático desde seguimiento diario ({fuenteSeguimiento})",
                Observaciones = observaciones ?? $"Registrado automáticamente desde {fuenteSeguimiento}",
                UsuarioMovimientoId = _currentUser.UserId
            };

            var movimiento = await CreateAsync(movimientoDto);

            // Procesar inmediatamente el movimiento para actualizar el inventario
            var procesarDto = new ProcesarMovimientoDto
            {
                MovimientoId = movimiento.Id,
                ObservacionesProcesamiento = $"Procesado automáticamente desde {fuenteSeguimiento}",
                AutoCrearInventarioDestino = false // No hay destino en retiros
            };

            var resultado = await ProcesarMovimientoAsync(procesarDto);

            if (!resultado.Success)
                return resultado;

            // Actualizar manualmente el inventario restando las aves retiradas
            inventario = await _context.InventarioAves.FindAsync(inventario.Id);
            if (inventario != null)
            {
                inventario.AplicarMovimientoSalida(hembrasRetiradas, machosRetirados, mixtasRetiradas);
                inventario.UpdatedByUserId = _currentUser.UserId;
                inventario.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return resultado;
        }
        catch (Exception ex)
        {
            return new ResultadoMovimientoDto(
                false,
                $"Error al registrar retiro desde {fuenteSeguimiento}: {ex.Message}",
                null,
                null,
                new List<string> { ex.Message },
                null);
        }
    }

    /// <summary>
    /// Aplica descuento en seguimiento diario de levante para traslado de aves (solo si el lote está en levante - semana < 26)
    /// </summary>
    private async Task AplicarDescuentoEnLevanteDiariaAvesAsync(MovimientoAves movimiento)
    {
        if (!movimiento.LoteOrigenId.HasValue || (movimiento.CantidadHembras == 0 && movimiento.CantidadMachos == 0))
            return;

        // Obtener información del lote
        var lote = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == movimiento.LoteOrigenId.Value && 
                       l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null || !lote.FechaEncaset.HasValue)
            return;

        // Calcular semana actual del lote
        var fechaMovimiento = movimiento.FechaMovimiento.Date;
        var diasDesdeEncaset = (fechaMovimiento - lote.FechaEncaset.Value.Date).Days;
        var semanaActual = (diasDesdeEncaset / 7) + 1;

        // Solo aplicar descuento si el lote está en levante (semana < 26)
        if (semanaActual >= 26)
            return;

        var loteId = movimiento.LoteOrigenId.Value;

        // Buscar registro existente para esa fecha
        var registroExistente = await _context.SeguimientoLoteLevante
            .Where(s => s.LoteId == loteId && s.FechaRegistro.Date == fechaMovimiento)
            .FirstOrDefaultAsync();

        if (registroExistente != null)
        {
            // Restar las aves trasladadas del registro existente
            // Las aves trasladadas se registran como selección para descontar
            // Hembras trasladadas se restan como SelH (selección hembras)
            // Machos trasladados se restan como SelM (selección machos)
            // Permitimos valores negativos para representar descuentos por traslado
            registroExistente.SelH = registroExistente.SelH - movimiento.CantidadHembras;
            registroExistente.SelM = registroExistente.SelM - movimiento.CantidadMachos;

            // Actualizar observaciones
            var obsTraslado = $"Descuento por traslado {movimiento.NumeroMovimiento} - {movimiento.TipoMovimiento}";
            if (movimiento.CantidadHembras > 0)
                obsTraslado += $" (H: {movimiento.CantidadHembras}";
            if (movimiento.CantidadMachos > 0)
                obsTraslado += movimiento.CantidadHembras > 0 ? $", M: {movimiento.CantidadMachos})" : $" (M: {movimiento.CantidadMachos})";

            registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                ? obsTraslado
                : $"{registroExistente.Observaciones} | {obsTraslado}";

            await _context.SaveChangesAsync();
        }
        else
        {
            // Si no existe registro para esa fecha, crear uno con valores negativos para descontar
            var registroDescuento = new SeguimientoLoteLevante
            {
                LoteId = loteId,
                FechaRegistro = fechaMovimiento,
                // Valores negativos para descontar aves trasladadas
                SelH = -movimiento.CantidadHembras, // Hembras trasladadas
                SelM = -movimiento.CantidadMachos, // Machos trasladados
                // Otros campos en cero
                MortalidadHembras = 0,
                MortalidadMachos = 0,
                ErrorSexajeHembras = 0,
                ErrorSexajeMachos = 0,
                ConsumoKgHembras = 0,
                ConsumoKgMachos = 0,
                TipoAlimento = "N/A",
                Observaciones = $"Registro de descuento por traslado {movimiento.NumeroMovimiento} - {movimiento.TipoMovimiento} " +
                               $"(H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})",
                Ciclo = "Normal"
            };

            _context.SeguimientoLoteLevante.Add(registroDescuento);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Aplica descuento en seguimiento diario de producción para traslado de aves (solo si el lote está en producción - semana 26+)
    /// </summary>
    private async Task AplicarDescuentoEnProduccionDiariaAvesAsync(MovimientoAves movimiento)
    {
        if (!movimiento.LoteOrigenId.HasValue || (movimiento.CantidadHembras == 0 && movimiento.CantidadMachos == 0))
            return;

        // Obtener información del lote
        var lote = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == movimiento.LoteOrigenId.Value && 
                       l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null || !lote.FechaEncaset.HasValue)
            return;

        // Calcular semana actual del lote
        var fechaMovimiento = movimiento.FechaMovimiento.Date;
        var diasDesdeEncaset = (fechaMovimiento - lote.FechaEncaset.Value.Date).Days;
        var semanaActual = (diasDesdeEncaset / 7) + 1;

        // Solo aplicar descuento si el lote está en producción (semana 26 o más)
        if (semanaActual < 26)
            return;

        var loteIdStr = movimiento.LoteOrigenId.Value.ToString();

        // Buscar registro existente para esa fecha
        var registroExistente = await _context.SeguimientoProduccion
            .Where(s => s.LoteId == loteIdStr && s.Fecha.Date == fechaMovimiento)
            .FirstOrDefaultAsync();

        if (registroExistente != null)
        {
            // Restar las aves trasladadas del registro existente
            // Las aves trasladadas se registran como mortalidad/selección para descontar
            // Hembras trasladadas se restan como SelH (selección hembras)
            // Machos trasladados se restan como MortalidadM
            // Permitimos valores negativos para representar descuentos por traslado
            registroExistente.SelH = registroExistente.SelH - movimiento.CantidadHembras;
            registroExistente.MortalidadM = registroExistente.MortalidadM - movimiento.CantidadMachos;

            // Actualizar observaciones
            var obsTraslado = $"Descuento por traslado {movimiento.NumeroMovimiento} - {movimiento.TipoMovimiento}";
            if (movimiento.CantidadHembras > 0)
                obsTraslado += $" (H: {movimiento.CantidadHembras}";
            if (movimiento.CantidadMachos > 0)
                obsTraslado += movimiento.CantidadHembras > 0 ? $", M: {movimiento.CantidadMachos})" : $" (M: {movimiento.CantidadMachos})";

            registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                ? obsTraslado
                : $"{registroExistente.Observaciones} | {obsTraslado}";

            await _context.SaveChangesAsync();
        }
        else
        {
            // Si no existe registro para esa fecha, crear uno con valores negativos para descontar
            var registroDescuento = new SeguimientoProduccion
            {
                LoteId = loteIdStr,
                Fecha = fechaMovimiento,
                // Valores negativos para descontar aves trasladadas
                SelH = -movimiento.CantidadHembras, // Hembras trasladadas
                MortalidadM = -movimiento.CantidadMachos, // Machos trasladados
                // Otros campos en cero
                MortalidadH = 0,
                ConsKgH = 0,
                ConsKgM = 0,
                HuevoTot = 0,
                HuevoInc = 0,
                HuevoLimpio = 0,
                HuevoTratado = 0,
                HuevoSucio = 0,
                HuevoDeforme = 0,
                HuevoBlanco = 0,
                HuevoDobleYema = 0,
                HuevoPiso = 0,
                HuevoPequeno = 0,
                HuevoRoto = 0,
                HuevoDesecho = 0,
                HuevoOtro = 0,
                TipoAlimento = "N/A",
                PesoHuevo = 0,
                Etapa = semanaActual >= 26 && semanaActual <= 33 ? 1 : (semanaActual >= 34 && semanaActual <= 50 ? 2 : 3),
                Observaciones = $"Registro de descuento por traslado {movimiento.NumeroMovimiento} - {movimiento.TipoMovimiento} " +
                               $"(H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})"
            };

            _context.SeguimientoProduccion.Add(registroDescuento);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Actualiza el inventario del lote al procesar un movimiento (resta del origen, suma al destino)
    /// </summary>
    private async Task ActualizarInventarioPorMovimientoAsync(MovimientoAves movimiento, bool autoCrearInventarioDestino = true)
    {
        // Si es salida (Venta o Traslado desde origen)
        if (movimiento.LoteOrigenId.HasValue && 
            (movimiento.TipoMovimiento == "Traslado" || movimiento.TipoMovimiento == "Venta"))
        {
            if (!movimiento.GranjaOrigenId.HasValue)
            {
                throw new InvalidOperationException("El movimiento debe tener una granja de origen especificada.");
            }
            
            var inventarioOrigen = await ObtenerOCrearInventarioAsync(
                movimiento.LoteOrigenId.Value,
                movimiento.GranjaOrigenId.Value,
                movimiento.NucleoOrigenId,
                movimiento.GalponOrigenId);
            
            if (inventarioOrigen != null)
            {
                // Validar que hay suficientes aves disponibles
                if (!inventarioOrigen.PuedeRealizarMovimiento(
                    movimiento.CantidadHembras,
                    movimiento.CantidadMachos,
                    movimiento.CantidadMixtas))
                {
                    throw new InvalidOperationException(
                        $"No hay suficientes aves en el inventario del lote {movimiento.LoteOrigenId}. " +
                        $"Disponibles: H={inventarioOrigen.CantidadHembras}, M={inventarioOrigen.CantidadMachos}, Mixtas={inventarioOrigen.CantidadMixtas}. " +
                        $"Solicitadas: H={movimiento.CantidadHembras}, M={movimiento.CantidadMachos}, Mixtas={movimiento.CantidadMixtas}");
                }
                
                inventarioOrigen.AplicarMovimientoSalida(
                    movimiento.CantidadHembras,
                    movimiento.CantidadMachos,
                    movimiento.CantidadMixtas);
                
                inventarioOrigen.UpdatedByUserId = _currentUser.UserId;
                inventarioOrigen.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        // Si es entrada (Traslado a destino)
        if (movimiento.LoteDestinoId.HasValue && movimiento.TipoMovimiento == "Traslado")
        {
            var granjaDestinoId = movimiento.GranjaDestinoId ?? movimiento.GranjaOrigenId;
            if (!granjaDestinoId.HasValue)
            {
                throw new InvalidOperationException("El movimiento debe tener una granja de destino o origen especificada.");
            }
            
            var inventarioDestino = await ObtenerOCrearInventarioAsync(
                movimiento.LoteDestinoId.Value,
                granjaDestinoId.Value,
                movimiento.NucleoDestinoId,
                movimiento.GalponDestinoId,
                crearSiNoExiste: autoCrearInventarioDestino);
            
            if (inventarioDestino != null)
            {
                inventarioDestino.AplicarMovimientoEntrada(
                    movimiento.CantidadHembras,
                    movimiento.CantidadMachos,
                    movimiento.CantidadMixtas);
                
                inventarioDestino.UpdatedByUserId = _currentUser.UserId;
                inventarioDestino.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Obtiene o crea un inventario para un lote en una ubicación específica
    /// </summary>
    private async Task<InventarioAves?> ObtenerOCrearInventarioAsync(
        int loteId,
        int granjaId,
        string? nucleoId = null,
        string? galponId = null,
        bool crearSiNoExiste = true)
    {
        // Buscar inventario activo existente
        var inventario = await _context.InventarioAves
            .Where(i => i.LoteId == loteId && 
                       i.GranjaId == granjaId &&
                       (nucleoId == null || i.NucleoId == nucleoId) &&
                       (galponId == null || i.GalponId == galponId) &&
                       i.CompanyId == _currentUser.CompanyId && 
                       i.DeletedAt == null &&
                       i.Estado == "Activo")
            .OrderByDescending(i => i.FechaActualizacion)
            .FirstOrDefaultAsync();

        // Si no existe y se permite crear, crear uno nuevo
        if (inventario == null && crearSiNoExiste)
        {
            // Obtener información del lote para inicializar el inventario
            var lote = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.LoteId == loteId && 
                           l.CompanyId == _currentUser.CompanyId && 
                           l.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (lote == null)
                return null;

            // Crear inventario con cantidades iniciales del lote
            inventario = new InventarioAves
            {
                LoteId = loteId,
                GranjaId = granjaId,
                NucleoId = nucleoId ?? lote.NucleoId,
                GalponId = galponId ?? lote.GalponId,
                CantidadHembras = lote.HembrasL ?? 0,
                CantidadMachos = lote.MachosL ?? 0,
                CantidadMixtas = lote.Mixtas ?? 0,
                FechaActualizacion = DateTime.UtcNow,
                Estado = "Activo",
                CompanyId = _currentUser.CompanyId,
                CreatedByUserId = _currentUser.UserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.InventarioAves.Add(inventario);
            await _context.SaveChangesAsync();
        }

        return inventario;
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
            m.Descripcion,
            m.PlantaDestino,
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

    /// <summary>
    /// Actualiza un movimiento de aves existente
    /// </summary>
    public async Task<MovimientoAvesDto> ActualizarMovimientoAvesAsync(int movimientoId, ActualizarMovimientoAvesDto dto, int usuarioId)
    {
        var movimiento = await _context.MovimientoAves
            .FirstOrDefaultAsync(m => 
                m.Id == movimientoId && 
                m.CompanyId == _currentUser.CompanyId && 
                m.DeletedAt == null);

        if (movimiento == null)
        {
            throw new InvalidOperationException($"Movimiento {movimientoId} no encontrado");
        }

        if (movimiento.Estado != "Pendiente")
        {
            throw new InvalidOperationException($"Solo se pueden actualizar movimientos en estado 'Pendiente'. El movimiento actual está en estado '{movimiento.Estado}'");
        }

        // Guardar las cantidades originales antes de la actualización
        var cantidadesOriginales = new Dictionary<string, int>
        {
            { "Hembras", movimiento.CantidadHembras },
            { "Machos", movimiento.CantidadMachos },
            { "Mixtas", movimiento.CantidadMixtas }
        };

        // Si se actualizan las cantidades, validar disponibilidad
        if (dto.CantidadHembras.HasValue || dto.CantidadMachos.HasValue || dto.CantidadMixtas.HasValue)
        {
            var nuevasCantidades = new Dictionary<string, int>
            {
                { "Hembras", dto.CantidadHembras ?? movimiento.CantidadHembras },
                { "Machos", dto.CantidadMachos ?? movimiento.CantidadMachos },
                { "Mixtas", dto.CantidadMixtas ?? movimiento.CantidadMixtas }
            };

            var cantidadesParaValidar = new Dictionary<string, int>();
            foreach (var tipo in nuevasCantidades.Keys)
            {
                var diferencia = nuevasCantidades[tipo] - cantidadesOriginales[tipo];
                if (diferencia > 0) // Solo validar si estamos pidiendo más aves
                {
                    cantidadesParaValidar[tipo] = diferencia;
                }
            }

            if (cantidadesParaValidar.Count > 0 && movimiento.LoteOrigenId.HasValue)
            {
                var inventarioOrigen = await ObtenerOCrearInventarioAsync(
                    movimiento.LoteOrigenId.Value,
                    movimiento.GranjaOrigenId ?? 0,
                    movimiento.NucleoOrigenId,
                    movimiento.GalponOrigenId,
                    crearSiNoExiste: false);

                if (inventarioOrigen != null)
                {
                    var hembrasDisponibles = inventarioOrigen.CantidadHembras + cantidadesOriginales["Hembras"];
                    var machosDisponibles = inventarioOrigen.CantidadMachos + cantidadesOriginales["Machos"];
                    var mixtasDisponibles = inventarioOrigen.CantidadMixtas + cantidadesOriginales["Mixtas"];

                    if (nuevasCantidades["Hembras"] > hembrasDisponibles ||
                        nuevasCantidades["Machos"] > machosDisponibles ||
                        nuevasCantidades["Mixtas"] > mixtasDisponibles)
                    {
                        throw new InvalidOperationException("No hay suficientes aves disponibles para esta actualización. Las nuevas cantidades exceden la disponibilidad actual.");
                    }
                }
            }
        }

        // Actualizar campos del movimiento
        movimiento.FechaMovimiento = dto.FechaMovimiento ?? movimiento.FechaMovimiento;
        movimiento.TipoMovimiento = dto.TipoMovimiento ?? movimiento.TipoMovimiento;
        movimiento.LoteOrigenId = dto.LoteOrigenId ?? movimiento.LoteOrigenId;
        movimiento.GranjaOrigenId = dto.GranjaOrigenId ?? movimiento.GranjaOrigenId;
        movimiento.NucleoOrigenId = dto.NucleoOrigenId ?? movimiento.NucleoOrigenId;
        movimiento.GalponOrigenId = dto.GalponOrigenId ?? movimiento.GalponOrigenId;
        movimiento.LoteDestinoId = dto.LoteDestinoId ?? movimiento.LoteDestinoId;
        movimiento.GranjaDestinoId = dto.GranjaDestinoId ?? movimiento.GranjaDestinoId;
        movimiento.NucleoDestinoId = dto.NucleoDestinoId ?? movimiento.NucleoDestinoId;
        movimiento.GalponDestinoId = dto.GalponDestinoId ?? movimiento.GalponDestinoId;
        movimiento.CantidadHembras = dto.CantidadHembras ?? movimiento.CantidadHembras;
        movimiento.CantidadMachos = dto.CantidadMachos ?? movimiento.CantidadMachos;
        movimiento.CantidadMixtas = dto.CantidadMixtas ?? movimiento.CantidadMixtas;
        movimiento.MotivoMovimiento = dto.MotivoMovimiento ?? movimiento.MotivoMovimiento;
        movimiento.PlantaDestino = dto.PlantaDestino ?? movimiento.PlantaDestino;
        movimiento.Descripcion = dto.Descripcion ?? movimiento.Descripcion;
        movimiento.Observaciones = dto.Observaciones ?? movimiento.Observaciones;

        movimiento.UpdatedByUserId = usuarioId;
        movimiento.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Si se actualizaron las cantidades y el movimiento ya estaba procesado, ajustar seguimiento diario
        if (movimiento.Estado == "Completado" && 
            (dto.CantidadHembras.HasValue || dto.CantidadMachos.HasValue || dto.CantidadMixtas.HasValue))
        {
            // Ajustar los registros en seguimiento diario
            await AjustarSeguimientoDiarioPorEdicionAsync(movimiento, cantidadesOriginales);
        }

        return await GetByIdAsync(movimiento.Id) ?? throw new InvalidOperationException("Error al actualizar movimiento");
    }

    /// <summary>
    /// Devuelve las aves al inventario cuando se cancela un movimiento
    /// </summary>
    private async Task DevolverAvesAlInventarioAsync(MovimientoAves movimiento)
    {
        // Si el movimiento ya fue procesado, necesitamos revertir los cambios en inventario y seguimiento diario
        if (movimiento.Estado == "Completado")
        {
            // Revertir cambios en inventario
            if (movimiento.LoteOrigenId.HasValue && movimiento.GranjaOrigenId.HasValue)
            {
                var inventarioOrigen = await ObtenerOCrearInventarioAsync(
                    movimiento.LoteOrigenId.Value,
                    movimiento.GranjaOrigenId.Value,
                    movimiento.NucleoOrigenId,
                    movimiento.GalponOrigenId,
                    crearSiNoExiste: false);

                if (inventarioOrigen != null)
                {
                    // Devolver las aves al inventario origen (sumar)
                    inventarioOrigen.AplicarMovimientoEntrada(
                        movimiento.CantidadHembras,
                        movimiento.CantidadMachos,
                        movimiento.CantidadMixtas);
                    inventarioOrigen.UpdatedByUserId = _currentUser.UserId;
                    inventarioOrigen.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Revertir cambios en inventario destino (restar)
            if (movimiento.LoteDestinoId.HasValue && movimiento.GranjaDestinoId.HasValue)
            {
                var inventarioDestino = await ObtenerOCrearInventarioAsync(
                    movimiento.LoteDestinoId.Value,
                    movimiento.GranjaDestinoId.Value,
                    movimiento.NucleoDestinoId,
                    movimiento.GalponDestinoId,
                    crearSiNoExiste: false);

                if (inventarioDestino != null)
                {
                    // Restar las aves del inventario destino
                    inventarioDestino.AplicarMovimientoSalida(
                        movimiento.CantidadHembras,
                        movimiento.CantidadMachos,
                        movimiento.CantidadMixtas);
                    inventarioDestino.UpdatedByUserId = _currentUser.UserId;
                    inventarioDestino.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            // Revertir cambios en seguimiento diario (Levante y Producción)
            if (movimiento.LoteOrigenId.HasValue)
            {
                await DevolverAvesEnSeguimientoDiarioAsync(movimiento);
            }
        }
    }

    /// <summary>
    /// Devuelve las aves en el seguimiento diario cuando se cancela un movimiento
    /// </summary>
    private async Task DevolverAvesEnSeguimientoDiarioAsync(MovimientoAves movimiento)
    {
        if (!movimiento.LoteOrigenId.HasValue)
            return;

        var lote = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == movimiento.LoteOrigenId.Value && 
                       l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null || !lote.FechaEncaset.HasValue)
            return;

        var fechaMovimiento = movimiento.FechaMovimiento.Date;
        var diasDesdeEncaset = (fechaMovimiento - lote.FechaEncaset.Value.Date).Days;
        var semanaActual = (diasDesdeEncaset / 7) + 1;
        var loteIdStr = movimiento.LoteOrigenId.Value.ToString();

        // Si es Levante (semana < 26)
        if (semanaActual < 26)
        {
            var registroLevante = await _context.SeguimientoLoteLevante
                .Where(s => s.LoteId == movimiento.LoteOrigenId.Value && 
                           s.FechaRegistro.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (registroLevante != null)
            {
                // Devolver las aves (sumar SelH y SelM)
                registroLevante.SelH += movimiento.CantidadHembras;
                registroLevante.SelM += movimiento.CantidadMachos;

                var obsDevolucion = $"Aves devueltas por cancelación de movimiento {movimiento.NumeroMovimiento}";
                registroLevante.Observaciones = string.IsNullOrEmpty(registroLevante.Observaciones)
                    ? obsDevolucion
                    : $"{registroLevante.Observaciones} | {obsDevolucion}";

                await _context.SaveChangesAsync();
            }
        }
        // Si es Producción (semana >= 26)
        else
        {
            var registroProduccion = await _context.SeguimientoProduccion
                .Where(s => s.LoteId == loteIdStr && s.Fecha.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (registroProduccion != null)
            {
                // Devolver las aves (sumar SelH y MortalidadM)
                registroProduccion.SelH += movimiento.CantidadHembras;
                registroProduccion.MortalidadM += movimiento.CantidadMachos;

                var obsDevolucion = $"Aves devueltas por cancelación de movimiento {movimiento.NumeroMovimiento}";
                registroProduccion.Observaciones = string.IsNullOrEmpty(registroProduccion.Observaciones)
                    ? obsDevolucion
                    : $"{registroProduccion.Observaciones} | {obsDevolucion}";

                await _context.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Crea un registro de entrada en el seguimiento diario del lote destino cuando se procesa un movimiento
    /// </summary>
    private async Task CrearRegistroEntradaEnLoteDestinoAsync(MovimientoAves movimiento)
    {
        if (!movimiento.LoteDestinoId.HasValue)
            return;

        var loteDestino = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == movimiento.LoteDestinoId.Value && 
                       l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (loteDestino == null || !loteDestino.FechaEncaset.HasValue)
            return;

        var fechaMovimiento = movimiento.FechaMovimiento.Date;
        var diasDesdeEncaset = (fechaMovimiento - loteDestino.FechaEncaset.Value.Date).Days;
        var semanaActual = (diasDesdeEncaset / 7) + 1;

        // Si es Levante (semana < 26)
        if (semanaActual < 26)
        {
            var registroExistente = await _context.SeguimientoLoteLevante
                .Where(s => s.LoteId == movimiento.LoteDestinoId.Value && 
                           s.FechaRegistro.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (registroExistente != null)
            {
                // Sumar las aves que entran (como entrada positiva)
                registroExistente.SelH = registroExistente.SelH + movimiento.CantidadHembras;
                registroExistente.SelM = registroExistente.SelM + movimiento.CantidadMachos;

                var obsEntrada = $"Entrada por movimiento {movimiento.NumeroMovimiento} (H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})";
                registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                    ? obsEntrada
                    : $"{registroExistente.Observaciones} | {obsEntrada}";

                await _context.SaveChangesAsync();
            }
            else
            {
                // Crear nuevo registro de entrada
                var registroEntrada = new SeguimientoLoteLevante
                {
                    LoteId = movimiento.LoteDestinoId.Value,
                    FechaRegistro = fechaMovimiento,
                    SelH = movimiento.CantidadHembras, // Entrada de hembras
                    SelM = movimiento.CantidadMachos, // Entrada de machos
                    MortalidadHembras = 0,
                    MortalidadMachos = 0,
                    ErrorSexajeHembras = 0,
                    ErrorSexajeMachos = 0,
                    ConsumoKgHembras = 0,
                    ConsumoKgMachos = 0,
                    TipoAlimento = "N/A",
                    Observaciones = $"Entrada por movimiento {movimiento.NumeroMovimiento} desde lote origen (H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})",
                    Ciclo = "Normal"
                };

                _context.SeguimientoLoteLevante.Add(registroEntrada);
                await _context.SaveChangesAsync();
            }
        }
        // Si es Producción (semana >= 26)
        else
        {
            var loteIdStr = movimiento.LoteDestinoId.Value.ToString();
            var registroExistente = await _context.SeguimientoProduccion
                .Where(s => s.LoteId == loteIdStr && s.Fecha.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (registroExistente != null)
            {
                // Sumar las aves que entran (como entrada positiva)
                registroExistente.SelH = registroExistente.SelH + movimiento.CantidadHembras;
                registroExistente.MortalidadM = registroExistente.MortalidadM + movimiento.CantidadMachos;

                var obsEntrada = $"Entrada por movimiento {movimiento.NumeroMovimiento} (H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})";
                registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                    ? obsEntrada
                    : $"{registroExistente.Observaciones} | {obsEntrada}";

                await _context.SaveChangesAsync();
            }
            else
            {
                // Crear nuevo registro de entrada
                var registroEntrada = new SeguimientoProduccion
                {
                    LoteId = loteIdStr,
                    Fecha = fechaMovimiento,
                    SelH = movimiento.CantidadHembras, // Entrada de hembras
                    MortalidadM = movimiento.CantidadMachos, // Entrada de machos
                    MortalidadH = 0,
                    ConsKgH = 0,
                    ConsKgM = 0,
                    HuevoTot = 0,
                    HuevoInc = 0,
                    HuevoLimpio = 0,
                    HuevoTratado = 0,
                    HuevoSucio = 0,
                    HuevoDeforme = 0,
                    HuevoBlanco = 0,
                    HuevoDobleYema = 0,
                    HuevoPiso = 0,
                    HuevoPequeno = 0,
                    HuevoRoto = 0,
                    HuevoDesecho = 0,
                    HuevoOtro = 0,
                    TipoAlimento = "N/A",
                    PesoHuevo = 0,
                    Etapa = semanaActual >= 26 && semanaActual <= 33 ? 1 : (semanaActual >= 34 && semanaActual <= 50 ? 2 : 3),
                    Observaciones = $"Entrada por movimiento {movimiento.NumeroMovimiento} desde lote origen (H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})"
                };

                _context.SeguimientoProduccion.Add(registroEntrada);
                await _context.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Ajusta el seguimiento diario cuando se edita un movimiento completado
    /// Devuelve las cantidades originales y luego aplica las nuevas cantidades
    /// </summary>
    private async Task AjustarSeguimientoDiarioPorEdicionAsync(MovimientoAves movimiento, Dictionary<string, int> cantidadesOriginales)
    {
        if (!movimiento.LoteOrigenId.HasValue)
            return;

        var lote = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == movimiento.LoteOrigenId.Value && 
                       l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null || !lote.FechaEncaset.HasValue)
            return;

        var fechaMovimiento = movimiento.FechaMovimiento.Date;
        var diasDesdeEncaset = (fechaMovimiento - lote.FechaEncaset.Value.Date).Days;
        var semanaActual = (diasDesdeEncaset / 7) + 1;

        // Si es Levante (semana < 26)
        if (semanaActual < 26)
        {
            var registroExistente = await _context.SeguimientoLoteLevante
                .Where(s => s.LoteId == movimiento.LoteOrigenId.Value && 
                           s.FechaRegistro.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (registroExistente != null)
            {
                // PRIMERO: Devolver las cantidades originales (sumarlas de vuelta)
                registroExistente.SelH += cantidadesOriginales["Hembras"];
                registroExistente.SelM += cantidadesOriginales["Machos"];

                // AHORA: Aplicar las nuevas cantidades (restarlas)
                registroExistente.SelH -= movimiento.CantidadHembras;
                registroExistente.SelM -= movimiento.CantidadMachos;

                var obsAjuste = $"Ajuste por edición de movimiento {movimiento.NumeroMovimiento}";
                registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                    ? obsAjuste
                    : $"{registroExistente.Observaciones} | {obsAjuste}";

                await _context.SaveChangesAsync();
            }
        }
        // Si es Producción (semana >= 26)
        else
        {
            var loteIdStr = movimiento.LoteOrigenId.Value.ToString();
            var registroExistente = await _context.SeguimientoProduccion
                .Where(s => s.LoteId == loteIdStr && s.Fecha.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (registroExistente != null)
            {
                // PRIMERO: Devolver las cantidades originales (sumarlas de vuelta)
                registroExistente.SelH += cantidadesOriginales["Hembras"];
                registroExistente.MortalidadM += cantidadesOriginales["Machos"];

                // AHORA: Aplicar las nuevas cantidades (restarlas)
                registroExistente.SelH -= movimiento.CantidadHembras;
                registroExistente.MortalidadM -= movimiento.CantidadMachos;

                var obsAjuste = $"Ajuste por edición de movimiento {movimiento.NumeroMovimiento}";
                registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                    ? obsAjuste
                    : $"{registroExistente.Observaciones} | {obsAjuste}";

                await _context.SaveChangesAsync();
            }
        }

        // Si hay lote destino, también ajustar el registro de entrada
        if (movimiento.LoteDestinoId.HasValue)
        {
            await AjustarRegistroEntradaEnLoteDestinoAsync(movimiento, cantidadesOriginales);
        }
    }

    /// <summary>
    /// Ajusta el registro de entrada en el lote destino cuando se edita un movimiento
    /// </summary>
    private async Task AjustarRegistroEntradaEnLoteDestinoAsync(MovimientoAves movimiento, Dictionary<string, int> cantidadesOriginales)
    {
        if (!movimiento.LoteDestinoId.HasValue)
            return;

        var loteDestino = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == movimiento.LoteDestinoId.Value && 
                       l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (loteDestino == null || !loteDestino.FechaEncaset.HasValue)
            return;

        var fechaMovimiento = movimiento.FechaMovimiento.Date;
        var diasDesdeEncaset = (fechaMovimiento - loteDestino.FechaEncaset.Value.Date).Days;
        var semanaActual = (diasDesdeEncaset / 7) + 1;

        // Si es Levante (semana < 26)
        if (semanaActual < 26)
        {
            var registroExistente = await _context.SeguimientoLoteLevante
                .Where(s => s.LoteId == movimiento.LoteDestinoId.Value && 
                           s.FechaRegistro.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (registroExistente != null)
            {
                // PRIMERO: Revertir las cantidades originales (restarlas)
                registroExistente.SelH -= cantidadesOriginales["Hembras"];
                registroExistente.SelM -= cantidadesOriginales["Machos"];

                // AHORA: Aplicar las nuevas cantidades (sumarlas)
                registroExistente.SelH += movimiento.CantidadHembras;
                registroExistente.SelM += movimiento.CantidadMachos;

                var obsAjuste = $"Ajuste por edición de movimiento {movimiento.NumeroMovimiento}";
                registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                    ? obsAjuste
                    : $"{registroExistente.Observaciones} | {obsAjuste}";

                await _context.SaveChangesAsync();
            }
        }
        // Si es Producción (semana >= 26)
        else
        {
            var loteIdStr = movimiento.LoteDestinoId.Value.ToString();
            var registroExistente = await _context.SeguimientoProduccion
                .Where(s => s.LoteId == loteIdStr && s.Fecha.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (registroExistente != null)
            {
                // PRIMERO: Revertir las cantidades originales (restarlas)
                registroExistente.SelH -= cantidadesOriginales["Hembras"];
                registroExistente.MortalidadM -= cantidadesOriginales["Machos"];

                // AHORA: Aplicar las nuevas cantidades (sumarlas)
                registroExistente.SelH += movimiento.CantidadHembras;
                registroExistente.MortalidadM += movimiento.CantidadMachos;

                var obsAjuste = $"Ajuste por edición de movimiento {movimiento.NumeroMovimiento}";
                registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                    ? obsAjuste
                    : $"{registroExistente.Observaciones} | {obsAjuste}";

                await _context.SaveChangesAsync();
            }
        }
    }
}
