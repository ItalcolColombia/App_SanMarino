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

    /// <summary>
    /// Ajusta la producción diaria cuando se edita un traslado.
    /// Devuelve las cantidades originales y luego aplica las nuevas cantidades.
    /// </summary>
    private async Task AjustarProduccionDiariaPorEdicionAsync(TrasladoHuevos traslado, Dictionary<string, int> cantidadesOriginales)
    {
        var fechaTraslado = traslado.FechaTraslado.Date;
        var loteIdStr = traslado.LoteId;

        // Buscar registro existente para esa fecha
        var registroExistente = await _context.SeguimientoProduccion
            .Where(s => s.LoteId == loteIdStr && s.Fecha.Date == fechaTraslado)
            .FirstOrDefaultAsync();

        if (registroExistente != null)
        {
            // PRIMERO: Devolver las cantidades originales (sumarlas de vuelta)
            registroExistente.HuevoLimpio += cantidadesOriginales["Limpio"];
            registroExistente.HuevoTratado += cantidadesOriginales["Tratado"];
            registroExistente.HuevoSucio += cantidadesOriginales["Sucio"];
            registroExistente.HuevoDeforme += cantidadesOriginales["Deforme"];
            registroExistente.HuevoBlanco += cantidadesOriginales["Blanco"];
            registroExistente.HuevoDobleYema += cantidadesOriginales["DobleYema"];
            registroExistente.HuevoPiso += cantidadesOriginales["Piso"];
            registroExistente.HuevoPequeno += cantidadesOriginales["Pequeno"];
            registroExistente.HuevoRoto += cantidadesOriginales["Roto"];
            registroExistente.HuevoDesecho += cantidadesOriginales["Desecho"];
            registroExistente.HuevoOtro += cantidadesOriginales["Otro"];

            // SEGUNDO: Aplicar las nuevas cantidades (restarlas)
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
            var obsAjuste = $"Ajuste por edición de traslado {traslado.NumeroTraslado}";
            registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                ? obsAjuste
                : $"{registroExistente.Observaciones} | {obsAjuste}";

            await _context.SaveChangesAsync();
        }
        else
        {
            // Si no existe registro, crear uno con las nuevas cantidades descontadas
            // (las originales ya estaban descontadas, así que solo aplicamos las nuevas)
            var registroAjuste = new SeguimientoProduccion
            {
                LoteId = loteIdStr,
                Fecha = fechaTraslado,
                HuevoLimpio = Math.Max(0, -traslado.CantidadLimpio),
                HuevoTratado = Math.Max(0, -traslado.CantidadTratado),
                HuevoSucio = Math.Max(0, -traslado.CantidadSucio),
                HuevoDeforme = Math.Max(0, -traslado.CantidadDeforme),
                HuevoBlanco = Math.Max(0, -traslado.CantidadBlanco),
                HuevoDobleYema = Math.Max(0, -traslado.CantidadDobleYema),
                HuevoPiso = Math.Max(0, -traslado.CantidadPiso),
                HuevoPequeno = Math.Max(0, -traslado.CantidadPequeno),
                HuevoRoto = Math.Max(0, -traslado.CantidadRoto),
                HuevoDesecho = Math.Max(0, -traslado.CantidadDesecho),
                HuevoOtro = Math.Max(0, -traslado.CantidadOtro),
                Observaciones = $"Ajuste por edición de traslado {traslado.NumeroTraslado}",
                MortalidadH = 0,
                MortalidadM = 0,
                SelH = 0,
                ConsKgH = 0,
                ConsKgM = 0,
                TipoAlimento = "N/A",
                PesoHuevo = 0,
                Etapa = 0
            };

            registroAjuste.HuevoTot = registroAjuste.HuevoLimpio + registroAjuste.HuevoTratado +
                                     registroAjuste.HuevoSucio + registroAjuste.HuevoDeforme +
                                     registroAjuste.HuevoBlanco + registroAjuste.HuevoDobleYema +
                                     registroAjuste.HuevoPiso + registroAjuste.HuevoPequeno +
                                     registroAjuste.HuevoRoto + registroAjuste.HuevoDesecho +
                                     registroAjuste.HuevoOtro;
            registroAjuste.HuevoInc = registroAjuste.HuevoLimpio + registroAjuste.HuevoTratado;

            _context.SeguimientoProduccion.Add(registroAjuste);
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

        if (traslado == null)
        {
            return false;
        }

        // Si ya está cancelado, no hacer nada
        if (traslado.Estado == "Cancelado")
        {
            return true;
        }

        try
        {
            // Si el traslado está en estado "Completado", significa que ya se aplicó el descuento
            // Necesitamos devolver los huevos al inventario antes de cancelar
            if (traslado.Estado == "Completado")
            {
                // Devolver los huevos al inventario antes de cancelar
                await DevolverHuevosAlInventarioAsync(traslado);
            }
            // Si está en "Pendiente", también puede haber descuento aplicado (si se procesó automáticamente)
            // pero por seguridad, verificamos si hay descuento aplicado y lo revertimos
            else if (traslado.Estado == "Pendiente")
            {
                // Verificar si hay descuento aplicado y devolver los huevos
                await DevolverHuevosAlInventarioAsync(traslado);
            }

            // Cancelar el traslado (modificar el método Cancelar para permitir cancelar completados)
            // Como el método de dominio no permite cancelar completados, lo hacemos manualmente
            if (traslado.Estado == "Completado")
            {
                // Permitir cancelar traslados completados para devolver huevos
                traslado.Estado = "Cancelado";
                traslado.FechaCancelacion = DateTime.UtcNow;
                var motivoCompleto = $"Cancelado (traslado completado): {motivo}";
                traslado.Observaciones = string.IsNullOrEmpty(traslado.Observaciones)
                    ? motivoCompleto
                    : $"{traslado.Observaciones} | {motivoCompleto}";
            }
            else
            {
                // Usar el método de dominio para cancelar traslados pendientes
                traslado.Cancelar(motivo);
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al cancelar el traslado: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Devuelve los huevos al inventario cuando se cancela un traslado
    /// </summary>
    private async Task DevolverHuevosAlInventarioAsync(TrasladoHuevos traslado)
    {
        var fechaTraslado = traslado.FechaTraslado.Date;
        var loteIdStr = traslado.LoteId;

        // Buscar registro existente para esa fecha
        var registroExistente = await _context.SeguimientoProduccion
            .Where(s => s.LoteId == loteIdStr && s.Fecha.Date == fechaTraslado)
            .FirstOrDefaultAsync();

        if (registroExistente != null)
        {
            // Devolver las cantidades al inventario (sumarlas de vuelta)
            registroExistente.HuevoLimpio += traslado.CantidadLimpio;
            registroExistente.HuevoTratado += traslado.CantidadTratado;
            registroExistente.HuevoSucio += traslado.CantidadSucio;
            registroExistente.HuevoDeforme += traslado.CantidadDeforme;
            registroExistente.HuevoBlanco += traslado.CantidadBlanco;
            registroExistente.HuevoDobleYema += traslado.CantidadDobleYema;
            registroExistente.HuevoPiso += traslado.CantidadPiso;
            registroExistente.HuevoPequeno += traslado.CantidadPequeno;
            registroExistente.HuevoRoto += traslado.CantidadRoto;
            registroExistente.HuevoDesecho += traslado.CantidadDesecho;
            registroExistente.HuevoOtro += traslado.CantidadOtro;

            // Recalcular totales
            registroExistente.HuevoTot = registroExistente.HuevoLimpio + registroExistente.HuevoTratado +
                                         registroExistente.HuevoSucio + registroExistente.HuevoDeforme +
                                         registroExistente.HuevoBlanco + registroExistente.HuevoDobleYema +
                                         registroExistente.HuevoPiso + registroExistente.HuevoPequeno +
                                         registroExistente.HuevoRoto + registroExistente.HuevoDesecho +
                                         registroExistente.HuevoOtro;
            registroExistente.HuevoInc = registroExistente.HuevoLimpio + registroExistente.HuevoTratado;

            // Actualizar observaciones
            var obsCancelacion = $"Huevos devueltos por cancelación de traslado {traslado.NumeroTraslado}";
            registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                ? obsCancelacion
                : $"{registroExistente.Observaciones} | {obsCancelacion}";

            await _context.SaveChangesAsync();
        }
        else
        {
            // Si no existe registro, crear uno con las cantidades devueltas (valores positivos)
            var registroDevolucion = new SeguimientoProduccion
            {
                LoteId = loteIdStr,
                Fecha = fechaTraslado,
                HuevoLimpio = traslado.CantidadLimpio,
                HuevoTratado = traslado.CantidadTratado,
                HuevoSucio = traslado.CantidadSucio,
                HuevoDeforme = traslado.CantidadDeforme,
                HuevoBlanco = traslado.CantidadBlanco,
                HuevoDobleYema = traslado.CantidadDobleYema,
                HuevoPiso = traslado.CantidadPiso,
                HuevoPequeno = traslado.CantidadPequeno,
                HuevoRoto = traslado.CantidadRoto,
                HuevoDesecho = traslado.CantidadDesecho,
                HuevoOtro = traslado.CantidadOtro,
                Observaciones = $"Huevos devueltos por cancelación de traslado {traslado.NumeroTraslado}",
                MortalidadH = 0,
                MortalidadM = 0,
                SelH = 0,
                ConsKgH = 0,
                ConsKgM = 0,
                TipoAlimento = "N/A",
                PesoHuevo = 0,
                Etapa = 0
            };

            registroDevolucion.HuevoTot = registroDevolucion.HuevoLimpio + registroDevolucion.HuevoTratado +
                                         registroDevolucion.HuevoSucio + registroDevolucion.HuevoDeforme +
                                         registroDevolucion.HuevoBlanco + registroDevolucion.HuevoDobleYema +
                                         registroDevolucion.HuevoPiso + registroDevolucion.HuevoPequeno +
                                         registroDevolucion.HuevoRoto + registroDevolucion.HuevoDesecho +
                                         registroDevolucion.HuevoOtro;
            registroDevolucion.HuevoInc = registroDevolucion.HuevoLimpio + registroDevolucion.HuevoTratado;

            _context.SeguimientoProduccion.Add(registroDevolucion);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<TrasladoHuevosDto?> ObtenerTrasladoPorIdAsync(int trasladoId)
    {
        var traslado = await _context.TrasladoHuevos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => 
                t.Id == trasladoId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null);

        if (traslado == null)
        {
            return null;
        }

        return await ToDtoAsync(traslado);
    }

    public async Task<TrasladoHuevosDto> ActualizarTrasladoHuevosAsync(int trasladoId, ActualizarTrasladoHuevosDto dto, int usuarioId)
    {
        var traslado = await _context.TrasladoHuevos
            .FirstOrDefaultAsync(t => 
                t.Id == trasladoId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null);

        if (traslado == null)
        {
            throw new InvalidOperationException($"Traslado {trasladoId} no encontrado");
        }

        if (traslado.Estado != "Pendiente")
        {
            throw new InvalidOperationException($"Solo se pueden actualizar traslados en estado 'Pendiente'. El traslado actual está en estado '{traslado.Estado}'");
        }

        // Si se actualizan las cantidades, validar disponibilidad
        // Necesitamos considerar que las cantidades originales ya están descontadas,
        // así que solo validamos la diferencia
        if (dto.CantidadLimpio.HasValue || dto.CantidadTratado.HasValue || dto.CantidadSucio.HasValue ||
            dto.CantidadDeforme.HasValue || dto.CantidadBlanco.HasValue || dto.CantidadDobleYema.HasValue ||
            dto.CantidadPiso.HasValue || dto.CantidadPequeno.HasValue || dto.CantidadRoto.HasValue ||
            dto.CantidadDesecho.HasValue || dto.CantidadOtro.HasValue)
        {
            // Calcular las nuevas cantidades (usar valores actuales si no se proporcionan)
            var nuevasCantidades = new Dictionary<string, int>();
            nuevasCantidades["Limpio"] = dto.CantidadLimpio ?? traslado.CantidadLimpio;
            nuevasCantidades["Tratado"] = dto.CantidadTratado ?? traslado.CantidadTratado;
            nuevasCantidades["Sucio"] = dto.CantidadSucio ?? traslado.CantidadSucio;
            nuevasCantidades["Deforme"] = dto.CantidadDeforme ?? traslado.CantidadDeforme;
            nuevasCantidades["Blanco"] = dto.CantidadBlanco ?? traslado.CantidadBlanco;
            nuevasCantidades["DobleYema"] = dto.CantidadDobleYema ?? traslado.CantidadDobleYema;
            nuevasCantidades["Piso"] = dto.CantidadPiso ?? traslado.CantidadPiso;
            nuevasCantidades["Pequeno"] = dto.CantidadPequeno ?? traslado.CantidadPequeno;
            nuevasCantidades["Roto"] = dto.CantidadRoto ?? traslado.CantidadRoto;
            nuevasCantidades["Desecho"] = dto.CantidadDesecho ?? traslado.CantidadDesecho;
            nuevasCantidades["Otro"] = dto.CantidadOtro ?? traslado.CantidadOtro;

            // Calcular la diferencia: si las nuevas cantidades son mayores, necesitamos validar disponibilidad
            // Si son menores, no hay problema porque estamos devolviendo huevos
            var cantidadesParaValidar = new Dictionary<string, int>();
            foreach (var tipo in nuevasCantidades.Keys)
            {
                var cantidadOriginal = tipo switch
                {
                    "Limpio" => traslado.CantidadLimpio,
                    "Tratado" => traslado.CantidadTratado,
                    "Sucio" => traslado.CantidadSucio,
                    "Deforme" => traslado.CantidadDeforme,
                    "Blanco" => traslado.CantidadBlanco,
                    "DobleYema" => traslado.CantidadDobleYema,
                    "Piso" => traslado.CantidadPiso,
                    "Pequeno" => traslado.CantidadPequeno,
                    "Roto" => traslado.CantidadRoto,
                    "Desecho" => traslado.CantidadDesecho,
                    "Otro" => traslado.CantidadOtro,
                    _ => 0
                };

                var cantidadNueva = nuevasCantidades[tipo];
                var diferencia = cantidadNueva - cantidadOriginal;

                // Solo validar si la diferencia es positiva (estamos pidiendo más huevos)
                if (diferencia > 0)
                {
                    cantidadesParaValidar[tipo] = diferencia;
                }
            }

            // Si hay cantidades adicionales que validar, verificar disponibilidad
            if (cantidadesParaValidar.Count > 0)
            {
                var hayDisponibilidad = await _disponibilidadService.ValidarDisponibilidadHuevosAsync(
                    traslado.LoteId, 
                    cantidadesParaValidar);

                if (!hayDisponibilidad)
                {
                    throw new InvalidOperationException("No hay suficientes huevos disponibles para esta actualización. Las nuevas cantidades exceden la disponibilidad actual.");
                }
            }
        }

        // Actualizar campos solo si se proporcionan
        if (dto.FechaTraslado.HasValue)
        {
            traslado.FechaTraslado = dto.FechaTraslado.Value;
        }

        if (!string.IsNullOrEmpty(dto.TipoOperacion))
        {
            traslado.TipoOperacion = dto.TipoOperacion;
        }

        // Guardar cantidades originales ANTES de actualizar (para poder revertir el descuento)
        var cantidadesOriginales = new Dictionary<string, int>
        {
            { "Limpio", traslado.CantidadLimpio },
            { "Tratado", traslado.CantidadTratado },
            { "Sucio", traslado.CantidadSucio },
            { "Deforme", traslado.CantidadDeforme },
            { "Blanco", traslado.CantidadBlanco },
            { "DobleYema", traslado.CantidadDobleYema },
            { "Piso", traslado.CantidadPiso },
            { "Pequeno", traslado.CantidadPequeno },
            { "Roto", traslado.CantidadRoto },
            { "Desecho", traslado.CantidadDesecho },
            { "Otro", traslado.CantidadOtro }
        };

        // Verificar si las cantidades cambiaron
        bool cantidadesCambiaron = false;

        // Actualizar cantidades
        if (dto.CantidadLimpio.HasValue && dto.CantidadLimpio.Value != traslado.CantidadLimpio)
        {
            traslado.CantidadLimpio = dto.CantidadLimpio.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadTratado.HasValue && dto.CantidadTratado.Value != traslado.CantidadTratado)
        {
            traslado.CantidadTratado = dto.CantidadTratado.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadSucio.HasValue && dto.CantidadSucio.Value != traslado.CantidadSucio)
        {
            traslado.CantidadSucio = dto.CantidadSucio.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadDeforme.HasValue && dto.CantidadDeforme.Value != traslado.CantidadDeforme)
        {
            traslado.CantidadDeforme = dto.CantidadDeforme.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadBlanco.HasValue && dto.CantidadBlanco.Value != traslado.CantidadBlanco)
        {
            traslado.CantidadBlanco = dto.CantidadBlanco.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadDobleYema.HasValue && dto.CantidadDobleYema.Value != traslado.CantidadDobleYema)
        {
            traslado.CantidadDobleYema = dto.CantidadDobleYema.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadPiso.HasValue && dto.CantidadPiso.Value != traslado.CantidadPiso)
        {
            traslado.CantidadPiso = dto.CantidadPiso.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadPequeno.HasValue && dto.CantidadPequeno.Value != traslado.CantidadPequeno)
        {
            traslado.CantidadPequeno = dto.CantidadPequeno.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadRoto.HasValue && dto.CantidadRoto.Value != traslado.CantidadRoto)
        {
            traslado.CantidadRoto = dto.CantidadRoto.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadDesecho.HasValue && dto.CantidadDesecho.Value != traslado.CantidadDesecho)
        {
            traslado.CantidadDesecho = dto.CantidadDesecho.Value;
            cantidadesCambiaron = true;
        }
        if (dto.CantidadOtro.HasValue && dto.CantidadOtro.Value != traslado.CantidadOtro)
        {
            traslado.CantidadOtro = dto.CantidadOtro.Value;
            cantidadesCambiaron = true;
        }

        // Actualizar destino
        if (dto.GranjaDestinoId.HasValue)
        {
            traslado.GranjaDestinoId = dto.GranjaDestinoId.Value;
        }
        else if (dto.GranjaDestinoId == null && dto.TipoOperacion == "Venta")
        {
            // Si cambia a venta, limpiar destino
            traslado.GranjaDestinoId = null;
            traslado.LoteDestinoId = null;
            traslado.TipoDestino = null;
        }

        if (dto.LoteDestinoId != null)
        {
            traslado.LoteDestinoId = dto.LoteDestinoId;
        }

        if (!string.IsNullOrEmpty(dto.TipoDestino))
        {
            traslado.TipoDestino = dto.TipoDestino;
        }

        // Actualizar motivo y descripción
        if (dto.Motivo != null)
        {
            traslado.Motivo = dto.Motivo;
        }

        if (dto.Descripcion != null)
        {
            traslado.Descripcion = dto.Descripcion;
        }

        if (dto.Observaciones != null)
        {
            traslado.Observaciones = dto.Observaciones;
        }

        // Actualizar auditoría
        traslado.UpdatedByUserId = usuarioId;
        traslado.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await ToDtoAsync(traslado);
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

