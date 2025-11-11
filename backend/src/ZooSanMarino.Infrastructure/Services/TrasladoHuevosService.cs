// src/ZooSanMarino.Infrastructure/Services/TrasladoHuevosService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class TrasladoHuevosService : ITrasladoHuevosService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDisponibilidadLoteService _disponibilidadService;

    public TrasladoHuevosService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        IDisponibilidadLoteService disponibilidadService)
    {
        _context = context;
        _currentUser = currentUser;
        _disponibilidadService = disponibilidadService;
    }

    public async Task<TrasladoHuevosDto> CrearTrasladoHuevosAsync(CrearTrasladoHuevosDto dto, int usuarioId)
    {
        // Validar disponibilidad de huevos
        var cantidadesPorTipo = new Dictionary<string, int>
        {
            { "Limpio", dto.CantidadLimpio },
            { "Tratado", dto.CantidadTratado },
            { "Sucio", dto.CantidadSucio },
            { "Deforme", dto.CantidadDeforme },
            { "Blanco", dto.CantidadBlanco },
            { "DobleYema", dto.CantidadDobleYema },
            { "Piso", dto.CantidadPiso },
            { "Pequeno", dto.CantidadPequeno },
            { "Roto", dto.CantidadRoto },
            { "Desecho", dto.CantidadDesecho },
            { "Otro", dto.CantidadOtro }
        };

        var hayDisponibilidad = await _disponibilidadService.ValidarDisponibilidadHuevosAsync(dto.LoteId, cantidadesPorTipo);
        if (!hayDisponibilidad)
        {
            throw new InvalidOperationException("No hay suficientes huevos disponibles para este traslado");
        }

        // Obtener información del lote
        var loteIdInt = int.TryParse(dto.LoteId, out var id) ? id : 0;
        var lote = await _context.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .FirstOrDefaultAsync(l => 
                l.LoteId == loteIdInt && 
                l.CompanyId == _currentUser.CompanyId && 
                l.DeletedAt == null);

        if (lote == null)
        {
            throw new InvalidOperationException($"Lote {dto.LoteId} no encontrado");
        }

        // Crear el traslado
        var traslado = new TrasladoHuevos
        {
            FechaTraslado = dto.FechaTraslado,
            TipoOperacion = dto.TipoOperacion,
            LoteId = dto.LoteId,
            GranjaOrigenId = lote.GranjaId,
            GranjaDestinoId = dto.GranjaDestinoId,
            LoteDestinoId = dto.LoteDestinoId,
            TipoDestino = dto.TipoDestino,
            Motivo = dto.Motivo,
            Descripcion = dto.Descripcion,
            CantidadLimpio = dto.CantidadLimpio,
            CantidadTratado = dto.CantidadTratado,
            CantidadSucio = dto.CantidadSucio,
            CantidadDeforme = dto.CantidadDeforme,
            CantidadBlanco = dto.CantidadBlanco,
            CantidadDobleYema = dto.CantidadDobleYema,
            CantidadPiso = dto.CantidadPiso,
            CantidadPequeno = dto.CantidadPequeno,
            CantidadRoto = dto.CantidadRoto,
            CantidadDesecho = dto.CantidadDesecho,
            CantidadOtro = dto.CantidadOtro,
            Estado = "Pendiente",
            UsuarioTrasladoId = usuarioId,
            Observaciones = dto.Observaciones,
            CompanyId = _currentUser.CompanyId,
            CreatedByUserId = usuarioId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrasladoHuevos.Add(traslado);
        await _context.SaveChangesAsync();

        // Generar número de traslado
        traslado.NumeroTraslado = traslado.GenerarNumeroTraslado();
        await _context.SaveChangesAsync();

        // Procesar automáticamente el traslado (aplicar reducciones)
        await ProcesarTrasladoAsync(traslado.Id);

        return await ToDtoAsync(traslado);
    }

    public async Task<bool> ProcesarTrasladoAsync(int trasladoId)
    {
        var traslado = await _context.TrasladoHuevos
            .FirstOrDefaultAsync(t => 
                t.Id == trasladoId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null);

        if (traslado == null || traslado.Estado != "Pendiente")
        {
            return false;
        }

        try
        {
            // Marcar como completado
            traslado.Procesar();
            await _context.SaveChangesAsync();

            // Las reducciones se calculan automáticamente en DisponibilidadLoteService
            // al restar los traslados completados de los totales acumulados
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CancelarTrasladoAsync(int trasladoId, string motivo)
    {
        var traslado = await _context.TrasladoHuevos
            .FirstOrDefaultAsync(t => 
                t.Id == trasladoId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null);

        if (traslado == null || traslado.Estado == "Completado")
        {
            return false;
        }

        try
        {
            traslado.Cancelar(motivo);
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<TrasladoHuevosDto>> ObtenerTrasladosPorLoteAsync(string loteId)
    {
        return await _context.TrasladoHuevos
            .AsNoTracking()
            .Where(t => 
                t.LoteId == loteId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null)
            .OrderByDescending(t => t.FechaTraslado)
            .Select(t => ToDtoSync(t))
            .ToListAsync();
    }

    private async Task<TrasladoHuevosDto> ToDtoAsync(TrasladoHuevos traslado)
    {
        // Obtener información del lote
        var loteIdInt = int.TryParse(traslado.LoteId, out var id) ? id : 0;
        var lote = await _context.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .FirstOrDefaultAsync(l => l.LoteId == loteIdInt);

        var granjaOrigen = await _context.Farms
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == traslado.GranjaOrigenId);

        Farm? granjaDestino = null;
        if (traslado.GranjaDestinoId.HasValue)
        {
            granjaDestino = await _context.Farms
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == traslado.GranjaDestinoId.Value);
        }

        return new TrasladoHuevosDto
        {
            Id = traslado.Id,
            NumeroTraslado = traslado.NumeroTraslado,
            FechaTraslado = traslado.FechaTraslado,
            TipoOperacion = traslado.TipoOperacion,
            LoteId = traslado.LoteId,
            LoteNombre = lote?.LoteNombre ?? string.Empty,
            GranjaOrigenId = traslado.GranjaOrigenId,
            GranjaOrigenNombre = granjaOrigen?.Name ?? string.Empty,
            GranjaDestinoId = traslado.GranjaDestinoId,
            GranjaDestinoNombre = granjaDestino?.Name,
            LoteDestinoId = traslado.LoteDestinoId,
            TipoDestino = traslado.TipoDestino,
            Motivo = traslado.Motivo,
            Descripcion = traslado.Descripcion,
            CantidadLimpio = traslado.CantidadLimpio,
            CantidadTratado = traslado.CantidadTratado,
            CantidadSucio = traslado.CantidadSucio,
            CantidadDeforme = traslado.CantidadDeforme,
            CantidadBlanco = traslado.CantidadBlanco,
            CantidadDobleYema = traslado.CantidadDobleYema,
            CantidadPiso = traslado.CantidadPiso,
            CantidadPequeno = traslado.CantidadPequeno,
            CantidadRoto = traslado.CantidadRoto,
            CantidadDesecho = traslado.CantidadDesecho,
            CantidadOtro = traslado.CantidadOtro,
            TotalHuevos = traslado.TotalHuevos,
            Estado = traslado.Estado,
            UsuarioTrasladoId = traslado.UsuarioTrasladoId,
            UsuarioNombre = traslado.UsuarioNombre,
            FechaProcesamiento = traslado.FechaProcesamiento,
            FechaCancelacion = traslado.FechaCancelacion,
            Observaciones = traslado.Observaciones,
            CreatedAt = traslado.CreatedAt,
            UpdatedAt = traslado.UpdatedAt
        };
    }

    private TrasladoHuevosDto ToDtoSync(TrasladoHuevos traslado)
    {
        // Versión síncrona sin cargar relaciones (para listados)
        return new TrasladoHuevosDto
        {
            Id = traslado.Id,
            NumeroTraslado = traslado.NumeroTraslado,
            FechaTraslado = traslado.FechaTraslado,
            TipoOperacion = traslado.TipoOperacion,
            LoteId = traslado.LoteId,
            LoteNombre = string.Empty,
            GranjaOrigenId = traslado.GranjaOrigenId,
            GranjaOrigenNombre = string.Empty,
            GranjaDestinoId = traslado.GranjaDestinoId,
            GranjaDestinoNombre = string.Empty,
            LoteDestinoId = traslado.LoteDestinoId,
            TipoDestino = traslado.TipoDestino,
            Motivo = traslado.Motivo,
            Descripcion = traslado.Descripcion,
            CantidadLimpio = traslado.CantidadLimpio,
            CantidadTratado = traslado.CantidadTratado,
            CantidadSucio = traslado.CantidadSucio,
            CantidadDeforme = traslado.CantidadDeforme,
            CantidadBlanco = traslado.CantidadBlanco,
            CantidadDobleYema = traslado.CantidadDobleYema,
            CantidadPiso = traslado.CantidadPiso,
            CantidadPequeno = traslado.CantidadPequeno,
            CantidadRoto = traslado.CantidadRoto,
            CantidadDesecho = traslado.CantidadDesecho,
            CantidadOtro = traslado.CantidadOtro,
            TotalHuevos = traslado.TotalHuevos,
            Estado = traslado.Estado,
            UsuarioTrasladoId = traslado.UsuarioTrasladoId,
            UsuarioNombre = traslado.UsuarioNombre,
            FechaProcesamiento = traslado.FechaProcesamiento,
            FechaCancelacion = traslado.FechaCancelacion,
            Observaciones = traslado.Observaciones,
            CreatedAt = traslado.CreatedAt,
            UpdatedAt = traslado.UpdatedAt
        };
    }
}

