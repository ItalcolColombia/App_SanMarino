// MovimientoAves/Funciones/MovimientoAvesService.EjecucionDirecta.cs
// Ejecución directa de movimientos desde el seguimiento diario: venta, traslado entre lotes y
// traslado por cierre de lote levante → producción.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    public async Task<ResultadoMovimientoDto> EjecutarVentaAsync(EjecutarVentaAvesRequest request)
    {
        try
        {
            if (request.CantidadHembras <= 0 && request.CantidadMachos <= 0)
                return new ResultadoMovimientoDto(false, "Debe indicar al menos una ave para vender", null, null, new List<string> { "Cantidades inválidas" }, null);

            var lote = await _context.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.LoteOrigenId &&
                                          l.CompanyId == _currentUser.CompanyId &&
                                          l.DeletedAt == null);
            if (lote is null)
                return new ResultadoMovimientoDto(false, $"Lote {request.LoteOrigenId} no encontrado", null, null, new List<string> { "Lote no existe" }, null);

            var dto = new CreateMovimientoAvesDto
            {
                FechaMovimiento    = request.Fecha,
                TipoMovimiento     = "Venta",
                LoteOrigenId       = request.LoteOrigenId,
                GranjaOrigenId     = lote.GranjaId,
                NucleoOrigenId     = lote.NucleoId,
                GalponOrigenId     = lote.GalponId,
                CantidadHembras    = request.CantidadHembras,
                CantidadMachos     = request.CantidadMachos,
                CantidadMixtas     = 0,
                MotivoMovimiento   = request.Motivo ?? "Venta desde seguimiento diario",
                Observaciones      = request.Observaciones,
                UsuarioMovimientoId = _currentUser.UserId
            };

            var movimiento = await CreateAsync(dto);
            return new ResultadoMovimientoDto(true, "Venta registrada correctamente", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimiento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar venta para lote {LoteId}", request.LoteOrigenId);
            return new ResultadoMovimientoDto(false, ex.Message, null, null, new List<string> { ex.Message }, null);
        }
    }

    public async Task<ResultadoMovimientoDto> EjecutarTrasladoAsync(EjecutarTrasladoAvesRequest request)
    {
        try
        {
            if (request.CantidadHembras <= 0 && request.CantidadMachos <= 0)
                return new ResultadoMovimientoDto(false, "Debe indicar al menos una ave para trasladar", null, null, new List<string> { "Cantidades inválidas" }, null);

            var loteOrigen = await _context.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.LoteOrigenId &&
                                          l.CompanyId == _currentUser.CompanyId &&
                                          l.DeletedAt == null);
            if (loteOrigen is null)
                return new ResultadoMovimientoDto(false, $"Lote origen {request.LoteOrigenId} no encontrado", null, null, new List<string> { "Lote origen no existe" }, null);

            var loteDestino = await _context.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.LoteDestinoId &&
                                          l.CompanyId == _currentUser.CompanyId &&
                                          l.DeletedAt == null);
            if (loteDestino is null)
                return new ResultadoMovimientoDto(false, $"Lote destino {request.LoteDestinoId} no encontrado", null, null, new List<string> { "Lote destino no existe" }, null);

            var dto = new CreateMovimientoAvesDto
            {
                FechaMovimiento    = request.Fecha,
                TipoMovimiento     = "Traslado",
                LoteOrigenId       = request.LoteOrigenId,
                GranjaOrigenId     = loteOrigen.GranjaId,
                NucleoOrigenId     = loteOrigen.NucleoId,
                GalponOrigenId     = loteOrigen.GalponId,
                LoteDestinoId      = request.LoteDestinoId,
                GranjaDestinoId    = loteDestino.GranjaId,
                NucleoDestinoId    = loteDestino.NucleoId,
                GalponDestinoId    = loteDestino.GalponId,
                CantidadHembras    = request.CantidadHembras,
                CantidadMachos     = request.CantidadMachos,
                CantidadMixtas     = 0,
                MotivoMovimiento   = "Traslado desde seguimiento diario",
                Observaciones      = request.Observaciones,
                UsuarioMovimientoId = _currentUser.UserId
            };

            var movimiento = await CreateAsync(dto);
            return new ResultadoMovimientoDto(true, "Traslado registrado correctamente", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimiento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar traslado {LoteOrigen}→{LoteDestino}", request.LoteOrigenId, request.LoteDestinoId);
            return new ResultadoMovimientoDto(false, ex.Message, null, null, new List<string> { ex.Message }, null);
        }
    }

    public async Task<ResultadoMovimientoDto> EjecutarTrasladoCierreLevanteAsync(TrasladoCierreLevanteRequest request)
    {
        try
        {
            if (request.HembrasTraslado <= 0 && request.MachosTraslado <= 0)
                return new ResultadoMovimientoDto(true, "Sin aves para trasladar en el cierre", null, null, new List<string>(), null);

            // Obtener el LoteId real desde LotePosturaLevante
            var posLevante = await _context.LotePosturaLevante.AsNoTracking()
                .FirstOrDefaultAsync(p => p.LotePosturaLevanteId == request.LotePosturaLevanteId &&
                                          p.CompanyId == _currentUser.CompanyId &&
                                          p.DeletedAt == null);
            if (posLevante is null)
                return new ResultadoMovimientoDto(false, $"LotePosturaLevante {request.LotePosturaLevanteId} no encontrado", null, null, new List<string> { "Postura levante no existe" }, null);

            if (posLevante.LoteId is null)
                return new ResultadoMovimientoDto(false, "LotePosturaLevante no tiene LoteId asociado", null, null, new List<string> { "LoteId nulo" }, null);

            var loteOrigen = await _context.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == posLevante.LoteId &&
                                          l.CompanyId == _currentUser.CompanyId &&
                                          l.DeletedAt == null);
            if (loteOrigen is null)
                return new ResultadoMovimientoDto(false, $"Lote {posLevante.LoteId} no encontrado", null, null, new List<string> { "Lote origen no existe" }, null);

            var dto = new CreateMovimientoAvesDto
            {
                FechaMovimiento  = request.Fecha,
                TipoMovimiento   = "Traslado",
                LoteOrigenId     = posLevante.LoteId.Value,
                GranjaOrigenId   = loteOrigen.GranjaId,
                NucleoOrigenId   = loteOrigen.NucleoId,
                GalponOrigenId   = loteOrigen.GalponId,
                CantidadHembras  = request.HembrasTraslado,
                CantidadMachos   = request.MachosTraslado,
                CantidadMixtas   = 0,
                MotivoMovimiento = "Traslado por cierre de lote levante",
                Observaciones    = BuildObsCierreLevante(request),
                UsuarioMovimientoId = _currentUser.UserId
            };

            // Si hay lote de producción destino, completar datos de destino
            if (request.LotePosturaProduccionId.HasValue)
            {
                var posProduccion = await _context.LotePosturaProduccion.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.LotePosturaProduccionId == request.LotePosturaProduccionId &&
                                              p.CompanyId == _currentUser.CompanyId &&
                                              p.DeletedAt == null);

                if (posProduccion?.LoteId != null)
                {
                    var loteDestino = await _context.Lotes.AsNoTracking()
                        .FirstOrDefaultAsync(l => l.LoteId == posProduccion.LoteId &&
                                                  l.CompanyId == _currentUser.CompanyId &&
                                                  l.DeletedAt == null);
                    if (loteDestino != null)
                    {
                        dto.LoteDestinoId   = loteDestino.LoteId;
                        dto.GranjaDestinoId = loteDestino.GranjaId;
                        dto.NucleoDestinoId = loteDestino.NucleoId;
                        dto.GalponDestinoId = loteDestino.GalponId;
                    }
                }
            }

            var movimiento = await CreateAsync(dto);
            return new ResultadoMovimientoDto(true, "Traslado de cierre registrado correctamente", movimiento.Id, movimiento.NumeroMovimiento, new List<string>(), movimiento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar traslado de cierre levante {LotePosturaLevanteId}", request.LotePosturaLevanteId);
            return new ResultadoMovimientoDto(false, ex.Message, null, null, new List<string> { ex.Message }, null);
        }
    }

    private static string BuildObsCierreLevante(TrasladoCierreLevanteRequest r)
    {
        var sb = new System.Text.StringBuilder("Cierre de lote levante");
        if (r.LiquidacionCierreId.HasValue)
            sb.Append($" (Liquidación #{r.LiquidacionCierreId})");
        if (!string.IsNullOrWhiteSpace(r.Observaciones))
            sb.Append($" — {r.Observaciones}");
        return sb.ToString();
    }
}
