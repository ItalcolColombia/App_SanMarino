// Funciones/TrasladoAvesDesdeSegService.Cohortes.cs
// Cohortes de aves por lote: registro en cada traslado (edad heredada del lote origen) y lectura
// de las edades presentes en un lote (cohorte propia + cohortes recibidas).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class TrasladoAvesDesdeSegService
{
    /// <summary>Largo máximo de <c>lote_aves_cohortes.observaciones</c>.</summary>
    private const int MaxObservacionesCohorte = 300;

    /// <summary>
    /// Registra la cohorte de aves que ingresa al lote DESTINO conservando la edad del lote ORIGEN.
    /// <para>
    /// La fecha de encasetamiento se toma de <c>lotes.fecha_encaset</c> del lote base origen y, si
    /// está vacía, de la del espejo (LPL/LPP). Si no hay ninguna, NO se crea la cohorte y el traslado
    /// continúa normalmente (la edad heredada es informativa: nunca debe tumbar un traslado).
    /// </para>
    /// Corre dentro de la MISMA transacción del traslado, después del <c>SaveChanges</c> que asigna
    /// el id del <see cref="MovimientoAves"/> de auditoría (la cohorte queda ligada a él para poder
    /// revertirse cuando ese movimiento se elimina).
    /// </summary>
    private async Task RegistrarCohorteDestinoAsync(
        LadoTraslado origen,
        LadoTraslado destino,
        TrasladoAvesDesdeSegDiarioDto dto,
        int movimientoAvesId,
        int companyIdFallback,
        int usuarioId,
        CancellationToken ct)
    {
        // Del lote base origen se toman el encasetamiento (edad heredada) y la ubicación que se CONGELA
        // en la cohorte: un receptor con aves de varias procedencias tiene que poder decir de qué
        // granja/galpón vino cada grupo aunque el lote origen se reubique o se elimine después.
        var loteOrigen = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == origen.LoteBaseId)
            .Select(l => new { l.FechaEncaset, l.GranjaId, l.NucleoId, l.GalponId })
            .FirstOrDefaultAsync(ct);

        var encaset = loteOrigen?.FechaEncaset ?? origen.FechaEncasetEspejo;
        if (encaset is null) return;

        // Empresa del ORIGEN (granja del lote); si no resuelve, la empresa efectiva de la sesión.
        var companyId = await ResolverCompanyIdDeGranjaAsync(origen.GranjaId, ct) ?? companyIdFallback;

        var observaciones = string.IsNullOrWhiteSpace(dto.Observaciones)
            ? $"Traslado desde {origen.LoteNombre}"
            : $"Traslado desde {origen.LoteNombre}. {dto.Observaciones.Trim()}";
        if (observaciones.Length > MaxObservacionesCohorte)
            observaciones = observaciones[..MaxObservacionesCohorte];

        _ctx.LoteAvesCohortes.Add(new LoteAvesCohorte
        {
            CompanyId = companyId,
            LoteId = destino.LoteBaseId,
            LoteOrigenId = origen.LoteBaseId,
            MovimientoAvesId = movimientoAvesId,
            GranjaOrigenId = loteOrigen?.GranjaId ?? origen.GranjaId,
            NucleoOrigenId = loteOrigen?.NucleoId,
            GalponOrigenId = loteOrigen?.GalponId,
            FechaIngreso = DateOnly.FromDateTime(dto.FechaSeguimiento.Date),
            FechaEncasetCohorte = DateOnly.FromDateTime(encaset.Value),
            CantidadHembras = dto.TrasladoHembras,
            CantidadMachos = dto.TrasladoMachos,
            Observaciones = observaciones,
            CreatedByUserId = usuarioId,
            CreatedAt = DateTime.UtcNow
        });

        await _ctx.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<LoteCohortesDto?> GetCohortesLoteAsync(int loteId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);

        var lote = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.DeletedAt == null)
            .Select(l => new { l.LoteNombre, l.FechaEncaset, l.GranjaId })
            .FirstOrDefaultAsync(ct);
        if (lote is null) return null;

        // Scope multi-empresa: la empresa efectiva debe ser la dueña de la granja del lote.
        var companyGranja = await ResolverCompanyIdDeGranjaAsync(lote.GranjaId, ct);
        if (companyGranja is null || companyGranja != companyId) return null;

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // El nombre del lote origen y el de la granja de procedencia se resuelven en la BD (LEFT JOIN),
        // no en memoria.
        var filas = await (
            from c in _ctx.LoteAvesCohortes.AsNoTracking()
            join lo in _ctx.Lotes.AsNoTracking() on c.LoteOrigenId equals lo.LoteId into loj
            from lo in loj.DefaultIfEmpty()
            join f in _ctx.Farms.AsNoTracking() on c.GranjaOrigenId equals f.Id into fj
            from f in fj.DefaultIfEmpty()
            where c.LoteId == loteId && c.DeletedAt == null
            orderby c.FechaIngreso descending, c.Id descending
            select new
            {
                c.Id,
                c.LoteOrigenId,
                LoteOrigenNombre = (string?)(lo == null ? null : lo.LoteNombre),
                GranjaOrigenNombre = (string?)(f == null ? null : f.Name),
                c.NucleoOrigenId,
                c.GalponOrigenId,
                c.FechaIngreso,
                c.FechaEncasetCohorte,
                c.CantidadHembras,
                c.CantidadMachos,
                c.Observaciones
            }).ToListAsync(ct);

        var cohortes = filas.Select(f => new LoteCohorteDto(
            Id: f.Id,
            LoteOrigenId: f.LoteOrigenId,
            LoteOrigenNombre: f.LoteOrigenNombre,
            UbicacionOrigen: LoteCohortesCalculos.DescribirUbicacionOrigen(
                f.GranjaOrigenNombre, f.NucleoOrigenId, f.GalponOrigenId),
            FechaIngreso: f.FechaIngreso,
            FechaEncasetCohorte: f.FechaEncasetCohorte,
            EdadDias: LoteCohortesCalculos.EdadDias(f.FechaEncasetCohorte, hoy),
            EdadSemanas: LoteCohortesCalculos.EdadSemanas(f.FechaEncasetCohorte, hoy),
            CantidadHembras: f.CantidadHembras,
            CantidadMachos: f.CantidadMachos,
            Observaciones: f.Observaciones
        )).ToList();

        DateOnly? encasetPropia = lote.FechaEncaset is DateTime fe ? DateOnly.FromDateTime(fe) : null;

        // Aves propias = saldo real del lote − lo recibido por traslado, para poder cuadrar
        // propias + recibidas = saldo. El saldo sale del resumen de mortalidad, que es la misma fuente
        // que usa el modal de traslado (no se inventa un número nuevo).
        var resumen = await _loteService.GetMortalidadResumenAsync(loteId);
        int? hembrasPropias = null, machosPropias = null;
        if (resumen != null)
        {
            hembrasPropias = LoteCohortesCalculos.PropiasDelLote(resumen.SaldoHembras, filas.Sum(f => f.CantidadHembras));
            machosPropias = LoteCohortesCalculos.PropiasDelLote(resumen.SaldoMachos, filas.Sum(f => f.CantidadMachos));
        }

        return new LoteCohortesDto(
            LoteId: loteId,
            LoteNombre: lote.LoteNombre,
            FechaEncasetPropia: encasetPropia,
            EdadPropiaDias: encasetPropia is DateOnly ep ? LoteCohortesCalculos.EdadDias(ep, hoy) : null,
            EdadPropiaSemanas: encasetPropia is DateOnly ep2 ? LoteCohortesCalculos.EdadSemanas(ep2, hoy) : null,
            HembrasPropias: hembrasPropias,
            MachosPropias: machosPropias,
            Cohortes: cohortes
        );
    }
}
