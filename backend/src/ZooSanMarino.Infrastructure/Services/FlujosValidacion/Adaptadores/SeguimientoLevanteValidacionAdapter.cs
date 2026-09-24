// Adaptador SEGUIMIENTO_LEVANTE (plan §16): conecta seguimiento_diario (tipo 'levante') con el
// motor genérico. NO reimplementa el descuento de alimento/aves: delega en
// IValidacionSeguimientoService.ValidarSinPermisoAsync, el mismo núcleo que usa el modo LEGACY_UN_PASO
// (una sola fórmula por número — CLAUDE.md).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoLevanteValidacionAdapter : IProcesoValidacionAdapter
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly IValidacionSeguimientoService _validacion;

    public SeguimientoLevanteValidacionAdapter(ZooSanMarinoContext ctx, IValidacionSeguimientoService validacion)
    {
        _ctx = ctx;
        _validacion = validacion;
    }

    public string AdapterKey => "SEGUIMIENTO_LEVANTE";

    private static long ParsearId(string recursoId) =>
        long.TryParse(recursoId, out var id) ? id : throw new InvalidOperationException($"recursoId inválido para Levante: '{recursoId}'.");

    public async Task<int?> ResolverCompanyIdAsync(string recursoId, CancellationToken ct = default)
    {
        var id = ParsearId(recursoId);
        var r = await _ctx.SeguimientoDiario.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.LotePosturaLevanteId, s.LoteId })
            .FirstOrDefaultAsync(ct);
        if (r is null) return null;

        if (r.LotePosturaLevanteId.HasValue)
        {
            var companyLpl = await _ctx.LotePosturaLevante.AsNoTracking()
                .Where(l => l.LotePosturaLevanteId == r.LotePosturaLevanteId.Value)
                .Select(l => (int?)l.CompanyId).FirstOrDefaultAsync(ct);
            if (companyLpl.HasValue) return companyLpl;
        }
        if (int.TryParse(r.LoteId, out var loteInt))
        {
            return await _ctx.Lotes.AsNoTracking().Where(l => l.LoteId == loteInt)
                .Select(l => (int?)l.CompanyId).FirstOrDefaultAsync(ct);
        }
        return null;
    }

    public async Task<ResumenRecursoValidacion?> ResumirAsync(string recursoId, CancellationToken ct = default)
    {
        var id = ParsearId(recursoId);
        var s = await _ctx.SeguimientoDiario.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.LotePosturaLevanteId, x.Fecha })
            .FirstOrDefaultAsync(ct);
        if (s is null) return null;

        var companyId = await ResolverCompanyIdAsync(recursoId, ct);
        if (!companyId.HasValue) return null;

        string farmNombre = "";
        string? nucleoId = null, galponId = null, loteRef = null;
        if (s.LotePosturaLevanteId.HasValue)
        {
            var lote = await _ctx.LotePosturaLevante.AsNoTracking()
                .Where(l => l.LotePosturaLevanteId == s.LotePosturaLevanteId.Value)
                .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId, l.LoteNombre })
                .FirstOrDefaultAsync(ct);
            if (lote is not null)
            {
                nucleoId = lote.NucleoId;
                galponId = lote.GalponId;
                loteRef = lote.LoteNombre;
                farmNombre = await _ctx.Farms.AsNoTracking().Where(f => f.Id == lote.GranjaId)
                    .Select(f => f.Name).FirstOrDefaultAsync(ct) ?? "";
            }
        }

        return new ResumenRecursoValidacion(companyId.Value, farmNombre, nucleoId, galponId, loteRef,
            DateOnly.FromDateTime(s.Fecha));
    }

    public async Task FinalizarAsync(string recursoId, CancellationToken ct = default) =>
        await _validacion.ValidarSinPermisoAsync(ModuloSeguimiento.Levante, ParsearId(recursoId), ct);

    public async Task LiberarAsync(string recursoId, CancellationToken ct = default) =>
        await _validacion.LiberarAsync(ModuloSeguimiento.Levante, ParsearId(recursoId), ct);
}
