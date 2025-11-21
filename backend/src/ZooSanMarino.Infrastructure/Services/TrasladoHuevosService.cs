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

            // Aplicar descuento en registro diario de producción
            await AplicarDescuentoEnProduccionDiariaAsync(traslado);

            // Las reducciones se calculan automáticamente en DisponibilidadLoteService
            // al restar los traslados completados de los totales acumulados
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Aplica descuento en el registro diario de producción restando los huevos trasladados
    /// </summary>
    private async Task AplicarDescuentoEnProduccionDiariaAsync(TrasladoHuevos traslado)
    {
        // Buscar el registro de producción diaria más reciente del lote para la fecha del traslado
        // Si no existe, crear uno nuevo con valores negativos para descontar
        var fechaTraslado = traslado.FechaTraslado.Date;
        var loteIdStr = traslado.LoteId;

        // Buscar registro existente para esa fecha
        var registroExistente = await _context.SeguimientoProduccion
            .Where(s => s.LoteId == loteIdStr && s.Fecha.Date == fechaTraslado)
            .FirstOrDefaultAsync();

        if (registroExistente != null)
        {
            // Restar las cantidades del registro existente
            registroExistente.HuevoLimpio = Math.Max(0, registroExistente.HuevoLimpio - traslado.CantidadLimpio);
            registroExistente.HuevoTratado = Math.Max(0, registroExistente.HuevoTratado - traslado.CantidadTratado);
            registroExistente.HuevoSucio = Math.Max(0, registroExistente.HuevoSucio - traslado.CantidadSucio);
            registroExistente.HuevoDeforme = Math.Max(0, registroExistente.HuevoDeforme - traslado.CantidadDeforme);
            registroExistente.HuevoBlanco = Math.Max(0, registroExistente.HuevoBlanco - traslado.CantidadBlanco);
            registroExistente.HuevoDobleYema = Math.Max(0, registroExistente.HuevoDobleYema - traslado.CantidadDobleYema);
            registroExistente.HuevoPiso = Math.Max(0, registroExistente.HuevoPiso - traslado.CantidadPiso);
            registroExistente.HuevoPequeno = Math.Max(0, registroExistente.HuevoPequeno - traslado.CantidadPequeno);
            registroExistente.HuevoRoto = Math.Max(0, registroExistente.HuevoRoto - traslado.CantidadRoto);
            registroExistente.HuevoDesecho = Math.Max(0, registroExistente.HuevoDesecho - traslado.CantidadDesecho);
            registroExistente.HuevoOtro = Math.Max(0, registroExistente.HuevoOtro - traslado.CantidadOtro);

            // Recalcular totales
            registroExistente.HuevoTot = registroExistente.HuevoLimpio + registroExistente.HuevoTratado +
                                         registroExistente.HuevoSucio + registroExistente.HuevoDeforme +
                                         registroExistente.HuevoBlanco + registroExistente.HuevoDobleYema +
                                         registroExistente.HuevoPiso + registroExistente.HuevoPequeno +
                                         registroExistente.HuevoRoto + registroExistente.HuevoDesecho +
                                         registroExistente.HuevoOtro;
            registroExistente.HuevoInc = registroExistente.HuevoLimpio + registroExistente.HuevoTratado;

            // Actualizar observaciones
            var obsTraslado = $"Descuento por traslado {traslado.NumeroTraslado}";
            registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                ? obsTraslado
                : $"{registroExistente.Observaciones} | {obsTraslado}";

            await _context.SaveChangesAsync();
        }
        else
        {
            // Si no existe registro para esa fecha, crear uno con valores negativos para descontar
            // Esto permite rastrear el descuento aunque no haya registro previo
            var registroDescuento = new SeguimientoProduccion
            {
                LoteId = loteIdStr,
                Fecha = fechaTraslado,
                // Valores negativos para descontar
                HuevoLimpio = -traslado.CantidadLimpio,
                HuevoTratado = -traslado.CantidadTratado,
                HuevoSucio = -traslado.CantidadSucio,
                HuevoDeforme = -traslado.CantidadDeforme,
                HuevoBlanco = -traslado.CantidadBlanco,
                HuevoDobleYema = -traslado.CantidadDobleYema,
                HuevoPiso = -traslado.CantidadPiso,
                HuevoPequeno = -traslado.CantidadPequeno,
                HuevoRoto = -traslado.CantidadRoto,
                HuevoDesecho = -traslado.CantidadDesecho,
                HuevoOtro = -traslado.CantidadOtro,
                // Totales negativos
                HuevoTot = -traslado.TotalHuevos,
                HuevoInc = -(traslado.CantidadLimpio + traslado.CantidadTratado),
                // Otros campos en cero
                MortalidadH = 0,
                MortalidadM = 0,
                SelH = 0,
                ConsKgH = 0,
                ConsKgM = 0,
                TipoAlimento = "N/A",
                PesoHuevo = 0,
                Etapa = 0,
                Observaciones = $"Registro de descuento por traslado {traslado.NumeroTraslado} - {traslado.TipoOperacion}"
            };

            _context.SeguimientoProduccion.Add(registroDescuento);
            await _context.SaveChangesAsync();
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
        // Primero materializar la consulta
        var traslados = await _context.TrasladoHuevos
            .AsNoTracking()
            .Where(t => 
                t.LoteId == loteId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null)
            .OrderByDescending(t => t.FechaTraslado)
            .ToListAsync();

        // Obtener información de granjas y lotes para todos los traslados
        var granjaIds = traslados
            .SelectMany(t => new[] { t.GranjaOrigenId, t.GranjaDestinoId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var loteIds = traslados
            .Where(t => !string.IsNullOrEmpty(t.LoteId))
            .Select(t => int.TryParse(t.LoteId, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var granjas = await _context.Farms
            .AsNoTracking()
            .Where(f => granjaIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name);

        var lotes = await _context.Lotes
            .AsNoTracking()
            .Where(l => loteIds.Contains(l.LoteId ?? 0))
            .Where(l => l.LoteId.HasValue)
            .ToDictionaryAsync(l => l.LoteId!.Value, l => l.LoteNombre ?? string.Empty);

        // Convertir a DTOs después de materializar
        return traslados.Select(t => ToDtoSync(t, granjas, lotes));
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

    private TrasladoHuevosDto ToDtoSync(TrasladoHuevos traslado, Dictionary<int, string>? granjas = null, Dictionary<int, string>? lotes = null)
    {
        // Obtener nombres de granjas y lotes si están disponibles
        var loteIdInt = int.TryParse(traslado.LoteId, out var id) ? id : 0;
        var loteNombre = lotes != null && loteIdInt > 0 && lotes.ContainsKey(loteIdInt) 
            ? lotes[loteIdInt] 
            : string.Empty;
        
        var granjaOrigenNombre = granjas != null && granjas.ContainsKey(traslado.GranjaOrigenId)
            ? granjas[traslado.GranjaOrigenId]
            : string.Empty;
        
        var granjaDestinoNombre = granjas != null && traslado.GranjaDestinoId.HasValue && granjas.ContainsKey(traslado.GranjaDestinoId.Value)
            ? granjas[traslado.GranjaDestinoId.Value]
            : string.Empty;

        // Versión síncrona con información de relaciones precargadas
        return new TrasladoHuevosDto
        {
            Id = traslado.Id,
            NumeroTraslado = traslado.NumeroTraslado,
            FechaTraslado = traslado.FechaTraslado,
            TipoOperacion = traslado.TipoOperacion,
            LoteId = traslado.LoteId,
            LoteNombre = loteNombre,
            GranjaOrigenId = traslado.GranjaOrigenId,
            GranjaOrigenNombre = granjaOrigenNombre,
            GranjaDestinoId = traslado.GranjaDestinoId,
            GranjaDestinoNombre = granjaDestinoNombre,
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

