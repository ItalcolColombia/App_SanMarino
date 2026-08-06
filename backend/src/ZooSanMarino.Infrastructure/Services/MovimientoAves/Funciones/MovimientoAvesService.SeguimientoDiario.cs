// MovimientoAves/Funciones/MovimientoAvesService.SeguimientoDiario.cs
// Efecto del movimiento sobre el seguimiento diario (levante y producción): descuento en origen,
// entrada en destino, devolución por cancelación, ajuste por edición y registro de retiros.
// La discriminación semana/fase y la etapa de producción delegan en MovimientoAvesCalculos.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
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

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers de convergencia a Feature-13 (seguimiento_diario, tipo='levante')
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>LotePosturaLevante activa del lote (tracked) para mantener acumulados de traslado.</summary>
    private Task<LotePosturaLevante?> ResolverLplLevanteAsync(int loteId) =>
        _context.LotePosturaLevante
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Upsert de la fila canónica de levante por (tipo='levante', lote, fecha.Date).
    /// Si no existe la crea con los NOT-NULL canónicos (convención Feature-13,
    /// igual que TrasladoAvesDesdeSegService). No toca Sel/Mortalidad.
    /// </summary>
    private async Task<SeguimientoDiario> UpsertSeguimientoLevanteAsync(int loteId, DateTime fechaDate, int? lotePosturaLevanteId)
    {
        var seg = await _context.SeguimientoDiario
            .Where(s => s.TipoSeguimiento == "levante"
                     && s.LoteId == loteId.ToString()
                     && s.Fecha.Date == fechaDate)
            .FirstOrDefaultAsync();

        if (seg is null)
        {
            seg = new SeguimientoDiario
            {
                TipoSeguimiento = "levante",
                LoteId = loteId.ToString(),
                LotePosturaLevanteId = lotePosturaLevanteId,
                Fecha = fechaDate,
                MortalidadHembras = 0, MortalidadMachos = 0,
                SelH = 0, SelM = 0,
                ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                Ciclo = "Traslado",
                TipoAlimento = "—",
                CreatedByUserId = _currentUser.UserGuid?.ToString() ?? _currentUser.UserId.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            _context.SeguimientoDiario.Add(seg);
        }
        return seg;
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
        var semanaActual = MovimientoAvesCalculos.SemanaDesdeEncaset(fechaMovimiento, lote.FechaEncaset.Value);

        // Solo aplicar descuento si el lote está en levante (semana < 26)
        if (MovimientoAvesCalculos.EstaEnProduccion(semanaActual))
            return;

        var loteId = movimiento.LoteOrigenId.Value;
        var lplOrigen = await ResolverLplLevanteAsync(loteId);

        // Convergencia Feature-13: NO se codifica el movimiento como ±Sel. Se usa la
        // fila canónica con columnas dedicadas de traslado/venta (misma convención que
        // TrasladoAvesDesdeSegService), preservando el saldo físico de aves.
        var seg = await UpsertSeguimientoLevanteAsync(loteId, fechaMovimiento, lplOrigen?.LotePosturaLevanteId);

        string obs;
        if (movimiento.TipoMovimiento == "Venta")
        {
            // La venta NO es traslado. Se registra el total para display/auditoría; el
            // descuento del saldo lo aporta el registro MovimientoAves (así lo consumen
            // los indicadores). No se tocan splits de traslado ni acumulados.
            seg.VentaAvesCantidad = (seg.VentaAvesCantidad ?? 0) + (movimiento.CantidadHembras + movimiento.CantidadMachos);
            seg.VentaAvesMotivo = movimiento.MotivoMovimiento;
            obs = $"Venta {movimiento.NumeroMovimiento} (H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})";
        }
        else
        {
            // Traslado SALIDA en el lote origen (columnas dedicadas Feature-13).
            LotePosturaLevante? lplDestino = movimiento.LoteDestinoId.HasValue
                ? await ResolverLplLevanteAsync(movimiento.LoteDestinoId.Value)
                : null;

            seg.TrasladoSalidaHembras += movimiento.CantidadHembras;
            seg.TrasladoSalidaMachos  += movimiento.CantidadMachos;
            seg.TrasladoAvesSalida     = (seg.TrasladoAvesSalida ?? 0) + (movimiento.CantidadHembras + movimiento.CantidadMachos);
            seg.EsTraslado             = true;
            seg.TrasladoDireccion      = "SALIDA";
            seg.TrasladoLoteContraparteId   = lplDestino?.LotePosturaLevanteId;
            seg.TrasladoGranjaContraparteId = movimiento.GranjaDestinoId;

            // Mantener acumulados de traslado en la LPL origen (lo que el hack NO hacía),
            // para que GetMortalidadResumenAsync refleje el traslado.
            if (lplOrigen != null)
            {
                lplOrigen.LevanteTrasladoSalidaHembras += movimiento.CantidadHembras;
                lplOrigen.LevanteTrasladoSalidaMachos  += movimiento.CantidadMachos;
            }
            obs = $"Traslado SALIDA {movimiento.NumeroMovimiento} (H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})";
        }

        seg.Observaciones = string.IsNullOrEmpty(seg.Observaciones) ? obs : $"{seg.Observaciones} | {obs}";
        seg.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    /// <summary>LotePosturaProduccion viva del lote (tracked) para acumulados de traslado y FK de la fila.</summary>
    private Task<LotePosturaProduccion?> ResolverLppProduccionAsync(int loteId) =>
        _context.LotePosturaProduccion
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Fila diaria de producción del día calendario (rango UTC sargable, no <c>.Fecha.Date ==</c>:
    /// EF lo traduce a date_trunc dependiente de la TZ de la sesión — gotcha FechasPuras).
    /// </summary>
    private Task<SeguimientoProduccion?> BuscarSeguimientoProduccionDelDiaAsync(int loteId, DateTime fechaDia)
    {
        var (diaDesde, diaHasta) = FechasPuras.RangoDiaUtc(fechaDia);
        return _context.SeguimientoProduccion
            .Where(s => s.LoteId == loteId && s.Fecha >= diaDesde && s.Fecha < diaHasta)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Upsert de la fila canónica de producción por (lote, día calendario). Si no existe la crea
    /// con los NOT-NULL canónicos, fecha anclada a MEDIODÍA, FK al LPP vivo (visible en la grilla
    /// del LPP) y auditoría — convención TrasladoAvesDesdeSegService/carga masiva. No toca Sel/Mortalidad.
    /// </summary>
    private async Task<SeguimientoProduccion> UpsertSeguimientoProduccionAsync(int loteId, DateTime fechaDia, int semanaActual)
    {
        var seg = await BuscarSeguimientoProduccionDelDiaAsync(loteId, fechaDia);
        if (seg is null)
        {
            var lpp = await ResolverLppProduccionAsync(loteId);
            seg = new SeguimientoProduccion
            {
                LoteId = loteId,
                LotePosturaProduccionId = lpp?.LotePosturaProduccionId,
                Fecha = FechasPuras.AnclarMediodiaUtc(fechaDia.Date),
                MortalidadH = 0, MortalidadM = 0,
                SelH = 0, SelM = 0,
                ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                TipoAlimento = "—",
                PesoHuevo = 0,
                Etapa = MovimientoAvesCalculos.EtapaProduccion(semanaActual),
                CompanyId = _currentUser.CompanyId,
                CreatedByUserId = _currentUser.UserId,
                CreatedAt = DateTime.UtcNow
            };
            _context.SeguimientoProduccion.Add(seg);
        }
        return seg;
    }

    /// <summary>
    /// Rastro del movimiento en el seguimiento diario de producción (solo si el lote está en
    /// producción — semana 26+). Convergencia D3: NO se codifica como ±Sel (el hack viejo escribía
    /// <c>sel_h</c>/<c>mortalidad_machos</c> NEGATIVOS y corrompía los contadores del día); el
    /// descuento del saldo lo aportan el registro MovimientoAves + el espejo LPP, y así lo consume
    /// <c>fn_seguimiento_diario_produccion</c>. Acá solo queda el rastro tipado (traslado) o la
    /// nota (venta, misma convención que la carga masiva).
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
        var semanaActual = MovimientoAvesCalculos.SemanaDesdeEncaset(fechaMovimiento, lote.FechaEncaset.Value);

        // Solo aplicar si el lote está en producción (semana 26 o más)
        if (MovimientoAvesCalculos.EstaEnLevante(semanaActual))
            return;

        var loteIdInt = movimiento.LoteOrigenId.Value;

        if (movimiento.TipoMovimiento == "Venta")
        {
            // La venta deja su CANTIDAD en la fila diaria (venta_aves_hembras/machos), no solo la
            // nota: así la grilla diaria la muestra y cuadra con la carga masiva. Sin fila del día
            // la fn diaria igual genera el día movimiento-only con mov_venta_* desde
            // movimiento_aves — no se crea una fila solo para esto.
            var segVenta = await BuscarSeguimientoProduccionDelDiaAsync(loteIdInt, fechaMovimiento);
            if (segVenta is null) return;

            segVenta.VentaAvesHembras += movimiento.CantidadHembras;
            segVenta.VentaAvesMachos += movimiento.CantidadMachos;
            if (!string.IsNullOrWhiteSpace(movimiento.MotivoMovimiento))
                segVenta.VentaAvesMotivo = string.IsNullOrWhiteSpace(segVenta.VentaAvesMotivo)
                    ? movimiento.MotivoMovimiento
                    : $"{segVenta.VentaAvesMotivo} | {movimiento.MotivoMovimiento}";

            var ventaTxt = $"Venta de aves {movimiento.NumeroMovimiento}: {movimiento.CantidadHembras} H / {movimiento.CantidadMachos} M" +
                           (string.IsNullOrWhiteSpace(movimiento.MotivoMovimiento) ? "" : $" ({movimiento.MotivoMovimiento})");
            segVenta.Observaciones = string.IsNullOrEmpty(segVenta.Observaciones) ? ventaTxt : $"{segVenta.Observaciones} | {ventaTxt}";
            segVenta.UpdatedByUserId = _currentUser.UserId;
            segVenta.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return;
        }

        // Traslado SALIDA en el lote origen (columnas dedicadas, espejo de TrasladoAvesDesdeSegService).
        var seg = await UpsertSeguimientoProduccionAsync(loteIdInt, fechaMovimiento, semanaActual);
        seg.TrasladoSalidaHembras += movimiento.CantidadHembras;
        seg.TrasladoSalidaMachos  += movimiento.CantidadMachos;
        seg.TrasladoHembras = (seg.TrasladoHembras ?? 0) + movimiento.CantidadHembras; // legacy R3
        seg.TrasladoMachos  = (seg.TrasladoMachos  ?? 0) + movimiento.CantidadMachos;
        seg.EsTraslado = true;
        seg.TrasladoDireccion = "SALIDA";
        seg.LoteDestinoId = movimiento.LoteDestinoId;       // lote base contraparte (informativo)
        seg.GranjaDestinoId = movimiento.GranjaDestinoId;
        seg.FechaTraslado = FechasPuras.AnclarMediodiaUtc(fechaMovimiento.Date);

        // Acumulados de traslado en fase producción del LPP origen (patrón Feature 14)
        var lppOrigen = await ResolverLppProduccionAsync(loteIdInt);
        if (lppOrigen != null)
        {
            lppOrigen.ProduccionTrasladoSalidaHembras += movimiento.CantidadHembras;
            lppOrigen.ProduccionTrasladoSalidaMachos  += movimiento.CantidadMachos;
        }

        var obsTraslado = $"Traslado SALIDA {movimiento.NumeroMovimiento} (H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})";
        seg.Observaciones = string.IsNullOrEmpty(seg.Observaciones) ? obsTraslado : $"{seg.Observaciones} | {obsTraslado}";
        seg.UpdatedByUserId = _currentUser.UserId;
        seg.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
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
        var semanaActual = MovimientoAvesCalculos.SemanaDesdeEncaset(fechaMovimiento, lote.FechaEncaset.Value);

        // Si es Levante (semana < 26)
        if (MovimientoAvesCalculos.EstaEnLevante(semanaActual))
        {
            // Convergencia Feature-13: invertir los splits de traslado / venta del ORIGEN
            // (misma cobertura que el legacy, que solo revertía el origen).
            var loteIdOrigen = movimiento.LoteOrigenId.Value;
            var seg = await _context.SeguimientoDiario
                .Where(s => s.TipoSeguimiento == "levante"
                         && s.LoteId == loteIdOrigen.ToString()
                         && s.Fecha.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (seg != null)
            {
                if (movimiento.TipoMovimiento == "Venta")
                {
                    seg.VentaAvesCantidad = Math.Max(0, (seg.VentaAvesCantidad ?? 0) - (movimiento.CantidadHembras + movimiento.CantidadMachos));
                }
                else
                {
                    seg.TrasladoSalidaHembras = Math.Max(0, seg.TrasladoSalidaHembras - movimiento.CantidadHembras);
                    seg.TrasladoSalidaMachos  = Math.Max(0, seg.TrasladoSalidaMachos  - movimiento.CantidadMachos);
                    seg.TrasladoAvesSalida     = Math.Max(0, (seg.TrasladoAvesSalida ?? 0) - (movimiento.CantidadHembras + movimiento.CantidadMachos));

                    var lplOrigen = await ResolverLplLevanteAsync(loteIdOrigen);
                    if (lplOrigen != null)
                    {
                        lplOrigen.LevanteTrasladoSalidaHembras = Math.Max(0, lplOrigen.LevanteTrasladoSalidaHembras - movimiento.CantidadHembras);
                        lplOrigen.LevanteTrasladoSalidaMachos  = Math.Max(0, lplOrigen.LevanteTrasladoSalidaMachos  - movimiento.CantidadMachos);
                    }

                    if (seg.TrasladoSalidaHembras == 0 && seg.TrasladoSalidaMachos == 0
                        && seg.TrasladoIngresoHembras == 0 && seg.TrasladoIngresoMachos == 0)
                    {
                        seg.EsTraslado = false;
                        seg.TrasladoDireccion = null;
                    }
                }

                var obsDevolucion = $"Aves devueltas por cancelación de movimiento {movimiento.NumeroMovimiento}";
                seg.Observaciones = string.IsNullOrEmpty(seg.Observaciones)
                    ? obsDevolucion
                    : $"{seg.Observaciones} | {obsDevolucion}";
                seg.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        // Si es Producción (semana >= 26)
        else
        {
            // Convergencia D3: invertir los splits de traslado del ORIGEN (no ±Sel); la venta
            // nunca escribió números en la fila diaria, solo queda la nota de cancelación.
            var registroProduccion = await BuscarSeguimientoProduccionDelDiaAsync(movimiento.LoteOrigenId!.Value, fechaMovimiento);

            if (registroProduccion != null)
            {
                if (movimiento.TipoMovimiento != "Venta")
                {
                    registroProduccion.TrasladoSalidaHembras = Math.Max(0, registroProduccion.TrasladoSalidaHembras - movimiento.CantidadHembras);
                    registroProduccion.TrasladoSalidaMachos  = Math.Max(0, registroProduccion.TrasladoSalidaMachos  - movimiento.CantidadMachos);
                    registroProduccion.TrasladoHembras = Math.Max(0, (registroProduccion.TrasladoHembras ?? 0) - movimiento.CantidadHembras);
                    registroProduccion.TrasladoMachos  = Math.Max(0, (registroProduccion.TrasladoMachos  ?? 0) - movimiento.CantidadMachos);

                    var lppOrigen = await ResolverLppProduccionAsync(movimiento.LoteOrigenId.Value);
                    if (lppOrigen != null)
                    {
                        lppOrigen.ProduccionTrasladoSalidaHembras = Math.Max(0, lppOrigen.ProduccionTrasladoSalidaHembras - movimiento.CantidadHembras);
                        lppOrigen.ProduccionTrasladoSalidaMachos  = Math.Max(0, lppOrigen.ProduccionTrasladoSalidaMachos  - movimiento.CantidadMachos);
                    }

                    if (registroProduccion.TrasladoSalidaHembras == 0 && registroProduccion.TrasladoSalidaMachos == 0
                        && registroProduccion.TrasladoIngresoHembras == 0 && registroProduccion.TrasladoIngresoMachos == 0)
                    {
                        registroProduccion.EsTraslado = false;
                        registroProduccion.TrasladoDireccion = null;
                    }
                }

                var obsDevolucion = $"Aves devueltas por cancelación de movimiento {movimiento.NumeroMovimiento}";
                registroProduccion.Observaciones = string.IsNullOrEmpty(registroProduccion.Observaciones)
                    ? obsDevolucion
                    : $"{registroProduccion.Observaciones} | {obsDevolucion}";
                registroProduccion.UpdatedByUserId = _currentUser.UserId;
                registroProduccion.UpdatedAt = DateTime.UtcNow;

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
        var semanaActual = MovimientoAvesCalculos.SemanaDesdeEncaset(fechaMovimiento, loteDestino.FechaEncaset.Value);

        // Si es Levante (semana < 26)
        if (MovimientoAvesCalculos.EstaEnLevante(semanaActual))
        {
            // Convergencia Feature-13: entrada en destino con columnas dedicadas de
            // traslado INGRESO (idéntico a TrasladoAvesDesdeSegService). NO ±Sel.
            var loteDestinoId = movimiento.LoteDestinoId.Value;
            var lplDestino = await ResolverLplLevanteAsync(loteDestinoId);
            var lplOrigen = movimiento.LoteOrigenId.HasValue
                ? await ResolverLplLevanteAsync(movimiento.LoteOrigenId.Value)
                : null;

            var seg = await UpsertSeguimientoLevanteAsync(loteDestinoId, fechaMovimiento, lplDestino?.LotePosturaLevanteId);

            seg.TrasladoIngresoHembras += movimiento.CantidadHembras;
            seg.TrasladoIngresoMachos  += movimiento.CantidadMachos;
            seg.TrasladoAvesEntrante     = (seg.TrasladoAvesEntrante ?? 0) + (movimiento.CantidadHembras + movimiento.CantidadMachos);
            seg.EsTraslado               = true;
            seg.TrasladoDireccion        = "INGRESO";
            seg.TrasladoLoteContraparteId   = lplOrigen?.LotePosturaLevanteId;
            seg.TrasladoGranjaContraparteId = movimiento.GranjaOrigenId;

            if (lplDestino != null)
            {
                lplDestino.LevanteTrasladoIngresoHembras += movimiento.CantidadHembras;
                lplDestino.LevanteTrasladoIngresoMachos  += movimiento.CantidadMachos;
            }

            var obsEntrada = $"Traslado INGRESO {movimiento.NumeroMovimiento} (H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})";
            seg.Observaciones = string.IsNullOrEmpty(seg.Observaciones) ? obsEntrada : $"{seg.Observaciones} | {obsEntrada}";
            seg.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
        // Si es Producción (semana >= 26)
        else
        {
            // Convergencia D3: entrada en destino con columnas dedicadas de traslado INGRESO
            // (espejo de TrasladoAvesDesdeSegService), NO ±Sel.
            var loteIdDestino = movimiento.LoteDestinoId.Value;
            var seg = await UpsertSeguimientoProduccionAsync(loteIdDestino, fechaMovimiento, semanaActual);

            seg.TrasladoIngresoHembras += movimiento.CantidadHembras;
            seg.TrasladoIngresoMachos  += movimiento.CantidadMachos;
            seg.TrasladoHembras = (seg.TrasladoHembras ?? 0) + movimiento.CantidadHembras; // legacy R3
            seg.TrasladoMachos  = (seg.TrasladoMachos  ?? 0) + movimiento.CantidadMachos;
            seg.EsTraslado = true;
            seg.TrasladoDireccion = "INGRESO";

            // Acumulados de traslado en fase producción del LPP destino (patrón Feature 14)
            var lppDestino = await ResolverLppProduccionAsync(loteIdDestino);
            if (lppDestino != null)
            {
                lppDestino.ProduccionTrasladoIngresoHembras += movimiento.CantidadHembras;
                lppDestino.ProduccionTrasladoIngresoMachos  += movimiento.CantidadMachos;
            }

            var obsEntrada = $"Traslado INGRESO {movimiento.NumeroMovimiento} (H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})";
            seg.Observaciones = string.IsNullOrEmpty(seg.Observaciones)
                ? obsEntrada
                : $"{seg.Observaciones} | {obsEntrada}";
            seg.UpdatedByUserId = _currentUser.UserId;
            seg.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
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
        var semanaActual = MovimientoAvesCalculos.SemanaDesdeEncaset(fechaMovimiento, lote.FechaEncaset.Value);

        // Si es Levante (semana < 26)
        if (MovimientoAvesCalculos.EstaEnLevante(semanaActual))
        {
            // Convergencia Feature-13: ajustar por delta (new - original) sobre los splits
            // de traslado SALIDA / venta del ORIGEN (no ±Sel).
            var loteIdOrigen = movimiento.LoteOrigenId.Value;
            var seg = await _context.SeguimientoDiario
                .Where(s => s.TipoSeguimiento == "levante"
                         && s.LoteId == loteIdOrigen.ToString()
                         && s.Fecha.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (seg != null)
            {
                var deltaH = movimiento.CantidadHembras - cantidadesOriginales["Hembras"];
                var deltaM = movimiento.CantidadMachos - cantidadesOriginales["Machos"];

                if (movimiento.TipoMovimiento == "Venta")
                {
                    seg.VentaAvesCantidad = Math.Max(0, (seg.VentaAvesCantidad ?? 0) + deltaH + deltaM);
                }
                else
                {
                    seg.TrasladoSalidaHembras = Math.Max(0, seg.TrasladoSalidaHembras + deltaH);
                    seg.TrasladoSalidaMachos  = Math.Max(0, seg.TrasladoSalidaMachos  + deltaM);
                    seg.TrasladoAvesSalida     = Math.Max(0, (seg.TrasladoAvesSalida ?? 0) + deltaH + deltaM);

                    var lplOrigen = await ResolverLplLevanteAsync(loteIdOrigen);
                    if (lplOrigen != null)
                    {
                        lplOrigen.LevanteTrasladoSalidaHembras = Math.Max(0, lplOrigen.LevanteTrasladoSalidaHembras + deltaH);
                        lplOrigen.LevanteTrasladoSalidaMachos  = Math.Max(0, lplOrigen.LevanteTrasladoSalidaMachos  + deltaM);
                    }
                }

                var obsAjuste = $"Ajuste por edición de movimiento {movimiento.NumeroMovimiento}";
                seg.Observaciones = string.IsNullOrEmpty(seg.Observaciones)
                    ? obsAjuste
                    : $"{seg.Observaciones} | {obsAjuste}";
                seg.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        // Si es Producción (semana >= 26)
        else
        {
            // Convergencia D3: ajustar por delta (new - original) sobre los splits de traslado
            // SALIDA / la nota de venta del ORIGEN (no ±Sel).
            var loteIdInt = movimiento.LoteOrigenId.Value;
            var registroExistente = await BuscarSeguimientoProduccionDelDiaAsync(loteIdInt, fechaMovimiento);

            if (registroExistente != null)
            {
                var deltaH = movimiento.CantidadHembras - cantidadesOriginales["Hembras"];
                var deltaM = movimiento.CantidadMachos - cantidadesOriginales["Machos"];

                if (movimiento.TipoMovimiento != "Venta")
                {
                    registroExistente.TrasladoSalidaHembras = Math.Max(0, registroExistente.TrasladoSalidaHembras + deltaH);
                    registroExistente.TrasladoSalidaMachos  = Math.Max(0, registroExistente.TrasladoSalidaMachos  + deltaM);
                    registroExistente.TrasladoHembras = Math.Max(0, (registroExistente.TrasladoHembras ?? 0) + deltaH);
                    registroExistente.TrasladoMachos  = Math.Max(0, (registroExistente.TrasladoMachos  ?? 0) + deltaM);

                    var lppOrigen = await ResolverLppProduccionAsync(loteIdInt);
                    if (lppOrigen != null)
                    {
                        lppOrigen.ProduccionTrasladoSalidaHembras = Math.Max(0, lppOrigen.ProduccionTrasladoSalidaHembras + deltaH);
                        lppOrigen.ProduccionTrasladoSalidaMachos  = Math.Max(0, lppOrigen.ProduccionTrasladoSalidaMachos  + deltaM);
                    }
                }

                var obsAjuste = $"Ajuste por edición de movimiento {movimiento.NumeroMovimiento}";
                registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                    ? obsAjuste
                    : $"{registroExistente.Observaciones} | {obsAjuste}";
                registroExistente.UpdatedByUserId = _currentUser.UserId;
                registroExistente.UpdatedAt = DateTime.UtcNow;

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
        var semanaActual = MovimientoAvesCalculos.SemanaDesdeEncaset(fechaMovimiento, loteDestino.FechaEncaset.Value);

        // Si es Levante (semana < 26)
        if (MovimientoAvesCalculos.EstaEnLevante(semanaActual))
        {
            // Convergencia Feature-13: ajustar por delta (new - original) sobre el traslado
            // INGRESO del DESTINO (no ±Sel).
            var loteDestinoId = movimiento.LoteDestinoId.Value;
            var seg = await _context.SeguimientoDiario
                .Where(s => s.TipoSeguimiento == "levante"
                         && s.LoteId == loteDestinoId.ToString()
                         && s.Fecha.Date == fechaMovimiento)
                .FirstOrDefaultAsync();

            if (seg != null)
            {
                var deltaH = movimiento.CantidadHembras - cantidadesOriginales["Hembras"];
                var deltaM = movimiento.CantidadMachos - cantidadesOriginales["Machos"];

                seg.TrasladoIngresoHembras = Math.Max(0, seg.TrasladoIngresoHembras + deltaH);
                seg.TrasladoIngresoMachos  = Math.Max(0, seg.TrasladoIngresoMachos  + deltaM);
                seg.TrasladoAvesEntrante     = Math.Max(0, (seg.TrasladoAvesEntrante ?? 0) + deltaH + deltaM);

                var lplDestino = await ResolverLplLevanteAsync(loteDestinoId);
                if (lplDestino != null)
                {
                    lplDestino.LevanteTrasladoIngresoHembras = Math.Max(0, lplDestino.LevanteTrasladoIngresoHembras + deltaH);
                    lplDestino.LevanteTrasladoIngresoMachos  = Math.Max(0, lplDestino.LevanteTrasladoIngresoMachos  + deltaM);
                }

                var obsAjuste = $"Ajuste por edición de movimiento {movimiento.NumeroMovimiento}";
                seg.Observaciones = string.IsNullOrEmpty(seg.Observaciones)
                    ? obsAjuste
                    : $"{seg.Observaciones} | {obsAjuste}";
                seg.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        // Si es Producción (semana >= 26)
        else
        {
            // Convergencia D3: ajustar por delta (new - original) sobre el traslado INGRESO
            // del DESTINO (no ±Sel).
            var loteIdDestino = movimiento.LoteDestinoId.Value;
            var registroExistente = await BuscarSeguimientoProduccionDelDiaAsync(loteIdDestino, fechaMovimiento);

            if (registroExistente != null)
            {
                var deltaH = movimiento.CantidadHembras - cantidadesOriginales["Hembras"];
                var deltaM = movimiento.CantidadMachos - cantidadesOriginales["Machos"];

                registroExistente.TrasladoIngresoHembras = Math.Max(0, registroExistente.TrasladoIngresoHembras + deltaH);
                registroExistente.TrasladoIngresoMachos  = Math.Max(0, registroExistente.TrasladoIngresoMachos  + deltaM);
                registroExistente.TrasladoHembras = Math.Max(0, (registroExistente.TrasladoHembras ?? 0) + deltaH);
                registroExistente.TrasladoMachos  = Math.Max(0, (registroExistente.TrasladoMachos  ?? 0) + deltaM);

                var lppDestino = await ResolverLppProduccionAsync(loteIdDestino);
                if (lppDestino != null)
                {
                    lppDestino.ProduccionTrasladoIngresoHembras = Math.Max(0, lppDestino.ProduccionTrasladoIngresoHembras + deltaH);
                    lppDestino.ProduccionTrasladoIngresoMachos  = Math.Max(0, lppDestino.ProduccionTrasladoIngresoMachos  + deltaM);
                }

                var obsAjuste = $"Ajuste por edición de movimiento {movimiento.NumeroMovimiento}";
                registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                    ? obsAjuste
                    : $"{registroExistente.Observaciones} | {obsAjuste}";
                registroExistente.UpdatedByUserId = _currentUser.UserId;
                registroExistente.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
    }
}
