// MovimientoAves/Funciones/MovimientoAvesService.Postura.cs
// Actualización directa de AvesHActual/AvesMActual en las tablas postura (fuente primaria de
// inventario): determinar la fase del lote, aplicar el movimiento y revertirlo al cancelar.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    /// <summary>
    /// Determina la fase del lote (Levante/Produccion) combinando tres señales:
    /// 1. Lote.Fase (campo directo)
    /// 2. LotePosturaLevante.EstadoCierre == "Cerrado" (levante cerrado → producción obligatoria)
    /// 3. semana >= 26 (cálculo por semanas como respaldo)
    /// </summary>
    private async Task<string> DeterminarFaseLoteAsync(int loteId, Lote lote)
    {
        // Si existe un registro LotePosturaProduccion, es Produccion de forma definitiva
        var tienePosturaProduccion = await _context.LotePosturaProduccion
            .AsNoTracking()
            .AnyAsync(p => p.LoteId == loteId && p.CompanyId == _currentUser.CompanyId && p.DeletedAt == null);

        if (tienePosturaProduccion)
            return "Produccion";

        var etapa = lote.FechaEncaset.HasValue
            ? MovimientoAvesCalculos.SemanaDesdeEncasetOCero(DateTime.UtcNow, lote.FechaEncaset.Value)
            : 0;

        var lotePosturaLev = await _context.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        var fase = lote.Fase ?? "Levante";
        if (fase == "Levante")
        {
            if (lotePosturaLev?.EstadoCierre == "Cerrado" || MovimientoAvesCalculos.EstaEnProduccion(etapa))
                fase = "Produccion";
        }

        return fase;
    }

    /// <summary>
    /// Actualiza AvesHActual/AvesMActual directamente en la tabla postura correspondiente
    /// al procesar un movimiento (venta o traslado).
    /// — Origen (venta o traslado): resta aves según fase del lote origen.
    /// — Destino (solo traslado): suma aves según fase del lote destino.
    /// Si el lote levante está cerrado (EstadoCierre=="Cerrado") → Produccion por obligación.
    /// </summary>
    private async Task ActualizarAvesActualesEnPosturaAsync(MovimientoAves movimiento)
    {
        // --- ORIGEN: restar aves ---
        if (movimiento.LoteOrigenId.HasValue &&
            (movimiento.CantidadHembras > 0 || movimiento.CantidadMachos > 0))
        {
            var loteOrigen = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.LoteId == movimiento.LoteOrigenId.Value &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (loteOrigen != null)
            {
                var faseOrigen = await DeterminarFaseLoteAsync(movimiento.LoteOrigenId.Value, loteOrigen);

                if (faseOrigen == "Produccion")
                {
                    var posturaProd = await _context.LotePosturaProduccion
                        .Where(p => p.LoteId == movimiento.LoteOrigenId.Value &&
                                   p.CompanyId == _currentUser.CompanyId &&
                                   p.DeletedAt == null)
                        .FirstOrDefaultAsync();

                    if (posturaProd != null)
                    {
                        // Usar valor inicial como base si AvesHActual/AvesMActual aún no fue inicializado
                        var avesHBase = posturaProd.AvesHActual
                            ?? posturaProd.AvesHInicial
                            ?? posturaProd.HembrasInicialesProd
                            ?? 0;
                        var avesMBase = posturaProd.AvesMActual
                            ?? posturaProd.AvesMInicial
                            ?? posturaProd.MachosInicialesProd
                            ?? 0;
                        posturaProd.AvesHActual = Math.Max(0, avesHBase - movimiento.CantidadHembras);
                        posturaProd.AvesMActual = Math.Max(0, avesMBase - movimiento.CantidadMachos);
                        posturaProd.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                else // Levante abierto
                {
                    var posturaLev = await _context.LotePosturaLevante
                        .Where(l => l.LoteId == movimiento.LoteOrigenId.Value &&
                                   l.CompanyId == _currentUser.CompanyId &&
                                   l.DeletedAt == null)
                        .FirstOrDefaultAsync();

                    if (posturaLev != null)
                    {
                        var avesHBaseLev = posturaLev.AvesHActual ?? posturaLev.AvesHInicial ?? 0;
                        var avesMBaseLev = posturaLev.AvesMActual ?? posturaLev.AvesMInicial ?? 0;
                        posturaLev.AvesHActual = Math.Max(0, avesHBaseLev - movimiento.CantidadHembras);
                        posturaLev.AvesMActual = Math.Max(0, avesMBaseLev - movimiento.CantidadMachos);
                        posturaLev.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }

        // --- DESTINO: sumar aves (solo traslados a otro lote) ---
        if (movimiento.LoteDestinoId.HasValue && movimiento.TipoMovimiento == "Traslado" &&
            (movimiento.CantidadHembras > 0 || movimiento.CantidadMachos > 0))
        {
            var loteDestino = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.LoteId == movimiento.LoteDestinoId.Value &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (loteDestino != null)
            {
                var faseDestino = await DeterminarFaseLoteAsync(movimiento.LoteDestinoId.Value, loteDestino);

                if (faseDestino == "Produccion")
                {
                    var posturaProd = await _context.LotePosturaProduccion
                        .Where(p => p.LoteId == movimiento.LoteDestinoId.Value &&
                                   p.CompanyId == _currentUser.CompanyId &&
                                   p.DeletedAt == null)
                        .FirstOrDefaultAsync();

                    if (posturaProd != null)
                    {
                        var avesHBase = posturaProd.AvesHActual
                            ?? posturaProd.AvesHInicial
                            ?? posturaProd.HembrasInicialesProd
                            ?? 0;
                        var avesMBase = posturaProd.AvesMActual
                            ?? posturaProd.AvesMInicial
                            ?? posturaProd.MachosInicialesProd
                            ?? 0;
                        posturaProd.AvesHActual = avesHBase + movimiento.CantidadHembras;
                        posturaProd.AvesMActual = avesMBase + movimiento.CantidadMachos;
                        posturaProd.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                else // Levante abierto
                {
                    var posturaLev = await _context.LotePosturaLevante
                        .Where(l => l.LoteId == movimiento.LoteDestinoId.Value &&
                                   l.CompanyId == _currentUser.CompanyId &&
                                   l.DeletedAt == null)
                        .FirstOrDefaultAsync();

                    if (posturaLev != null)
                    {
                        var avesHBaseLevDest = posturaLev.AvesHActual ?? posturaLev.AvesHInicial ?? 0;
                        var avesMBaseLevDest = posturaLev.AvesMActual ?? posturaLev.AvesMInicial ?? 0;
                        posturaLev.AvesHActual = avesHBaseLevDest + movimiento.CantidadHembras;
                        posturaLev.AvesMActual = avesMBaseLevDest + movimiento.CantidadMachos;
                        posturaLev.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Revierte los cambios en AvesHActual/AvesMActual en las tablas postura
    /// al cancelar un movimiento completado.
    /// — Origen: devuelve las aves (suma).
    /// — Destino (solo traslado): quita las aves que habían entrado (resta).
    /// </summary>
    private async Task RevertirAvesActualesEnPosturaAsync(MovimientoAves movimiento)
    {
        // --- ORIGEN: devolver aves (sumar) ---
        if (movimiento.LoteOrigenId.HasValue &&
            (movimiento.CantidadHembras > 0 || movimiento.CantidadMachos > 0))
        {
            var loteOrigen = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.LoteId == movimiento.LoteOrigenId.Value &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (loteOrigen != null)
            {
                var faseOrigen = await DeterminarFaseLoteAsync(movimiento.LoteOrigenId.Value, loteOrigen);

                if (faseOrigen == "Produccion")
                {
                    var posturaProd = await _context.LotePosturaProduccion
                        .Where(p => p.LoteId == movimiento.LoteOrigenId.Value &&
                                   p.CompanyId == _currentUser.CompanyId &&
                                   p.DeletedAt == null)
                        .FirstOrDefaultAsync();

                    if (posturaProd != null)
                    {
                        posturaProd.AvesHActual = (posturaProd.AvesHActual ?? 0) + movimiento.CantidadHembras;
                        posturaProd.AvesMActual = (posturaProd.AvesMActual ?? 0) + movimiento.CantidadMachos;
                        posturaProd.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                else // Levante abierto
                {
                    var posturaLev = await _context.LotePosturaLevante
                        .Where(l => l.LoteId == movimiento.LoteOrigenId.Value &&
                                   l.CompanyId == _currentUser.CompanyId &&
                                   l.DeletedAt == null)
                        .FirstOrDefaultAsync();

                    if (posturaLev != null)
                    {
                        posturaLev.AvesHActual = (posturaLev.AvesHActual ?? 0) + movimiento.CantidadHembras;
                        posturaLev.AvesMActual = (posturaLev.AvesMActual ?? 0) + movimiento.CantidadMachos;
                        posturaLev.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }

        // --- DESTINO: restar aves que habían entrado (solo traslados) ---
        if (movimiento.LoteDestinoId.HasValue && movimiento.TipoMovimiento == "Traslado" &&
            (movimiento.CantidadHembras > 0 || movimiento.CantidadMachos > 0))
        {
            var loteDestino = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.LoteId == movimiento.LoteDestinoId.Value &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (loteDestino != null)
            {
                var faseDestino = await DeterminarFaseLoteAsync(movimiento.LoteDestinoId.Value, loteDestino);

                if (faseDestino == "Produccion")
                {
                    var posturaProd = await _context.LotePosturaProduccion
                        .Where(p => p.LoteId == movimiento.LoteDestinoId.Value &&
                                   p.CompanyId == _currentUser.CompanyId &&
                                   p.DeletedAt == null)
                        .FirstOrDefaultAsync();

                    if (posturaProd != null)
                    {
                        posturaProd.AvesHActual = Math.Max(0, (posturaProd.AvesHActual ?? 0) - movimiento.CantidadHembras);
                        posturaProd.AvesMActual = Math.Max(0, (posturaProd.AvesMActual ?? 0) - movimiento.CantidadMachos);
                        posturaProd.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                else // Levante abierto
                {
                    var posturaLev = await _context.LotePosturaLevante
                        .Where(l => l.LoteId == movimiento.LoteDestinoId.Value &&
                                   l.CompanyId == _currentUser.CompanyId &&
                                   l.DeletedAt == null)
                        .FirstOrDefaultAsync();

                    if (posturaLev != null)
                    {
                        posturaLev.AvesHActual = Math.Max(0, (posturaLev.AvesHActual ?? 0) - movimiento.CantidadHembras);
                        posturaLev.AvesMActual = Math.Max(0, (posturaLev.AvesMActual ?? 0) - movimiento.CantidadMachos);
                        posturaLev.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }
    }
}
