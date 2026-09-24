// Adaptador SEGUIMIENTO_PRODUCCION (plan §16): conecta seguimiento_diario_produccion con el motor
// genérico. NO reimplementa el descuento de alimento/aves: delega en
// IValidacionSeguimientoService.ValidarSinPermisoAsync (una sola fórmula por número).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoProduccionValidacionAdapter : IProcesoValidacionAdapter
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly IValidacionSeguimientoService _validacion;

    public SeguimientoProduccionValidacionAdapter(ZooSanMarinoContext ctx, IValidacionSeguimientoService validacion)
    {
        _ctx = ctx;
        _validacion = validacion;
    }

    public string AdapterKey => "SEGUIMIENTO_PRODUCCION";

    private static long ParsearId(string recursoId) =>
        long.TryParse(recursoId, out var id) ? id : throw new InvalidOperationException($"recursoId inválido para Producción: '{recursoId}'.");

    public async Task<int?> ResolverCompanyIdAsync(string recursoId, CancellationToken ct = default)
    {
        var id = ParsearId(recursoId);
        return await _ctx.SeguimientoProduccion.AsNoTracking()
            .Where(s => s.Id == id && s.DeletedAt == null)
            .Select(s => (int?)s.CompanyId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ResumenRecursoValidacion?> ResumirAsync(string recursoId, CancellationToken ct = default)
    {
        var id = ParsearId(recursoId);
        var s = await _ctx.SeguimientoProduccion.AsNoTracking()
            .Where(x => x.Id == id && x.DeletedAt == null)
            .Select(x => new { x.CompanyId, x.Fecha, x.LotePosturaProduccionId })
            .FirstOrDefaultAsync(ct);
        if (s is null) return null;

        string farmNombre = "";
        string? nucleoId = null, galponId = null, loteRef = null;
        if (s.LotePosturaProduccionId.HasValue)
        {
            var lote = await _ctx.LotePosturaProduccion.AsNoTracking()
                .Where(l => l.LotePosturaProduccionId == s.LotePosturaProduccionId.Value)
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

        return new ResumenRecursoValidacion(s.CompanyId, farmNombre, nucleoId, galponId, loteRef,
            DateOnly.FromDateTime(s.Fecha));
    }

    public async Task FinalizarAsync(string recursoId, CancellationToken ct = default) =>
        await _validacion.ValidarSinPermisoAsync(ModuloSeguimiento.Produccion, ParsearId(recursoId), ct);

    public async Task LiberarAsync(string recursoId, CancellationToken ct = default) =>
        await _validacion.LiberarAsync(ModuloSeguimiento.Produccion, ParsearId(recursoId), ct);
}
