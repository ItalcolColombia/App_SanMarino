// src/ZooSanMarino.Infrastructure/Services/CorreccionAvesDisponiblesEngordeService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Diagnóstico y corrección de "aves disponibles fantasma" en lotes pollo engorde:
/// lotes Cerrados cuyo maestro (hembras_l/machos_l − bajas del seguimiento) aún reporta
/// aves disponibles que nunca fueron descargadas en ningún registro (encaset &gt; bajas + ventas),
/// típicamente por atribución de género imprecisa al final del ciclo.
/// Plan: fase_de_desarrollo/correccion_aves_disponibles_engorde_2601_plan.md
/// </summary>
public class CorreccionAvesDisponiblesEngordeService : ICorreccionAvesDisponiblesEngordeService
{
    private const string EstadoCerrado = "Cerrado";
    private const string TipoEventoVentaAves = "VENTA_AVES";

    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly ILoteReproductoraAveEngordeService _avesDisponiblesSvc;

    public CorreccionAvesDisponiblesEngordeService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        ILoteReproductoraAveEngordeService avesDisponiblesSvc)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _avesDisponiblesSvc = avesDisponiblesSvc;
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

    public async Task<IReadOnlyList<ValidacionAvesDisponiblesLoteDto>> ValidarPorNombreAsync(string loteNombre, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(loteNombre))
            throw new InvalidOperationException("loteNombre es requerido.");

        var nombre = loteNombre.Trim();
        var companyId = await GetEffectiveCompanyIdAsync();

        var lotes = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteNombre == nombre)
            .OrderBy(l => l.LoteAveEngordeId)
            .Select(l => new
            {
                Id = l.LoteAveEngordeId!.Value,
                l.LoteNombre,
                l.GranjaId,
                l.GalponId,
                l.EstadoOperativoLote,
                l.LiquidadoAt,
                HembrasL = l.HembrasL ?? 0,
                MachosL = l.MachosL ?? 0
            })
            .ToListAsync(ct);
        if (lotes.Count == 0) return Array.Empty<ValidacionAvesDisponiblesLoteDto>();

        var ids = lotes.Select(l => l.Id).ToList();

        // Bajas del seguimiento diario por género + fecha del último registro.
        var segPorLote = (await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => ids.Contains(s.LoteAveEngordeId))
            .GroupBy(s => s.LoteAveEngordeId)
            .Select(g => new
            {
                Id = g.Key,
                BajasH = g.Sum(s => (s.MortalidadHembras ?? 0) + (s.SelH ?? 0) + (s.ErrorSexajeHembras ?? 0)),
                BajasM = g.Sum(s => (s.MortalidadMachos ?? 0) + (s.SelM ?? 0) + (s.ErrorSexajeMachos ?? 0)),
                UltimoSeg = g.Max(s => s.Fecha)
            })
            .ToListAsync(ct)).ToDictionary(x => x.Id);

        // Ventas de aves no anuladas por género (misma fuente que usa fn_seguimiento_diario_engorde).
        var ventasPorLote = (await _ctx.LoteRegistroHistoricoUnificados.AsNoTracking()
            .Where(h => h.LoteAveEngordeId != null && ids.Contains(h.LoteAveEngordeId.Value)
                     && h.TipoEvento == TipoEventoVentaAves && !h.Anulado)
            .GroupBy(h => h.LoteAveEngordeId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                VenH = g.Sum(h => h.CantidadHembras ?? 0),
                VenM = g.Sum(h => h.CantidadMachos ?? 0),
                VenX = g.Sum(h => h.CantidadMixtas ?? 0),
                UltimaVenta = g.Max(h => h.FechaOperacion)
            })
            .ToListAsync(ct)).ToDictionary(x => x.Id);

        var result = new List<ValidacionAvesDisponiblesLoteDto>(lotes.Count);
        foreach (var lote in lotes)
        {
            segPorLote.TryGetValue(lote.Id, out var seg);
            ventasPorLote.TryGetValue(lote.Id, out var ven);

            // Ventas posteriores al último seguimiento (no visibles en la tabla diaria v6).
            int ventasPostSeg = 0;
            if (seg != null && ven != null && ven.UltimaVenta.Date > seg.UltimoSeg.Date)
            {
                ventasPostSeg = await _ctx.LoteRegistroHistoricoUnificados.AsNoTracking()
                    .Where(h => h.LoteAveEngordeId == lote.Id && h.TipoEvento == TipoEventoVentaAves && !h.Anulado
                             && h.FechaOperacion.Date > seg.UltimoSeg.Date)
                    .SumAsync(h => (h.CantidadHembras ?? 0) + (h.CantidadMachos ?? 0) + (h.CantidadMixtas ?? 0), ct);
            }

            // Disponibilidad con la MISMA fórmula del endpoint aves-disponibles (cero divergencia).
            var disp = await _avesDisponiblesSvc.GetAvesDisponiblesAsync(lote.Id);
            var dispH = disp?.HembrasDisponibles ?? 0;
            var dispM = disp?.MachosDisponibles ?? 0;

            var esCerrado = string.Equals(lote.EstadoOperativoLote, EstadoCerrado, StringComparison.OrdinalIgnoreCase);
            var requiereCorreccion = esCerrado && (dispH > 0 || dispM > 0);

            result.Add(new ValidacionAvesDisponiblesLoteDto
            {
                LoteAveEngordeId = lote.Id,
                LoteNombre = lote.LoteNombre,
                GranjaId = lote.GranjaId,
                GalponId = lote.GalponId,
                EstadoOperativoLote = lote.EstadoOperativoLote,
                LiquidadoAt = lote.LiquidadoAt,
                HembrasIniciales = disp?.HembrasIniciales ?? 0,
                MachosIniciales = disp?.MachosIniciales ?? 0,
                HembrasL = lote.HembrasL,
                MachosL = lote.MachosL,
                BajasSeguimientoHembras = seg?.BajasH ?? 0,
                BajasSeguimientoMachos = seg?.BajasM ?? 0,
                VentasHembras = ven?.VenH ?? 0,
                VentasMachos = ven?.VenM ?? 0,
                VentasMixtas = ven?.VenX ?? 0,
                VentasPosterioresAlUltimoSeguimiento = ventasPostSeg,
                FechaUltimoSeguimiento = seg?.UltimoSeg,
                FechaUltimaVenta = ven?.UltimaVenta,
                HembrasDisponibles = dispH,
                MachosDisponibles = dispM,
                GeneroSobrante = dispH > 0 && dispM > 0 ? "Ambos"
                               : dispH > 0 ? "Hembras"
                               : dispM > 0 ? "Machos"
                               : null,
                RequiereCorreccion = requiereCorreccion,
                AjusteHembras = requiereCorreccion ? dispH : 0,
                AjusteMachos = requiereCorreccion ? dispM : 0
            });
        }
        return result;
    }

    public async Task<CorreccionAvesDisponiblesResponse> CorregirPorNombreAsync(CorregirAvesDisponiblesRequest request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.LoteNombre))
            throw new InvalidOperationException("LoteNombre es requerido.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var validacion = await ValidarPorNombreAsync(request.LoteNombre, ct);
        var aCorregir = validacion.Where(v => v.RequiereCorreccion).ToList();

        var items = new List<CorreccionAvesDisponiblesLoteDto>(aCorregir.Count);
        var corregidos = 0;

        if (request.DryRun || aCorregir.Count == 0)
        {
            items.AddRange(aCorregir.Select(v => new CorreccionAvesDisponiblesLoteDto
            {
                LoteAveEngordeId = v.LoteAveEngordeId,
                GalponId = v.GalponId,
                HembrasLAntes = v.HembrasL,
                MachosLAntes = v.MachosL,
                AjusteHembras = v.AjusteHembras,
                AjusteMachos = v.AjusteMachos,
                HembrasLDespues = Math.Max(0, v.HembrasL - v.AjusteHembras),
                MachosLDespues = Math.Max(0, v.MachosL - v.AjusteMachos),
                Corregido = false
            }));
        }
        else
        {
            await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
            var ahora = DateTime.UtcNow;
            foreach (var v in aCorregir)
            {
                var lote = await _ctx.LoteAveEngorde
                    .SingleAsync(l => l.LoteAveEngordeId == v.LoteAveEngordeId && l.CompanyId == companyId && l.DeletedAt == null, ct);

                var hembrasAntes = lote.HembrasL ?? 0;
                var machosAntes = lote.MachosL ?? 0;
                lote.HembrasL = Math.Max(0, hembrasAntes - v.AjusteHembras);
                lote.MachosL = Math.Max(0, machosAntes - v.AjusteMachos);
                lote.UpdatedByUserId = _current.UserId;
                lote.UpdatedAt = ahora;

                // Auditoría: aves fantasma descontadas por género (TipoRegistro "Ajuste",
                // valor ya contemplado por HistorialLotePolloEngorde; los lectores filtran "Inicio").
                _ctx.HistorialLotePolloEngorde.Add(new HistorialLotePolloEngorde
                {
                    CompanyId = companyId,
                    TipoLote = "LoteAveEngorde",
                    LoteAveEngordeId = v.LoteAveEngordeId,
                    TipoRegistro = "Ajuste",
                    AvesHembras = v.AjusteHembras,
                    AvesMachos = v.AjusteMachos,
                    AvesMixtas = 0,
                    FechaRegistro = ahora,
                    CreatedAt = ahora
                });

                items.Add(new CorreccionAvesDisponiblesLoteDto
                {
                    LoteAveEngordeId = v.LoteAveEngordeId,
                    GalponId = v.GalponId,
                    HembrasLAntes = hembrasAntes,
                    MachosLAntes = machosAntes,
                    AjusteHembras = v.AjusteHembras,
                    AjusteMachos = v.AjusteMachos,
                    HembrasLDespues = lote.HembrasL ?? 0,
                    MachosLDespues = lote.MachosL ?? 0,
                    Corregido = true
                });
                corregidos++;
            }
            await _ctx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return new CorreccionAvesDisponiblesResponse
        {
            LoteNombre = request.LoteNombre.Trim(),
            DryRun = request.DryRun,
            LotesEvaluados = validacion.Count,
            LotesConDescuadre = aCorregir.Count,
            LotesCorregidos = corregidos,
            Items = items
        };
    }
}
