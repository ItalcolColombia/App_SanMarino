// src/ZooSanMarino.Infrastructure/Services/CorreccionAvesDisponiblesEngordeService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Diagnóstico y corrección de descuadres de saldos de aves en lotes pollo engorde:
/// (1) ventas Pendientes vencidas sin confirmar (constan en histórico/tabla diaria pero no
///     descontaron el maestro) → se confirman vía el flujo real de la app (CompleteAsync);
/// (2) ventas Completadas que nunca descontaron hembras_l/machos_l (bug histórico de
///     escritura) → re-sync del maestro solo con evidencia exacta;
/// (3) lotes Cerrados con disponibles fantasma → disponibles a 0 (regla 2601).
/// Planes: correccion_aves_disponibles_engorde_2601_plan.md · correccion_saldos_engorde_2602_global_plan.md
/// </summary>
public class CorreccionAvesDisponiblesEngordeService : ICorreccionAvesDisponiblesEngordeService
{
    private const string EstadoCerrado = "Cerrado";
    private const string EstadoPendiente = "Pendiente";
    private const string EstadoCompletado = "Completado";
    private const string TipoEventoVentaAves = "VENTA_AVES";
    private static readonly string[] TiposVenta = { "Venta", "Despacho", "Retiro" };

    /// <summary>
    /// Auditoría de descuento por aves fantasma (nunca descargadas): SÍ participa en la
    /// conservación (esperado = iniciales − ventas − ajustes fantasma).
    /// </summary>
    private const string TipoRegistroAjusteFantasma = "Ajuste";
    /// <summary>
    /// Auditoría de re-sync por ventas Completadas que no descontaron: SUSTITUYE el descuento
    /// de esas ventas, por lo que NO se resta en la conservación (evita re-aplicarse).
    /// </summary>
    private const string TipoRegistroAjusteResync = "AjusteResync";

    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly ILoteReproductoraAveEngordeService _avesDisponiblesSvc;
    private readonly IMovimientoPolloEngordeService _movimientoSvc;

    public CorreccionAvesDisponiblesEngordeService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        ILoteReproductoraAveEngordeService avesDisponiblesSvc,
        IMovimientoPolloEngordeService movimientoSvc)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _avesDisponiblesSvc = avesDisponiblesSvc;
        _movimientoSvc = movimientoSvc;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Carga de datos por lote
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record LoteBase(
        int Id, string? LoteNombre, int GranjaId, string? GalponId, string EstadoOperativoLote,
        DateTime? LiquidadoAt, int HembrasL, int MachosL, int Mixtas, int Encaset);

    /// <summary>Venta completada con cantidades efectivas sobre el maestro (Panamá: mixtas viven en H/M → efX=0).</summary>
    private sealed record VentaCompletada(DateTime Fecha, int Id, int H, int M, int X);

    private sealed record EstadoLote(
        LoteBase Lote,
        int IniH, int IniM, int IniX, bool HistorialConfiable,
        int BajasH, int BajasM, DateTime? UltimoSeg,
        int VenHistH, int VenHistM, int VenHistX, DateTime? UltimaVenta, int VentasPostSeg,
        int PendH, int PendM, int PendX, List<int> PendVencidosIds, int PendVencidosH, int PendVencidosM,
        List<VentaCompletada> VentasCompletadas, int AjusteAudH, int AjusteAudM, int AjusteAudX);

    private async Task<List<LoteBase>> LoadLotesAsync(int companyId, string? loteNombre, CancellationToken ct)
    {
        var nombre = loteNombre?.Trim();
        return await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null
                     && (string.IsNullOrEmpty(nombre) || l.LoteNombre == nombre))
            .OrderBy(l => l.LoteAveEngordeId)
            .Select(l => new LoteBase(
                l.LoteAveEngordeId!.Value, l.LoteNombre, l.GranjaId, l.GalponId, l.EstadoOperativoLote,
                l.LiquidadoAt, l.HembrasL ?? 0, l.MachosL ?? 0, l.Mixtas ?? 0, l.AvesEncasetadas ?? 0))
            .ToListAsync(ct);
    }

    private async Task<EstadoLote> LoadEstadoLoteAsync(LoteBase lote, CancellationToken ct)
    {
        var seg = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == lote.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                BajasH = g.Sum(s => (s.MortalidadHembras ?? 0) + (s.SelH ?? 0) + (s.ErrorSexajeHembras ?? 0)),
                BajasM = g.Sum(s => (s.MortalidadMachos ?? 0) + (s.SelM ?? 0) + (s.ErrorSexajeMachos ?? 0)),
                UltimoSeg = (DateTime?)g.Max(s => s.Fecha)
            })
            .SingleOrDefaultAsync(ct);

        var ven = await _ctx.LoteRegistroHistoricoUnificados.AsNoTracking()
            .Where(h => h.LoteAveEngordeId == lote.Id && h.TipoEvento == TipoEventoVentaAves && !h.Anulado)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                H = g.Sum(x => x.CantidadHembras ?? 0),
                M = g.Sum(x => x.CantidadMachos ?? 0),
                X = g.Sum(x => x.CantidadMixtas ?? 0),
                UltimaVenta = (DateTime?)g.Max(x => x.FechaOperacion)
            })
            .SingleOrDefaultAsync(ct);

        int ventasPostSeg = 0;
        if (seg?.UltimoSeg != null && ven?.UltimaVenta != null && ven.UltimaVenta.Value.Date > seg.UltimoSeg.Value.Date)
        {
            var ultimoSeg = seg.UltimoSeg.Value.Date;
            ventasPostSeg = await _ctx.LoteRegistroHistoricoUnificados.AsNoTracking()
                .Where(h => h.LoteAveEngordeId == lote.Id && h.TipoEvento == TipoEventoVentaAves && !h.Anulado
                         && h.FechaOperacion.Date > ultimoSeg)
                .SumAsync(h => (h.CantidadHembras ?? 0) + (h.CantidadMachos ?? 0) + (h.CantidadMixtas ?? 0), ct);
        }

        var hoy = DateTime.UtcNow.Date;
        var pendientes = await _ctx.MovimientoPolloEngorde.AsNoTracking()
            .Where(m => m.Estado == EstadoPendiente && m.DeletedAt == null
                     && TiposVenta.Contains(m.TipoMovimiento)
                     && m.LoteAveEngordeOrigenId == lote.Id)
            .Select(m => new { m.Id, m.FechaMovimiento, m.CantidadHembras, m.CantidadMachos, m.CantidadMixtas })
            .ToListAsync(ct);
        var pendVencidos = pendientes.Where(p => p.FechaMovimiento.Date < hoy)
            .OrderBy(p => p.FechaMovimiento).ThenBy(p => p.Id).ToList();

        var ventasCompletadas = (await _ctx.MovimientoPolloEngorde.AsNoTracking()
            .Where(m => m.Estado == EstadoCompletado && m.DeletedAt == null
                     && TiposVenta.Contains(m.TipoMovimiento)
                     && m.LoteAveEngordeOrigenId == lote.Id)
            .Select(m => new { m.Id, m.FechaMovimiento, m.CantidadHembras, m.CantidadMachos, m.CantidadMixtas, m.EsVentaMixta })
            .ToListAsync(ct))
            .OrderBy(m => m.FechaMovimiento).ThenBy(m => m.Id)
            .Select(m => new VentaCompletada(m.FechaMovimiento, m.Id, m.CantidadHembras, m.CantidadMachos,
                m.EsVentaMixta ? 0 : m.CantidadMixtas))
            .ToList();

        var ini = await _ctx.HistorialLotePolloEngorde.AsNoTracking()
            .Where(h => h.LoteAveEngordeId == lote.Id && h.TipoLote == "LoteAveEngorde" && h.TipoRegistro == "Inicio")
            .OrderBy(h => h.FechaRegistro).ThenBy(h => h.Id)
            .Select(h => new { h.AvesHembras, h.AvesMachos, h.AvesMixtas })
            .FirstOrDefaultAsync(ct);

        // Solo los ajustes FANTASMA participan en la conservación; los de re-sync sustituyen
        // el descuento de ventas que no descontaron y no deben volver a restarse.
        var ajustes = await _ctx.HistorialLotePolloEngorde.AsNoTracking()
            .Where(h => h.LoteAveEngordeId == lote.Id && h.TipoLote == "LoteAveEngorde" && h.TipoRegistro == TipoRegistroAjusteFantasma)
            .GroupBy(_ => 1)
            .Select(g => new { H = g.Sum(x => x.AvesHembras), M = g.Sum(x => x.AvesMachos), X = g.Sum(x => x.AvesMixtas) })
            .SingleOrDefaultAsync(ct);

        int iniH = ini?.AvesHembras ?? 0, iniM = ini?.AvesMachos ?? 0, iniX = ini?.AvesMixtas ?? 0;
        bool historialConfiable = ini != null && lote.Encaset > 0 && (iniH + iniM + iniX) == lote.Encaset;

        return new EstadoLote(
            lote,
            iniH, iniM, iniX, historialConfiable,
            seg?.BajasH ?? 0, seg?.BajasM ?? 0, seg?.UltimoSeg,
            ven?.H ?? 0, ven?.M ?? 0, ven?.X ?? 0, ven?.UltimaVenta, ventasPostSeg,
            pendientes.Sum(p => p.CantidadHembras), pendientes.Sum(p => p.CantidadMachos), pendientes.Sum(p => p.CantidadMixtas),
            pendVencidos.Select(p => p.Id).ToList(),
            pendVencidos.Sum(p => p.CantidadHembras), pendVencidos.Sum(p => p.CantidadMachos),
            ventasCompletadas,
            ajustes?.H ?? 0, ajustes?.M ?? 0, ajustes?.X ?? 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cálculo del descuadre del maestro
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record Resync(int AjusteH, int AjusteM, bool Exacto, string? Detalle);

    /// <summary>
    /// Calcula el descuento pendiente del maestro por ventas Completadas que no descontaron.
    /// Con historial confiable: esperado = iniciales − ventasCompletadas − ajustes auditados (por género).
    /// Sin historial confiable: conservación total + walk determinista de ventas viejas→nuevas
    /// que debe igualar EXACTO el sobrante (el bug afectó a la cohorte más vieja); si no cierra → manual.
    /// Nunca propone aumentos (drift negativo ⇒ revisión manual).
    /// </summary>
    private static Resync CalcularResyncMaestro(EstadoLote e)
    {
        var l = e.Lote;
        if (l.Encaset <= 0)
            return new Resync(0, 0, true, null); // sin base de conservación (lote vaciado o sin encaset)

        var vcompH = e.VentasCompletadas.Sum(v => v.H);
        var vcompM = e.VentasCompletadas.Sum(v => v.M);
        var vcompX = e.VentasCompletadas.Sum(v => v.X);

        if (e.HistorialConfiable)
        {
            var driftH = l.HembrasL - (e.IniH - vcompH - e.AjusteAudH);
            var driftM = l.MachosL - (e.IniM - vcompM - e.AjusteAudM);
            if (driftH == 0 && driftM == 0) return new Resync(0, 0, true, null);
            if (driftH < 0 || driftM < 0)
                return new Resync(0, 0, false,
                    $"Maestro por debajo de lo esperado (driftH={driftH}, driftM={driftM}): posible sobre-descuento o movimiento no modelado.");
            return new Resync(driftH, driftM, true, null);
        }

        // Historial no confiable → conservación total
        var driftTotal = (l.HembrasL + l.MachosL + l.Mixtas)
                       - (l.Encaset - (vcompH + vcompM + vcompX) - (e.AjusteAudH + e.AjusteAudM + e.AjusteAudX));
        if (driftTotal == 0) return new Resync(0, 0, true, null);
        if (driftTotal < 0)
            return new Resync(0, 0, false,
                $"Maestro por debajo de la conservación total (drift={driftTotal}) con historial Inicio no confiable.");

        // Walk viejas→nuevas: la cohorte que no descontó debe sumar exactamente driftTotal.
        int acc = 0, accH = 0, accM = 0;
        foreach (var v in e.VentasCompletadas)
        {
            if (acc == driftTotal) break;
            acc += v.H + v.M + v.X;
            accH += v.H;
            accM += v.M;
            if (acc > driftTotal)
                return new Resync(0, 0, false,
                    $"Sobrante {driftTotal} no coincide exacto con una cohorte de ventas viejas (acumulado {acc}): revisión manual.");
        }
        if (acc != driftTotal)
            return new Resync(0, 0, false,
                $"Sobrante {driftTotal} mayor que el total de ventas completadas ({acc}): revisión manual.");
        return new Resync(accH, accM, true, null);
    }

    private static string? ResolverTipoDescuadre(EstadoLote e, Resync resync, int dispH, int dispM)
    {
        var esCerrado = string.Equals(e.Lote.EstadoOperativoLote, EstadoCerrado, StringComparison.OrdinalIgnoreCase);
        if (e.PendVencidosIds.Count > 0) return TipoDescuadreAvesEngorde.PendientesSinConfirmar;
        if (!resync.Exacto) return TipoDescuadreAvesEngorde.RevisionManual;
        if (resync.AjusteH > 0 || resync.AjusteM > 0) return TipoDescuadreAvesEngorde.MaestroNoDescontado;
        if (esCerrado && (dispH > 0 || dispM > 0)) return TipoDescuadreAvesEngorde.FantasmaCerrado;
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validación (diagnóstico, solo lectura)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ValidacionAvesDisponiblesLoteDto>> ValidarPorNombreAsync(string? loteNombre, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var lotes = await LoadLotesAsync(companyId, loteNombre, ct);
        if (lotes.Count == 0) return Array.Empty<ValidacionAvesDisponiblesLoteDto>();

        var result = new List<ValidacionAvesDisponiblesLoteDto>(lotes.Count);
        foreach (var lote in lotes)
        {
            var e = await LoadEstadoLoteAsync(lote, ct);

            var disp = await _avesDisponiblesSvc.GetAvesDisponiblesAsync(lote.Id);
            var dispH = disp?.HembrasDisponibles ?? 0;
            var dispM = disp?.MachosDisponibles ?? 0;

            var resync = CalcularResyncMaestro(e);
            var tipo = ResolverTipoDescuadre(e, resync, dispH, dispM);

            var vcompH = e.VentasCompletadas.Sum(v => v.H);
            var vcompM = e.VentasCompletadas.Sum(v => v.M);
            var vcompX = e.VentasCompletadas.Sum(v => v.X);
            var driftTotal = lote.Encaset > 0
                ? (lote.HembrasL + lote.MachosL + lote.Mixtas)
                  - (lote.Encaset - (vcompH + vcompM + vcompX) - (e.AjusteAudH + e.AjusteAudM + e.AjusteAudX))
                : 0;

            var esCerrado = string.Equals(lote.EstadoOperativoLote, EstadoCerrado, StringComparison.OrdinalIgnoreCase);
            var ajusteFantH = esCerrado ? dispH : 0;
            var ajusteFantM = esCerrado ? dispM : 0;

            result.Add(new ValidacionAvesDisponiblesLoteDto
            {
                LoteAveEngordeId = lote.Id,
                LoteNombre = lote.LoteNombre,
                GranjaId = lote.GranjaId,
                GalponId = lote.GalponId,
                EstadoOperativoLote = lote.EstadoOperativoLote,
                LiquidadoAt = lote.LiquidadoAt,
                HembrasIniciales = e.IniH,
                MachosIniciales = e.IniM,
                HistorialInicioConfiable = e.HistorialConfiable,
                HembrasL = lote.HembrasL,
                MachosL = lote.MachosL,
                BajasSeguimientoHembras = e.BajasH,
                BajasSeguimientoMachos = e.BajasM,
                VentasHembras = e.VenHistH,
                VentasMachos = e.VenHistM,
                VentasMixtas = e.VenHistX,
                VentasPendientesHembras = e.PendH,
                VentasPendientesMachos = e.PendM,
                VentasPendientesMixtas = e.PendX,
                MovimientosPendientesVencidos = e.PendVencidosIds.Count,
                VentasPendientesVencidasHembras = e.PendVencidosH,
                VentasPendientesVencidasMachos = e.PendVencidosM,
                DriftMaestroHembras = e.HistorialConfiable ? lote.HembrasL - (e.IniH - vcompH - e.AjusteAudH) : null,
                DriftMaestroMachos = e.HistorialConfiable ? lote.MachosL - (e.IniM - vcompM - e.AjusteAudM) : null,
                DriftMaestroTotal = driftTotal,
                VentasPosterioresAlUltimoSeguimiento = e.VentasPostSeg,
                FechaUltimoSeguimiento = e.UltimoSeg,
                FechaUltimaVenta = e.UltimaVenta,
                HembrasDisponibles = dispH,
                MachosDisponibles = dispM,
                GeneroSobrante = dispH > 0 && dispM > 0 ? "Ambos"
                               : dispH > 0 ? "Hembras"
                               : dispM > 0 ? "Machos"
                               : null,
                TipoDescuadre = tipo,
                RequiereCorreccion = tipo != null && tipo != TipoDescuadreAvesEngorde.RevisionManual,
                AjusteHembras = resync.AjusteH + ajusteFantH,
                AjusteMachos = resync.AjusteM + ajusteFantM
            });
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Corrección
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<CorreccionAvesDisponiblesResponse> CorregirPorNombreAsync(CorregirAvesDisponiblesRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new InvalidOperationException("Body requerido.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var validacion = await ValidarPorNombreAsync(request.LoteNombre, ct);
        var conDescuadre = validacion.Where(v => v.TipoDescuadre != null).ToList();
        var aCorregir = conDescuadre.Where(v => v.RequiereCorreccion).ToList();

        var items = new List<CorreccionAvesDisponiblesLoteDto>();
        var corregidos = 0;
        var totalConfirmados = 0;

        // Los RevisionManual siempre se reportan (nunca se tocan).
        items.AddRange(conDescuadre.Where(v => !v.RequiereCorreccion).Select(v => new CorreccionAvesDisponiblesLoteDto
        {
            LoteAveEngordeId = v.LoteAveEngordeId,
            LoteNombre = v.LoteNombre,
            GalponId = v.GalponId,
            Acciones = { TipoDescuadreAvesEngorde.RevisionManual },
            HembrasLAntes = v.HembrasL,
            MachosLAntes = v.MachosL,
            HembrasLDespues = v.HembrasL,
            MachosLDespues = v.MachosL,
            Corregido = false,
            Observacion = "Evidencia no exacta: requiere revisión manual (no se modificó)."
        }));

        if (request.DryRun)
        {
            items.AddRange(aCorregir.Select(v => new CorreccionAvesDisponiblesLoteDto
            {
                LoteAveEngordeId = v.LoteAveEngordeId,
                LoteNombre = v.LoteNombre,
                GalponId = v.GalponId,
                Acciones = BuildAcciones(v),
                MovimientosConfirmados = v.MovimientosPendientesVencidos,
                HembrasLAntes = v.HembrasL,
                MachosLAntes = v.MachosL,
                ConfirmadosHembras = v.VentasPendientesVencidasHembras,
                ConfirmadosMachos = v.VentasPendientesVencidasMachos,
                AjusteHembras = v.AjusteHembras,
                AjusteMachos = v.AjusteMachos,
                HembrasLDespues = Math.Max(0, v.HembrasL - v.VentasPendientesVencidasHembras - v.AjusteHembras),
                MachosLDespues = Math.Max(0, v.MachosL - v.VentasPendientesVencidasMachos - v.AjusteMachos),
                Corregido = false,
                Observacion = v.MovimientosPendientesVencidos > 0
                    ? $"Confirmaría {v.MovimientosPendientesVencidos} venta(s) pendiente(s) vencida(s) ({v.VentasPendientesVencidasHembras} H / {v.VentasPendientesVencidasMachos} M) y luego re-evaluaría maestro/fantasma."
                    : null
            }));
            totalConfirmados = aCorregir.Sum(v => v.MovimientosPendientesVencidos);
        }
        else if (aCorregir.Count > 0)
        {
            await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
            var ahora = DateTime.UtcNow;
            foreach (var v in aCorregir)
            {
                var lote = await _ctx.LoteAveEngorde
                    .SingleAsync(l => l.LoteAveEngordeId == v.LoteAveEngordeId && l.CompanyId == companyId && l.DeletedAt == null, ct);
                var loteBase = new LoteBase(v.LoteAveEngordeId, lote.LoteNombre, lote.GranjaId, lote.GalponId,
                    lote.EstadoOperativoLote, lote.LiquidadoAt, lote.HembrasL ?? 0, lote.MachosL ?? 0,
                    lote.Mixtas ?? 0, lote.AvesEncasetadas ?? 0);

                var hembrasAntes = lote.HembrasL ?? 0;
                var machosAntes = lote.MachosL ?? 0;
                var acciones = new List<string>();

                // 1) Confirmar ventas pendientes vencidas con el flujo real de la app
                //    (estado → Completado, fecha_procesamiento, descuento del maestro; el histórico
                //    unificado ya existe desde la creación, no se duplica).
                var estadoInicial = await LoadEstadoLoteAsync(loteBase, ct);
                var nConfirmados = 0;
                foreach (var movId in estadoInicial.PendVencidosIds)
                {
                    var dto = await _movimientoSvc.CompleteAsync(movId)
                        ?? throw new InvalidOperationException($"Movimiento pendiente {movId} no encontrado al confirmar (lote {v.LoteAveEngordeId}).");
                    nConfirmados++;
                }
                if (nConfirmados > 0) acciones.Add(TipoDescuadreAvesEngorde.PendientesSinConfirmar);
                totalConfirmados += nConfirmados;

                // 2) Re-sync del maestro (recargado: la confirmación ya pudo descontar)
                var loteBase2 = new LoteBase(v.LoteAveEngordeId, lote.LoteNombre, lote.GranjaId, lote.GalponId,
                    lote.EstadoOperativoLote, lote.LiquidadoAt, lote.HembrasL ?? 0, lote.MachosL ?? 0,
                    lote.Mixtas ?? 0, lote.AvesEncasetadas ?? 0);
                var estado2 = await LoadEstadoLoteAsync(loteBase2, ct);
                var resync = CalcularResyncMaestro(estado2);
                int ajusteH = 0, ajusteM = 0;
                string? observacion = null;
                if (!resync.Exacto)
                {
                    observacion = resync.Detalle;
                }
                else if (resync.AjusteH > 0 || resync.AjusteM > 0)
                {
                    ajusteH += resync.AjusteH;
                    ajusteM += resync.AjusteM;
                    lote.HembrasL = Math.Max(0, (lote.HembrasL ?? 0) - resync.AjusteH);
                    lote.MachosL = Math.Max(0, (lote.MachosL ?? 0) - resync.AjusteM);
                    acciones.Add(TipoDescuadreAvesEngorde.MaestroNoDescontado);
                    // Auditoría re-sync: sustituye el descuento de ventas que no descontaron
                    // (NO participa en la conservación → no se re-aplica en corridas futuras).
                    _ctx.HistorialLotePolloEngorde.Add(new HistorialLotePolloEngorde
                    {
                        CompanyId = companyId,
                        TipoLote = "LoteAveEngorde",
                        LoteAveEngordeId = v.LoteAveEngordeId,
                        TipoRegistro = TipoRegistroAjusteResync,
                        AvesHembras = resync.AjusteH,
                        AvesMachos = resync.AjusteM,
                        AvesMixtas = 0,
                        FechaRegistro = ahora,
                        CreatedAt = ahora
                    });
                    await _ctx.SaveChangesAsync(ct); // persistir antes de recalcular disponibles
                }

                // 3) Fantasma en cerrados: disponibles deben quedar en 0
                var esCerrado = string.Equals(lote.EstadoOperativoLote, EstadoCerrado, StringComparison.OrdinalIgnoreCase);
                if (esCerrado)
                {
                    var disp = await _avesDisponiblesSvc.GetAvesDisponiblesAsync(v.LoteAveEngordeId);
                    var fantH = disp?.HembrasDisponibles ?? 0;
                    var fantM = disp?.MachosDisponibles ?? 0;
                    if (fantH > 0 || fantM > 0)
                    {
                        ajusteH += fantH;
                        ajusteM += fantM;
                        lote.HembrasL = Math.Max(0, (lote.HembrasL ?? 0) - fantH);
                        lote.MachosL = Math.Max(0, (lote.MachosL ?? 0) - fantM);
                        acciones.Add(TipoDescuadreAvesEngorde.FantasmaCerrado);
                        // Auditoría fantasma: aves nunca descargadas (SÍ participa en la conservación).
                        _ctx.HistorialLotePolloEngorde.Add(new HistorialLotePolloEngorde
                        {
                            CompanyId = companyId,
                            TipoLote = "LoteAveEngorde",
                            LoteAveEngordeId = v.LoteAveEngordeId,
                            TipoRegistro = TipoRegistroAjusteFantasma,
                            AvesHembras = fantH,
                            AvesMachos = fantM,
                            AvesMixtas = 0,
                            FechaRegistro = ahora,
                            CreatedAt = ahora
                        });
                    }
                }
                if (acciones.Count > 0 || nConfirmados > 0)
                {
                    lote.UpdatedByUserId = _current.UserId;
                    lote.UpdatedAt = ahora;
                    corregidos++;
                }
                await _ctx.SaveChangesAsync(ct);

                items.Add(new CorreccionAvesDisponiblesLoteDto
                {
                    LoteAveEngordeId = v.LoteAveEngordeId,
                    LoteNombre = lote.LoteNombre,
                    GalponId = lote.GalponId,
                    Acciones = acciones,
                    MovimientosConfirmados = nConfirmados,
                    HembrasLAntes = hembrasAntes,
                    MachosLAntes = machosAntes,
                    ConfirmadosHembras = estadoInicial.PendVencidosH,
                    ConfirmadosMachos = estadoInicial.PendVencidosM,
                    AjusteHembras = ajusteH,
                    AjusteMachos = ajusteM,
                    HembrasLDespues = lote.HembrasL ?? 0,
                    MachosLDespues = lote.MachosL ?? 0,
                    Corregido = true,
                    Observacion = observacion
                });
            }
            await tx.CommitAsync(ct);
        }

        return new CorreccionAvesDisponiblesResponse
        {
            LoteNombre = request.LoteNombre?.Trim(),
            DryRun = request.DryRun,
            LotesEvaluados = validacion.Count,
            LotesConDescuadre = conDescuadre.Count,
            LotesCorregidos = corregidos,
            MovimientosConfirmados = totalConfirmados,
            Items = items
        };
    }

    private static List<string> BuildAcciones(ValidacionAvesDisponiblesLoteDto v)
    {
        var acciones = new List<string>();
        if (v.MovimientosPendientesVencidos > 0) acciones.Add(TipoDescuadreAvesEngorde.PendientesSinConfirmar);
        if (v.TipoDescuadre == TipoDescuadreAvesEngorde.MaestroNoDescontado) acciones.Add(TipoDescuadreAvesEngorde.MaestroNoDescontado);
        if (v.TipoDescuadre == TipoDescuadreAvesEngorde.FantasmaCerrado) acciones.Add(TipoDescuadreAvesEngorde.FantasmaCerrado);
        return acciones;
    }
}
