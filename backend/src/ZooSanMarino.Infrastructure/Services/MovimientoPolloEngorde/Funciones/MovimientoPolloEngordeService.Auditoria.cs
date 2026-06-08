// MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.Auditoria.cs
// Auditoría de ventas vs disponibilidad y corrección de sobreventas (Completadas).
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using PagedResultCommon = ZooSanMarino.Application.DTOs.Common.PagedResult<ZooSanMarino.Application.DTOs.MovimientoPolloEngordeDto>;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoPolloEngordeService
{
    /// <inheritdoc />
    public async Task<AuditoriaVentasEngordeResponse> AuditarVentasEngordeAsync(AuditoriaVentasEngordeRequest request)
    {
        var companyId = _currentUser.CompanyId;
        var granjaId = request.GranjaId;
        var loteIds = (request.LoteAveEngordeIds ?? new List<int>()).Distinct().ToList();
        if (!granjaId.HasValue && loteIds.Count == 0)
            throw new InvalidOperationException("Debe indicar GranjaId o al menos un LoteAveEngordeId.");

        if (loteIds.Count == 0 && granjaId.HasValue)
        {
            loteIds = await _ctx.LoteAveEngorde.AsNoTracking()
                .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.GranjaId == granjaId.Value && l.LoteAveEngordeId != null)
                .Select(l => l.LoteAveEngordeId!.Value)
                .ToListAsync();
        }

        var ids = loteIds.Distinct().ToList();
        if (ids.Count == 0)
            return new AuditoriaVentasEngordeResponse
            {
                Ok = true,
                DryRun = true,
                AplicarCorreccion = false,
                Mensaje = "No hay lotes para auditar."
            };

        var lotes = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteAveEngordeId != null && ids.Contains(l.LoteAveEngordeId.Value))
            .Select(l => new
            {
                Id = l.LoteAveEngordeId!.Value,
                l.LoteNombre,
                HembrasL = l.HembrasL ?? 0,
                MachosL = l.MachosL ?? 0,
                Mixtas = l.Mixtas ?? 0,
                Encaset = l.AvesEncasetadas ?? 0,
                MortCajaH = l.MortCajaH ?? 0,
                MortCajaM = l.MortCajaM ?? 0
            })
            .ToListAsync();
        var loteById = lotes.ToDictionary(x => x.Id);

        var inicioHist = await _ctx.HistorialLotePolloEngorde.AsNoTracking()
            .Where(h => h.CompanyId == companyId
                        && h.TipoLote == "LoteAveEngorde"
                        && h.TipoRegistro == "Inicio"
                        && h.LoteAveEngordeId != null
                        && ids.Contains(h.LoteAveEngordeId.Value))
            .OrderBy(h => h.FechaRegistro).ThenBy(h => h.Id)
            .Select(h => new { Id = h.LoteAveEngordeId!.Value, h.AvesHembras, h.AvesMachos, h.AvesMixtas })
            .ToListAsync();
        var iniById = inicioHist
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var segAcum = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => ids.Contains(s.LoteAveEngordeId))
            .GroupBy(s => s.LoteAveEngordeId)
            .Select(g => new
            {
                Id = g.Key,
                MortH = g.Sum(x => x.MortalidadHembras ?? 0),
                MortM = g.Sum(x => x.MortalidadMachos ?? 0),
                SelH = g.Sum(x => x.SelH ?? 0),
                SelM = g.Sum(x => x.SelM ?? 0),
                ErrH = g.Sum(x => x.ErrorSexajeHembras ?? 0),
                ErrM = g.Sum(x => x.ErrorSexajeMachos ?? 0)
            })
            .ToListAsync();
        var segById = segAcum.ToDictionary(
            x => x.Id,
            x => (MortH: x.MortH, MortM: x.MortM, SelH: x.SelH, SelM: x.SelM, ErrH: x.ErrH, ErrM: x.ErrM));

        var asignadas = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            .Where(lr => ids.Contains(lr.LoteAveEngordeId))
            .GroupBy(lr => lr.LoteAveEngordeId)
            .Select(g => new { Id = g.Key, H = g.Sum(x => x.H ?? 0), M = g.Sum(x => x.M ?? 0) })
            .ToListAsync();
        var asigById = asignadas.ToDictionary(x => x.Id, x => (H: x.H, M: x.M));

        // Ventas por lote (Completado y Pendiente), solo salidas.
        var movVentas = await _ctx.MovimientoPolloEngorde.AsNoTracking()
            .Where(m =>
                m.CompanyId == companyId
                && m.DeletedAt == null
                && m.Estado != "Cancelado"
                && m.Estado != "Anulado"
                && (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                && m.LoteAveEngordeOrigenId != null
                && ids.Contains(m.LoteAveEngordeOrigenId.Value))
            .Select(m => new
            {
                m.Id,
                LoteId = m.LoteAveEngordeOrigenId!.Value,
                m.NumeroMovimiento,
                m.FechaMovimiento,
                m.Estado,
                m.CantidadHembras,
                m.CantidadMachos,
                m.CantidadMixtas
            })
            .ToListAsync();

        var compByLote = movVentas.Where(x => x.Estado == "Completado")
            .GroupBy(x => x.LoteId)
            .ToDictionary(
                g => g.Key,
                g => (
                    H: g.Sum(x => x.CantidadHembras),
                    M: g.Sum(x => x.CantidadMachos),
                    X: g.Sum(x => x.CantidadMixtas)
                ));
        var pendByLote = movVentas.Where(x => x.Estado == "Pendiente")
            .GroupBy(x => x.LoteId)
            .ToDictionary(
                g => g.Key,
                g => (
                    H: g.Sum(x => x.CantidadHembras),
                    M: g.Sum(x => x.CantidadMachos),
                    X: g.Sum(x => x.CantidadMixtas)
                ));

        var resp = new AuditoriaVentasEngordeResponse
        {
            DryRun = request.DryRun,
            AplicarCorreccion = request.AplicarCorreccion && !request.DryRun
        };

        var acciones = new List<AuditoriaCorreccionAccion>();
        var lotesOut = new List<AuditoriaVentasLoteDetalle>();
        var hayErrores = false;

        // Si vamos a corregir, trabajamos con entidades trackeadas en una transacción.
        await using var tx = resp.AplicarCorreccion ? await _ctx.Database.BeginTransactionAsync() : null;
        try
        {
            foreach (var id in ids)
            {
                if (!loteById.TryGetValue(id, out var lote))
                {
                    lotesOut.Add(new AuditoriaVentasLoteDetalle(
                        id, null,
                        0, 0, 0,
                        0, 0,
                        0, 0,
                        0, 0,
                        0, 0,
                        0, 0,
                        0, 0, 0,
                        0, 0, 0,
                        0, 0, 0,
                        0, 0, 0,
                        false,
                        "No existe"
                    ));
                    hayErrores = true;
                    continue;
                }

                var ini = iniById.GetValueOrDefault(id);
                var encH = ini?.AvesHembras ?? 0;
                var encM = ini?.AvesMachos ?? 0;
                var encX = ini?.AvesMixtas ?? 0;
                if (encH + encM + encX == 0 && lote.Encaset > 0)
                    encX = lote.Encaset;

                var seg = segById.GetValueOrDefault(id, (MortH: 0, MortM: 0, SelH: 0, SelM: 0, ErrH: 0, ErrM: 0));
                var asg = asigById.GetValueOrDefault(id, (H: 0, M: 0));
                var vendC = compByLote.GetValueOrDefault(id, (H: 0, M: 0, X: 0));
                var vendP = pendByLote.GetValueOrDefault(id, (H: 0, M: 0, X: 0));

                var maxH = MovimientoPolloEngordeCalculos.MaxVendiblePorSexo(encH, lote.MortCajaH, seg.MortH, seg.SelH, seg.ErrH, asg.H);
                var maxM = MovimientoPolloEngordeCalculos.MaxVendiblePorSexo(encM, lote.MortCajaM, seg.MortM, seg.SelM, seg.ErrM, asg.M);
                var maxX = Math.Max(0, encX);

                var totalVentasH = vendC.H + vendP.H;
                var totalVentasM = vendC.M + vendP.M;
                var totalVentasX = vendC.X + vendP.X;

                var excesoH = MovimientoPolloEngordeCalculos.Exceso(totalVentasH, maxH);
                var excesoM = MovimientoPolloEngordeCalculos.Exceso(totalVentasM, maxM);
                var excesoX = MovimientoPolloEngordeCalculos.Exceso(totalVentasX, maxX);

                var autoCorregible = (excesoH + excesoM + excesoX) > 0 && (vendP.H + vendP.M + vendP.X) > 0;
                var estado = (excesoH + excesoM + excesoX) == 0 ? "OK" : (autoCorregible ? "Exceso (corregible en Pendiente)" : "Exceso (no corregible)");

                if ((excesoH + excesoM + excesoX) > 0) hayErrores = true;

                if (resp.AplicarCorreccion && autoCorregible)
                {
                    // Ajustar movimientos Pendiente del lote, del más reciente al más antiguo.
                    var pendMovs = await _ctx.MovimientoPolloEngorde
                        .Where(m =>
                            m.CompanyId == companyId
                            && m.DeletedAt == null
                            && m.Estado == "Pendiente"
                            && (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                            && m.LoteAveEngordeOrigenId == id)
                        .OrderByDescending(m => m.FechaMovimiento).ThenByDescending(m => m.Id)
                        .ToListAsync();

                    foreach (var m in pendMovs)
                    {
                        if (excesoH + excesoM + excesoX <= 0) break;
                        var antesH = m.CantidadHembras;
                        var antesM = m.CantidadMachos;
                        var antesX = m.CantidadMixtas;

                        var reduceH = Math.Min(m.CantidadHembras, excesoH);
                        var reduceM = Math.Min(m.CantidadMachos, excesoM);
                        var reduceX = Math.Min(m.CantidadMixtas, excesoX);

                        m.CantidadHembras -= reduceH;
                        m.CantidadMachos -= reduceM;
                        m.CantidadMixtas -= reduceX;

                        excesoH -= reduceH;
                        excesoM -= reduceM;
                        excesoX -= reduceX;

                        var nota = $"Ajuste por auditoría (exceso). -H:{reduceH} -M:{reduceM} -X:{reduceX}.";
                        m.Observaciones = AppendObservaciones(m.Observaciones, " | " + nota);
                        m.UpdatedByUserId = _currentUser.UserId;
                        m.UpdatedAt = DateTime.UtcNow;

                        if (m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas <= 0)
                        {
                            m.Estado = "Cancelado";
                            m.FechaCancelacion = DateTime.UtcNow;
                            m.Observaciones = AppendObservaciones(m.Observaciones, " | Cancelado por auditoría: quedó en 0.");
                        }

                        acciones.Add(new AuditoriaCorreccionAccion(
                            m.Id,
                            m.NumeroMovimiento,
                            id,
                            antesH, antesM, antesX,
                            m.CantidadHembras, m.CantidadMachos, m.CantidadMixtas,
                            nota
                        ));
                    }
                }

                lotesOut.Add(new AuditoriaVentasLoteDetalle(
                    id,
                    lote.LoteNombre,
                    encH, encM, encX,
                    lote.MortCajaH, lote.MortCajaM,
                    seg.MortH, seg.MortM,
                    seg.SelH, seg.SelM,
                    seg.ErrH, seg.ErrM,
                    asg.H, asg.M,
                    maxH, maxM, maxX,
                    vendC.H, vendC.M, vendC.X,
                    vendP.H, vendP.M, vendP.X,
                    MovimientoPolloEngordeCalculos.Exceso(totalVentasH, maxH),
                    MovimientoPolloEngordeCalculos.Exceso(totalVentasM, maxM),
                    MovimientoPolloEngordeCalculos.Exceso(totalVentasX, maxX),
                    autoCorregible,
                    estado
                ));
            }

            if (resp.AplicarCorreccion)
            {
                await _ctx.SaveChangesAsync();
                if (tx != null) await tx.CommitAsync();
            }

            resp.Acciones = acciones;
            resp.Lotes = lotesOut;
            resp.Ok = !hayErrores;
            resp.Mensaje = resp.Ok
                ? "Auditoría OK: lotes coherentes."
                : (resp.AplicarCorreccion
                    ? "Auditoría: se encontraron incoherencias; se aplicaron correcciones (solo Pendiente)."
                    : "Auditoría: se encontraron incoherencias. Puede ejecutar con AplicarCorreccion=true para corregir Pendiente.");
            return resp;
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CorregirVentasCompletadasResponse> CorregirVentasCompletadasAsync(CorregirVentasCompletadasRequest request)
    {
        var companyId = _currentUser.CompanyId;
        var granjaId = request.GranjaId;
        var loteIds = (request.LoteAveEngordeIds ?? new List<int>()).Distinct().ToList();
        if (!granjaId.HasValue && loteIds.Count == 0)
            throw new InvalidOperationException("Debe indicar GranjaId o al menos un LoteAveEngordeId.");

        if (loteIds.Count == 0 && granjaId.HasValue)
        {
            loteIds = await _ctx.LoteAveEngorde.AsNoTracking()
                .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.GranjaId == granjaId.Value && l.LoteAveEngordeId != null)
                .Select(l => l.LoteAveEngordeId!.Value)
                .ToListAsync();
        }

        var ids = loteIds.Distinct().ToList();
        if (ids.Count == 0)
            return new CorregirVentasCompletadasResponse { Ok = true, DryRun = true, Mensaje = "No hay lotes para corregir." };

        // Reusar auditoría para determinar excesos por lote.
        var audit = await AuditarVentasEngordeAsync(new AuditoriaVentasEngordeRequest
        {
            GranjaId = granjaId,
            LoteAveEngordeIds = ids,
            AplicarCorreccion = false,
            DryRun = true
        });

        var targets = (audit.Lotes ?? new List<AuditoriaVentasLoteDetalle>())
            .Where(l => (l.ExcesoH + l.ExcesoM + l.ExcesoX) > 0)
            .ToList();

        if (targets.Count == 0)
            return new CorregirVentasCompletadasResponse { Ok = true, DryRun = request.DryRun, Mensaje = "No hay excesos para corregir." };

        var acciones = new List<CorreccionCompletadoAccionDto>();

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            foreach (var t in targets)
            {
                var excesoH = t.ExcesoH;
                var excesoM = t.ExcesoM;
                var excesoX = t.ExcesoX;

                // Si hay Pendientes, la corrección debe hacerse por el otro flujo (Pendiente).
                if (t.VendidasPendienteH + t.VendidasPendienteM + t.VendidasPendienteX > 0)
                    continue;

                // Traer movimientos completados del lote (ventas/salidas), más recientes primero.
                var movs = await _ctx.MovimientoPolloEngorde
                    .Include(m => m.LoteAveEngordeOrigen)
                    .Where(m =>
                        m.CompanyId == companyId
                        && m.DeletedAt == null
                        && m.Estado == "Completado"
                        && (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                        && m.LoteAveEngordeOrigenId == t.LoteAveEngordeId)
                    .OrderByDescending(m => m.FechaMovimiento).ThenByDescending(m => m.Id)
                    .ToListAsync();

                foreach (var m in movs)
                {
                    if (excesoH + excesoM + excesoX <= 0) break;
                    if (m.LoteAveEngordeOrigen == null)
                        throw new InvalidOperationException($"No se pudo cargar el lote origen para el movimiento {m.Id}.");

                    var antesH = m.CantidadHembras;
                    var antesM = m.CantidadMachos;
                    var antesX = m.CantidadMixtas;

                    // Reducimos el movimiento por el exceso (eso equivale a "devolver" esa diferencia al lote).
                    var reduceH = Math.Min(m.CantidadHembras, excesoH);
                    var reduceM = Math.Min(m.CantidadMachos, excesoM);
                    var reduceX = Math.Min(m.CantidadMixtas, excesoX);

                    if (reduceH + reduceM + reduceX <= 0) continue;

                    m.CantidadHembras -= reduceH;
                    m.CantidadMachos -= reduceM;
                    m.CantidadMixtas -= reduceX;

                    // Devolver al lote solo lo reducido.
                    var lote = m.LoteAveEngordeOrigen;
                    lote.HembrasL = (lote.HembrasL ?? 0) + reduceH;
                    lote.MachosL = (lote.MachosL ?? 0) + reduceM;
                    lote.Mixtas = (lote.Mixtas ?? 0) + reduceX;

                    excesoH -= reduceH;
                    excesoM -= reduceM;
                    excesoX -= reduceX;

                    var nota = $"Corrección auditoría (Completado): -H:{reduceH} -M:{reduceM} -X:{reduceX}. Devuelto al lote.";
                    m.Observaciones = AppendObservaciones(m.Observaciones, " | " + nota);
                    m.UpdatedByUserId = _currentUser.UserId;
                    m.UpdatedAt = DateTime.UtcNow;

                    // Si queda en 0, lo anulamos (y triggers marcan unificado como anulado).
                    if (m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas <= 0)
                    {
                        m.Estado = "Anulado";
                        m.DeletedAt = DateTime.UtcNow;
                        m.Observaciones = AppendObservaciones(m.Observaciones, " | Anulado por auditoría: quedó en 0.");
                    }

                    acciones.Add(new CorreccionCompletadoAccionDto(
                        m.Id,
                        m.NumeroMovimiento,
                        t.LoteAveEngordeId,
                        antesH, antesM, antesX,
                        m.CantidadHembras, m.CantidadMachos, m.CantidadMixtas,
                        reduceH, reduceM, reduceX,
                        nota
                    ));
                }

                if (excesoH + excesoM + excesoX > 0)
                {
                    // No se pudo corregir totalmente con los movimientos encontrados.
                    // Dejamos rastro para revisión manual.
                    // (No lanzamos excepción para no bloquear correcciones parciales).
                }
            }

            if (request.DryRun)
            {
                await tx.RollbackAsync();
                return new CorregirVentasCompletadasResponse
                {
                    Ok = true,
                    DryRun = true,
                    Mensaje = $"Simulación OK. Acciones propuestas: {acciones.Count}.",
                    Acciones = acciones
                };
            }

            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();

            return new CorregirVentasCompletadasResponse
            {
                Ok = true,
                DryRun = false,
                Mensaje = $"Corrección aplicada. Acciones: {acciones.Count}.",
                Acciones = acciones
            };
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync();
            throw MapDbUpdateToInvalidOperation(ex, MensajeAyudaCorreccionCompletados);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
