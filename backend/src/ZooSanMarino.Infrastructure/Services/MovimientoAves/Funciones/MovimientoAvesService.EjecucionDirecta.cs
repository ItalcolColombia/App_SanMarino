// MovimientoAves/Funciones/MovimientoAvesService.EjecucionDirecta.cs
// Traslado de aves por CIERRE de lote levante → producción.
// Los métodos EjecutarVentaAsync/EjecutarTrasladoAsync (endpoints `ejecutar-venta` y
// `ejecutar-traslado`) se eliminaron el 3-sep-2026: quedaron sin un solo llamador en todo el
// repo — la venta y el traslado manuales van por `POST /api/traslados/aves` (Camino A) y por
// `POST /api/traslados/aves-desde-seguimiento` (Camino C).
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
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
