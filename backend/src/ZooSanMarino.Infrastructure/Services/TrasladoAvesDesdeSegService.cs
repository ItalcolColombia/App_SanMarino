using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio de traslados de aves desde la pantalla "Seguimiento Diario".
///
/// Feature 13 — Reescrito para:
///   1. Validar misma etapa (Levante→Levante o Producción→Producción).
///   2. Usar el saldo REAL (no aves_h_actual del encasetamiento) — obtenido
///      vía ILoteService.GetMortalidadResumenAsync que ya incluye traslados.
///   3. Generar DOS registros en seguimiento_diario (SALIDA en origen,
///      INGRESO en destino) con es_traslado=true y dirección.
///   4. Actualizar los acumulados traslado_ingreso_/salida_ en lote_postura_levante.
///   5. Mantener aves_h_actual / aves_m_actual para compatibilidad con código legacy.
/// </summary>
public class TrasladoAvesDesdeSegService : ITrasladoAvesDesdeSegService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly ILoteService _loteService;

    public TrasladoAvesDesdeSegService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        ILoteService loteService)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _loteService = loteService;
    }

    private async Task<int> GetEffectiveCompanyIdAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    public async Task<DisponibilidadAvesDto?> GetDisponibilidadAvesAsync(
        int loteId, string tipo, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);

        if (tipo.Equals("Levante", StringComparison.OrdinalIgnoreCase))
        {
            var lpl = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Include(l => l.Farm)
                .Where(l => l.LotePosturaLevanteId == loteId
                         && l.CompanyId == companyId
                         && l.DeletedAt == null)
                .FirstOrDefaultAsync(ct);

            if (lpl is null) return null;

            // ── Saldo REAL: si el LPL tiene Lote base asociado, usar resumen-mortalidad
            int avesHReal = lpl.AvesHActual ?? 0;
            int avesMReal = lpl.AvesMActual ?? 0;
            if (lpl.LoteId is int loteBaseId)
            {
                var resumen = await _loteService.GetMortalidadResumenAsync(loteBaseId);
                if (resumen != null)
                {
                    avesHReal = resumen.SaldoHembras;
                    avesMReal = resumen.SaldoMachos;
                }
            }

            return new DisponibilidadAvesDto(
                LoteId: loteId,
                LoteNombre: lpl.LoteNombre,
                TipoLote: "Levante",
                AvesHActual: avesHReal,
                AvesMActual: avesMReal,
                GranjaId: lpl.GranjaId,
                GranjaNombre: lpl.Farm?.Name,
                GalponId: lpl.GalponId,
                GalponNombre: null
            );
        }
        else
        {
            var lpp = await _ctx.LotePosturaProduccion
                .AsNoTracking()
                .Include(l => l.Farm)
                .Where(l => l.LotePosturaProduccionId == loteId
                         && l.CompanyId == companyId
                         && l.DeletedAt == null)
                .FirstOrDefaultAsync(ct);

            if (lpp is null) return null;

            return new DisponibilidadAvesDto(
                LoteId: loteId,
                LoteNombre: lpp.LoteNombre,
                TipoLote: "Produccion",
                AvesHActual: lpp.AvesHActual ?? 0,
                AvesMActual: lpp.AvesMActual ?? 0,
                GranjaId: lpp.GranjaId,
                GranjaNombre: lpp.Farm?.Name,
                GalponId: lpp.GalponId,
                GalponNombre: null
            );
        }
    }

    public async Task<TrasladoAvesResultDto> EjecutarTrasladoDesdeSegAsync(
        TrasladoAvesDesdeSegDiarioDto dto,
        int usuarioId,
        CancellationToken ct = default)
    {
        // ── 0. Validación de etapa (same-phase) ──────────────────────────
        if (!string.Equals(dto.TipoOrigen, dto.TipoDestino, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"No se permite cross-phase: origen={dto.TipoOrigen} no coincide con destino={dto.TipoDestino}. " +
                "Sólo se puede trasladar dentro de la misma etapa (Levante→Levante o Producción→Producción).");
        }

        await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
        try
        {
            var companyId = await GetEffectiveCompanyIdAsync(ct);
            var fechaDate = dto.FechaSeguimiento.Date;
            var fechaUtc  = DateTime.UtcNow;

            int? granjaDestinoIdOut = dto.GranjaDestinoId;

            if (dto.TipoOrigen.Equals("Levante", StringComparison.OrdinalIgnoreCase))
            {
                // ── 1a. Cargar LPL origen ────────────────────────────────
                var lplOrigen = await _ctx.LotePosturaLevante
                    .Where(l => l.LotePosturaLevanteId == dto.LoteOrigenId
                             && l.CompanyId == companyId
                             && l.DeletedAt == null)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException("Lote levante origen no encontrado.");

                if (lplOrigen.LoteId is null)
                    throw new InvalidOperationException("El lote levante origen no tiene un Lote base asignado.");

                // ── 2a. Validar stock con SALDO REAL (incluye mortalidad/sel/error + traslados) ─
                var resumenOrigen = await _loteService.GetMortalidadResumenAsync(lplOrigen.LoteId.Value)
                    ?? throw new InvalidOperationException("No se pudo calcular el saldo real del lote origen.");

                if (resumenOrigen.SaldoHembras < dto.TrasladoHembras)
                    throw new InvalidOperationException(
                        $"Stock insuficiente (real): hay {resumenOrigen.SaldoHembras} hembras vivas, " +
                        $"se intentaron trasladar {dto.TrasladoHembras}.");
                if (resumenOrigen.SaldoMachos < dto.TrasladoMachos)
                    throw new InvalidOperationException(
                        $"Stock insuficiente (real): hay {resumenOrigen.SaldoMachos} machos vivos, " +
                        $"se intentaron trasladar {dto.TrasladoMachos}.");

                // ── 3a. Cargar LPL destino ───────────────────────────────
                var lplDestino = await _ctx.LotePosturaLevante
                    .Where(l => l.LotePosturaLevanteId == dto.LoteDestinoId
                             && l.CompanyId == companyId
                             && l.DeletedAt == null)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException("Lote levante destino no encontrado.");

                if (lplDestino.LotePosturaLevanteId == lplOrigen.LotePosturaLevanteId)
                    throw new InvalidOperationException("El lote origen y destino no pueden ser el mismo.");
                if (lplDestino.LoteId is null)
                    throw new InvalidOperationException("El lote destino no tiene un Lote base asignado.");

                granjaDestinoIdOut ??= lplDestino.GranjaId;

                // ── 4a. Actualizar acumulados de traslado ─────────────────
                lplOrigen.LevanteTrasladoSalidaHembras += dto.TrasladoHembras;
                lplOrigen.LevanteTrasladoSalidaMachos  += dto.TrasladoMachos;
                lplOrigen.AvesHActual = Math.Max(0, (lplOrigen.AvesHActual ?? 0) - dto.TrasladoHembras);
                lplOrigen.AvesMActual = Math.Max(0, (lplOrigen.AvesMActual ?? 0) - dto.TrasladoMachos);

                lplDestino.LevanteTrasladoIngresoHembras += dto.TrasladoHembras;
                lplDestino.LevanteTrasladoIngresoMachos  += dto.TrasladoMachos;
                lplDestino.AvesHActual = (lplDestino.AvesHActual ?? 0) + dto.TrasladoHembras;
                lplDestino.AvesMActual = (lplDestino.AvesMActual ?? 0) + dto.TrasladoMachos;

                // ── 5a. UPSERT registro SALIDA en seguimiento_diario (lote origen) ─
                //   Si ya existe un SD manual para esa fecha+lote, lo extendemos con los
                //   campos de traslado. Si no existe, lo creamos como "puro traslado".
                //   No tocamos MortalidadHembras/Machos: el traslado vive en sus propias columnas.
                var totalAves = dto.TrasladoHembras + dto.TrasladoMachos;

                var segSalida = await _ctx.SeguimientoDiario
                    .Where(s => s.TipoSeguimiento == "levante"
                             && s.LoteId == lplOrigen.LoteId.Value.ToString()
                             && s.Fecha == fechaDate)
                    .FirstOrDefaultAsync(ct);

                if (segSalida is null)
                {
                    segSalida = new SeguimientoDiario
                    {
                        TipoSeguimiento = "levante",
                        LoteId = lplOrigen.LoteId.Value.ToString(),
                        LotePosturaLevanteId = lplOrigen.LotePosturaLevanteId,
                        Fecha = fechaDate,
                        MortalidadHembras = 0, MortalidadMachos = 0,
                        SelH = 0, SelM = 0,
                        ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                        Ciclo = "Traslado",
                        TipoAlimento = "—",
                        CreatedByUserId = _current.UserGuid?.ToString() ?? usuarioId.ToString(),
                        CreatedAt = fechaUtc
                    };
                    _ctx.SeguimientoDiario.Add(segSalida);
                }

                // Acumulamos sobre lo que hubiera (caso: varios traslados en el mismo día)
                segSalida.TrasladoSalidaHembras += dto.TrasladoHembras;
                segSalida.TrasladoSalidaMachos  += dto.TrasladoMachos;
                segSalida.TrasladoAvesSalida     = (segSalida.TrasladoAvesSalida ?? 0) + totalAves;
                segSalida.EsTraslado             = true;
                segSalida.TrasladoDireccion      = "SALIDA";
                segSalida.TrasladoLoteContraparteId   = lplDestino.LotePosturaLevanteId;
                segSalida.TrasladoGranjaContraparteId = lplDestino.GranjaId;
                segSalida.Observaciones = string.IsNullOrWhiteSpace(segSalida.Observaciones)
                    ? $"Traslado SALIDA → {lplDestino.LoteNombre}. {dto.Observaciones ?? ""}".Trim()
                    : $"{segSalida.Observaciones} | Traslado SALIDA → {lplDestino.LoteNombre}";
                segSalida.UpdatedAt = fechaUtc;

                // ── 6a. UPSERT registro INGRESO en seguimiento_diario (lote destino) ─
                var segIngreso = await _ctx.SeguimientoDiario
                    .Where(s => s.TipoSeguimiento == "levante"
                             && s.LoteId == lplDestino.LoteId.Value.ToString()
                             && s.Fecha == fechaDate)
                    .FirstOrDefaultAsync(ct);

                if (segIngreso is null)
                {
                    segIngreso = new SeguimientoDiario
                    {
                        TipoSeguimiento = "levante",
                        LoteId = lplDestino.LoteId.Value.ToString(),
                        LotePosturaLevanteId = lplDestino.LotePosturaLevanteId,
                        Fecha = fechaDate,
                        MortalidadHembras = 0, MortalidadMachos = 0,
                        SelH = 0, SelM = 0,
                        ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                        Ciclo = "Traslado",
                        TipoAlimento = "—",
                        CreatedByUserId = _current.UserGuid?.ToString() ?? usuarioId.ToString(),
                        CreatedAt = fechaUtc
                    };
                    _ctx.SeguimientoDiario.Add(segIngreso);
                }

                segIngreso.TrasladoIngresoHembras += dto.TrasladoHembras;
                segIngreso.TrasladoIngresoMachos  += dto.TrasladoMachos;
                segIngreso.TrasladoAvesEntrante    = (segIngreso.TrasladoAvesEntrante ?? 0) + totalAves;
                segIngreso.EsTraslado              = true;
                segIngreso.TrasladoDireccion       = "INGRESO";
                segIngreso.TrasladoLoteContraparteId   = lplOrigen.LotePosturaLevanteId;
                segIngreso.TrasladoGranjaContraparteId = lplOrigen.GranjaId;
                segIngreso.Observaciones = string.IsNullOrWhiteSpace(segIngreso.Observaciones)
                    ? $"Traslado INGRESO ← {lplOrigen.LoteNombre}. {dto.Observaciones ?? ""}".Trim()
                    : $"{segIngreso.Observaciones} | Traslado INGRESO ← {lplOrigen.LoteNombre}";
                segIngreso.UpdatedAt = fechaUtc;
            }
            else
            {
                // ════════════════════════════════════════════════════════════════
                // PRODUCCIÓN ↔ PRODUCCIÓN (Feature 14, paridad con Levante)
                // ════════════════════════════════════════════════════════════════
                var lppOrigen = await _ctx.LotePosturaProduccion
                    .Where(l => l.LotePosturaProduccionId == dto.LoteOrigenId
                             && l.CompanyId == companyId
                             && l.DeletedAt == null)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException("Lote producción origen no encontrado.");

                if (lppOrigen.LoteId is null)
                    throw new InvalidOperationException("El lote producción origen no tiene un Lote base asignado.");
                if ((lppOrigen.AvesHActual ?? 0) < dto.TrasladoHembras)
                    throw new InvalidOperationException($"Stock insuficiente: solo hay {lppOrigen.AvesHActual ?? 0} hembras disponibles.");
                if ((lppOrigen.AvesMActual ?? 0) < dto.TrasladoMachos)
                    throw new InvalidOperationException($"Stock insuficiente de machos: solo hay {lppOrigen.AvesMActual ?? 0} disponibles.");

                var lppDestino = await _ctx.LotePosturaProduccion
                    .Where(l => l.LotePosturaProduccionId == dto.LoteDestinoId
                             && l.CompanyId == companyId
                             && l.DeletedAt == null)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException("Lote producción destino no encontrado.");

                if (lppDestino.LoteId is null)
                    throw new InvalidOperationException("El lote producción destino no tiene un Lote base asignado.");

                // Acumulados en LPP origen + decremento de aves actuales
                lppOrigen.ProduccionTrasladoSalidaHembras += dto.TrasladoHembras;
                lppOrigen.ProduccionTrasladoSalidaMachos  += dto.TrasladoMachos;
                lppOrigen.AvesHActual = (lppOrigen.AvesHActual ?? 0) - dto.TrasladoHembras;
                lppOrigen.AvesMActual = (lppOrigen.AvesMActual ?? 0) - dto.TrasladoMachos;

                // Acumulados en LPP destino + incremento de aves actuales
                lppDestino.ProduccionTrasladoIngresoHembras += dto.TrasladoHembras;
                lppDestino.ProduccionTrasladoIngresoMachos  += dto.TrasladoMachos;
                lppDestino.AvesHActual = (lppDestino.AvesHActual ?? 0) + dto.TrasladoHembras;
                lppDestino.AvesMActual = (lppDestino.AvesMActual ?? 0) + dto.TrasladoMachos;
                granjaDestinoIdOut ??= lppDestino.GranjaId;

                var totalAvesP = dto.TrasladoHembras + dto.TrasladoMachos;
                int createdByIdP = usuarioId; // AuditableEntity.CreatedByUserId es int

                // ── UPSERT registro SALIDA en la canónica de producción (lote origen) ──
                var segSalidaP = await _ctx.SeguimientoProduccion
                    .Where(s => s.LoteId == lppOrigen.LoteId!.Value && s.Fecha == fechaDate)
                    .FirstOrDefaultAsync(ct);

                if (segSalidaP is null)
                {
                    segSalidaP = new SeguimientoProduccion
                    {
                        LoteId = lppOrigen.LoteId!.Value,
                        Fecha = fechaDate,
                        MortalidadH = 0, MortalidadM = 0,
                        SelH = 0, SelM = 0,
                        ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                        TipoAlimento = "—",
                        CompanyId = companyId,
                        CreatedByUserId = createdByIdP,
                        CreatedAt = fechaUtc
                    };
                    _ctx.SeguimientoProduccion.Add(segSalidaP);
                }

                segSalidaP.TrasladoSalidaHembras += dto.TrasladoHembras;
                segSalidaP.TrasladoSalidaMachos  += dto.TrasladoMachos;
                segSalidaP.TrasladoHembras       = (segSalidaP.TrasladoHembras ?? 0) + dto.TrasladoHembras;
                segSalidaP.TrasladoMachos        = (segSalidaP.TrasladoMachos  ?? 0) + dto.TrasladoMachos;
                segSalidaP.LoteDestinoId         = lppDestino.LotePosturaProduccionId;
                segSalidaP.GranjaDestinoId       = lppDestino.GranjaId;
                segSalidaP.FechaTraslado         = fechaDate;
                segSalidaP.EsTraslado            = true;
                segSalidaP.TrasladoDireccion     = "SALIDA";
                segSalidaP.TrasladoLoteContraparteId   = lppDestino.LotePosturaProduccionId;
                segSalidaP.TrasladoGranjaContraparteId = lppDestino.GranjaId;
                segSalidaP.TrasladoObservaciones = string.IsNullOrWhiteSpace(segSalidaP.TrasladoObservaciones)
                    ? $"Traslado SALIDA → {lppDestino.LoteNombre}. {dto.Observaciones ?? ""}".Trim()
                    : $"{segSalidaP.TrasladoObservaciones} | Traslado SALIDA → {lppDestino.LoteNombre}";
                segSalidaP.UpdatedAt = fechaUtc;
                segSalidaP.UpdatedByUserId = createdByIdP;

                // ── UPSERT registro INGRESO en la canónica de producción (lote destino) ──
                var segIngresoP = await _ctx.SeguimientoProduccion
                    .Where(s => s.LoteId == lppDestino.LoteId!.Value && s.Fecha == fechaDate)
                    .FirstOrDefaultAsync(ct);

                if (segIngresoP is null)
                {
                    segIngresoP = new SeguimientoProduccion
                    {
                        LoteId = lppDestino.LoteId!.Value,
                        Fecha = fechaDate,
                        MortalidadH = 0, MortalidadM = 0,
                        SelH = 0, SelM = 0,
                        ErrorSexajeHembras = 0, ErrorSexajeMachos = 0,
                        TipoAlimento = "—",
                        CompanyId = companyId,
                        CreatedByUserId = createdByIdP,
                        CreatedAt = fechaUtc
                    };
                    _ctx.SeguimientoProduccion.Add(segIngresoP);
                }

                segIngresoP.TrasladoIngresoHembras += dto.TrasladoHembras;
                segIngresoP.TrasladoIngresoMachos  += dto.TrasladoMachos;
                segIngresoP.EsTraslado              = true;
                segIngresoP.TrasladoDireccion       = "INGRESO";
                segIngresoP.TrasladoLoteContraparteId   = lppOrigen.LotePosturaProduccionId;
                segIngresoP.TrasladoGranjaContraparteId = lppOrigen.GranjaId;
                segIngresoP.TrasladoObservaciones = string.IsNullOrWhiteSpace(segIngresoP.TrasladoObservaciones)
                    ? $"Traslado INGRESO ← {lppOrigen.LoteNombre}. {dto.Observaciones ?? ""}".Trim()
                    : $"{segIngresoP.TrasladoObservaciones} | Traslado INGRESO ← {lppOrigen.LoteNombre}";
                segIngresoP.UpdatedAt = fechaUtc;
                segIngresoP.UpdatedByUserId = createdByIdP;
            }

            // ── 7. Auditoría — MovimientoAves ─────────────────────────────
            var movimiento = new MovimientoAves
            {
                NumeroMovimiento = $"TSD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
                FechaMovimiento = dto.FechaSeguimiento,
                TipoMovimiento = "Traslado",
                CantidadHembras = dto.TrasladoHembras,
                CantidadMachos = dto.TrasladoMachos,
                CantidadMixtas = 0,
                Estado = "Completado",
                FechaProcesamiento = DateTime.UtcNow,
                Observaciones = dto.Observaciones,
                UsuarioMovimientoId = usuarioId,
                GranjaDestinoId = granjaDestinoIdOut
            };

            // Asociar origen al movimiento
            if (dto.TipoOrigen.Equals("Levante", StringComparison.OrdinalIgnoreCase))
            {
                var lplRef = await _ctx.LotePosturaLevante
                    .AsNoTracking()
                    .Where(l => l.LotePosturaLevanteId == dto.LoteOrigenId)
                    .Select(l => new { l.LoteId, l.GranjaId })
                    .FirstOrDefaultAsync(ct);
                movimiento.LoteOrigenId = lplRef?.LoteId;
                movimiento.GranjaOrigenId = lplRef?.GranjaId;
            }
            else
            {
                var lppRef = await _ctx.LotePosturaProduccion
                    .AsNoTracking()
                    .Where(l => l.LotePosturaProduccionId == dto.LoteOrigenId)
                    .Select(l => new { l.LoteId, l.GranjaId })
                    .FirstOrDefaultAsync(ct);
                movimiento.LoteOrigenId = lppRef?.LoteId;
                movimiento.GranjaOrigenId = lppRef?.GranjaId;
            }

            _ctx.MovimientoAves.Add(movimiento);
            await _ctx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // ── 8. Leer saldo final REAL del origen para devolver ────────
            int avesHFinal = 0, avesMFinal = 0;
            if (dto.TipoOrigen.Equals("Levante", StringComparison.OrdinalIgnoreCase))
            {
                var lpl = await _ctx.LotePosturaLevante.AsNoTracking()
                    .Where(l => l.LotePosturaLevanteId == dto.LoteOrigenId).FirstOrDefaultAsync(ct);
                if (lpl?.LoteId is int loteIdF)
                {
                    var res = await _loteService.GetMortalidadResumenAsync(loteIdF);
                    if (res != null)
                    {
                        avesHFinal = res.SaldoHembras;
                        avesMFinal = res.SaldoMachos;
                    }
                    else
                    {
                        avesHFinal = lpl.AvesHActual ?? 0;
                        avesMFinal = lpl.AvesMActual ?? 0;
                    }
                }
            }
            else
            {
                var lpp = await _ctx.LotePosturaProduccion.AsNoTracking()
                    .Where(l => l.LotePosturaProduccionId == dto.LoteOrigenId).FirstOrDefaultAsync(ct);
                avesHFinal = lpp?.AvesHActual ?? 0;
                avesMFinal = lpp?.AvesMActual ?? 0;
            }

            return new TrasladoAvesResultDto(
                Exitoso: true,
                Mensaje: "Traslado ejecutado correctamente.",
                MovimientoAvesId: movimiento.Id,
                AvesHActualOrigen: avesHFinal,
                AvesMActualOrigen: avesMFinal
            );
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
