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
        var semanaActual = MovimientoAvesCalculos.SemanaDesdeEncaset(fechaMovimiento, lote.FechaEncaset.Value);

        // Solo aplicar descuento si el lote está en producción (semana 26 o más)
        if (MovimientoAvesCalculos.EstaEnLevante(semanaActual))
            return;

        var loteIdInt = movimiento.LoteOrigenId.Value;

        // Buscar registro existente para esa fecha
        var registroExistente = await _context.SeguimientoProduccion
            .Where(s => s.LoteId == loteIdInt && s.Fecha.Date == fechaMovimiento)
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
                LoteId = loteIdInt,
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
                Etapa = MovimientoAvesCalculos.EtapaProduccion(semanaActual),
                Observaciones = $"Registro de descuento por traslado {movimiento.NumeroMovimiento} - {movimiento.TipoMovimiento} " +
                               $"(H: {movimiento.CantidadHembras}, M: {movimiento.CantidadMachos})"
            };

            _context.SeguimientoProduccion.Add(registroDescuento);
            await _context.SaveChangesAsync();
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
            var registroProduccion = await _context.SeguimientoProduccion
                .Where(s => s.LoteId == movimiento.LoteOrigenId!.Value && s.Fecha.Date == fechaMovimiento)
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
            var loteIdDestino = movimiento.LoteDestinoId.Value;
            var registroExistente = await _context.SeguimientoProduccion
                .Where(s => s.LoteId == loteIdDestino && s.Fecha.Date == fechaMovimiento)
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
                    LoteId = loteIdDestino,
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
                    Etapa = MovimientoAvesCalculos.EtapaProduccion(semanaActual),
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
            var loteIdInt = movimiento.LoteOrigenId.Value;
            var registroExistente = await _context.SeguimientoProduccion
                .Where(s => s.LoteId == loteIdInt && s.Fecha.Date == fechaMovimiento)
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
            var loteIdDestino = movimiento.LoteDestinoId.Value;
            var registroExistente = await _context.SeguimientoProduccion
                .Where(s => s.LoteId == loteIdDestino && s.Fecha.Date == fechaMovimiento)
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
